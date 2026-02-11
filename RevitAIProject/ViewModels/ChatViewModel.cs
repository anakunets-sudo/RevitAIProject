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
                    if (string.IsNullOrEmpty(aiResult.Message) && (aiResult.Actions == null || aiResult.Actions.Count == 0))
                    {
                        aiResult.Message = "Извините, я не понял запрос.";
                    }

                    RemoveLastAiThinkingLine();

                    // Выводим предварительное сообщение (напр. "Проверяю стены...")
                    AddFormattedMessage(aiResult.Message, RevitMessageType.Ai);

                    _revitApiService.SessionContext.Reset();

                    // Шаг 2: Выполнение действий
                    foreach (var action in aiResult.Actions)
                    {
                        action.Execute(_revitApiService);
                    }

                    // Теперь await ДЕЙСТВИТЕЛЬНО остановит выполнение метода
                    // до тех пор, пока в RevitTaskHandler не сработает SetResult
                    await _revitApiService.RaiseAsync();

                    // Шаг 3: Собираем детальный отчет с привязкой к категориям
                    var storage = _revitApiService.SessionContext.Storage;

                    // Сопоставляем ключи из хранилища с тем, что запрашивал ИИ в текущем ходу
                    var reportDetails = new List<string>();
                    foreach (var kvp in storage)
                    {
                        // Теперь мы ищем среди объектов IRevitLogic, так как добавили свойства в базу
                        var originalAction = aiResult.Actions.Where(e=>e is IRevitQuery)?
                            .Cast<IRevitQuery>() // Приводим к базовому классу
                            .FirstOrDefault(a => a.SearchAiName == kvp.Key);

                        string categoryInfo = "";
                        if (originalAction != null && !string.IsNullOrEmpty(originalAction.CategoryName))
                        {
                            categoryInfo = $" ({originalAction.CategoryName})";
                        }

                        reportDetails.Add($"{kvp.Key}{categoryInfo}: {kvp.Value.Count} elements");
                    }

                    string detailedSummary = string.Join(", ", reportDetails);

                    // Формируем системный отчет. Теперь ИИ увидит: "$q1 (OST_Floors): 1 elements, $q2 (OST_Walls): 10 elements"
                    string feedbackPrompt = $"SYSTEM_REPORT: Actions completed. " +
                                            $"Storage contents: [{detailedSummary}]. " +
                                            $"User request: '{userRequest}'. " +
                                            $"Answer clearly based on the storage data above.";

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
                    case RevitMessageType.Info: prefix = "Система: "; break;
                    case RevitMessageType.Error: prefix = "Ошибка: "; break;
                    case RevitMessageType.Ai: prefix = "AI: "; break;
                    case RevitMessageType.User: prefix = "Вы: "; break;
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