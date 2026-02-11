using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using RevitAIProject.Services;
using RevitAIProject.Views;
using RevitAIProject.Logic.Queries; // Добавили пространство имен для запросов
using System;
using System.Collections.Generic;
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

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            string userRequest = UserInput;
            AddFormattedMessage(userRequest, RevitMessageType.User);

            IsBusy = true;
            AddFormattedMessage("Думаю...", RevitMessageType.Ai);

            try
            {
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

                    // Шаг 2: Выполнение действий
                    foreach (var action in aiResult.Actions)
                    {
                        action.Execute(_revitApiService);
                    }

                    await _revitApiService.RaiseAsync();                    

                    var foundCount = _revitApiService.SessionContext.LastFoundIds.Count;

                    MessageBox.Show(foundCount.ToString());

                    // Формируем системный отчет для ИИ
                    string feedbackPrompt = $"SYSTEM_REPORT: 'GetElements' finished. Found: {foundCount} elements. " +
                                            $"They are saved in LAST_QUERY_RESULT. Answer the user's question: '{userRequest}'";

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