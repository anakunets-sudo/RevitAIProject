using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using RevitAIProject.Logic;
using RevitAIProject.Logic.Actions;
using RevitAIProject.Logic.Queries; // Добавили пространство имен для запросов
using RevitAIProject.Services;
using RevitAIProject.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace RevitAIProject.ViewModels
{
    public class ChatViewModel : ViewModelBase
    {
        private readonly IOllamaService _ollamaService;
        private readonly IRevitApiService _revitApiService;
        private CancellationTokenSource _cts;
        private readonly IUIDispatcherHelper _dispatcher;
        private readonly VoiceService _voice;

        private int _cursorPosition;
        public int CursorPosition { get => _cursorPosition; set => Set(ref _cursorPosition, value); }

        private string _userInput;
        private string _chatHistory;
        private bool _isBusy;
        private readonly object _uiLock = new object();

        // Обновили конструктор, добавив SessionContext
        public ChatViewModel(IOllamaService ollamaService, IRevitApiService revitApiService, VoiceService voice, IUIDispatcherHelper dispatcher)
        {
            _ollamaService = ollamaService;
            _revitApiService = revitApiService;
            _voice = voice;
            _dispatcher = dispatcher;

            _revitApiService.OnMessageReported += AddFormattedMessage;
            SendCommand = new RelayCommand(async () => await SendMessage(), () => !IsBusy);
            ChatHistory = "Система: Ожидание вашего запроса...\n";

            _voice.OnTextRecognized += (text) =>
            {
                _dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrWhiteSpace(UserInput)) UserInput = text;
                    else UserInput += " " + text;
                });
            };

            RecordVoiceCommand = new RelayCommand(() => { OnRecordVoice(); });
        }

        private void OnRecordVoice()
        {
            if (!IsRecording)
            {
                if (IsBusy) _cts?.Cancel();
                IsRecording = true;
                _voice.Start();
            }
            else
            {
                IsRecording = false;
                Task.Run(() => _voice.StopAsync());
            }
        }

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(UserInput)) return;

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                string userRequest = UserInput;
                AddFormattedMessage(userRequest, RevitMessageType.User);
                IsBusy = true;

                AddFormattedMessage("Думаю...", RevitMessageType.Ai);

                // Шаг 1: Первичный запрос к ИИ
                var aiResult = await _ollamaService.GetAiResponseAsync(userRequest, _cts.Token);
                UserInput = string.Empty;

                if (!_cts.Token.IsCancellationRequested)
                {
                    RemoveLastAiThinkingLine();
                    AddFormattedMessage(aiResult.Message, RevitMessageType.Ai);

                    // Очищаем "короткую память" перед новым циклом
                    _revitApiService.SessionContext.Reset(); // Сброс всего

                    // Шаг 2: Наполнение очереди и выполнение в Revit
                    foreach (var action in aiResult.Actions)
                    {
                        action.Execute(_revitApiService);
                    }

                    // Ждем завершения всей пачки в потоке Revit
                    await _revitApiService.RaiseAsync();

                    // Шаг 3: Обработка всех типов накопившихся рапортов
                    var allReports = _revitApiService.SessionContext.Reports; // Берем все сообщения сессии

                    foreach (var report in allReports)
                    {
                        // 1. Если это ошибка — выводим её в чат пользователю немедленно!
                        if (report.Type == RevitMessageType.Error || report.Type == RevitMessageType.Error)
                        {
                            AddFormattedMessage(report.Text, RevitMessageType.Error);
                        }

                        // 2. Если это предупреждение — тоже можно показать
                        if (report.Type == RevitMessageType.Warning)
                        {
                            AddFormattedMessage(report.Text, RevitMessageType.Warning);
                        }
                    }

                    // Шаг 3: Сборка финального ответа на основе "Timeline"
                    // Теперь мы НЕ ПЕРЕБИРАЕМ категории вручную!
                    var aiReports = _revitApiService.SessionContext.GetAiMessages();
                    string executionTimeline = string.Join("; ", aiReports);

                    // Если рапортов нет (например, пустой список команд), даем страховку
                    if (string.IsNullOrEmpty(executionTimeline)) executionTimeline = "Actions executed, but no reports generated.";

                    // Формируем финальный промпт. ИИ увидит историю успеха/ошибок каждого шага.
                    string feedbackPrompt = $"SYSTEM_REPORT: {executionTimeline}. " +
                                            $"User request: '{userRequest}'. " +
                                            $"Final Step: Answer the user based on the execution facts above.";

                    var finalAiResponse = await _ollamaService.GetAiResponseAsync(feedbackPrompt, _cts.Token);

                    if (!string.IsNullOrEmpty(finalAiResponse.Message))
                    {
                        AddFormattedMessage(finalAiResponse.Message, RevitMessageType.Ai);
                    }
                }
            }
            catch (Exception ex)
            {
                RemoveLastAiThinkingLine();
                AddFormattedMessage(ex.Message, RevitMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        #region UI Helpers
        private void AddFormattedMessage(string text, RevitMessageType type)
        {
            _dispatcher.Invoke(() => {
                string prefix = "";
                switch (type)
                {
                    case RevitMessageType.Info: prefix = "System: "; break;
                    case RevitMessageType.Error: prefix = "Error: "; break;
                    case RevitMessageType.Ai: prefix = "AI: "; break;
                    case RevitMessageType.User: prefix = "You: "; break;
                    case RevitMessageType.Warning: prefix = "Attention: "; break;
                }
                ChatHistory += prefix + text + "\n";
            });
        }

        private void RemoveLastAiThinkingLine()
        {
            string target = "AI: Думаю...\n";
            if (ChatHistory.EndsWith(target))
            {
                ChatHistory = ChatHistory.Substring(0, ChatHistory.Length - target.Length);
            }
        }

        private bool _isRecording;
        public bool IsRecording { get => _isRecording; set => Set(ref _isRecording, value); }
        public string UserInput { get => _userInput; set => Set(ref _userInput, value); }
        public string ChatHistory { get => _chatHistory; set => Set(ref _chatHistory, value); }
        public bool IsBusy
        {
            get => _isBusy;
            set { if (Set(ref _isBusy, value)) ((RelayCommand)SendCommand).RaiseCanExecuteChanged(); }
        }
        public ICommand SendCommand { get; }
        public ICommand RecordVoiceCommand { get; }
        #endregion
    }
}