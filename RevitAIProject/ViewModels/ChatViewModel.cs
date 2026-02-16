using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using RevitAIProject.Logic;
using RevitAIProject.Logic.Actions;
using RevitAIProject.Logic.Queries; // Добавили пространство имен для запросов
using RevitAIProject.Models;
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
        ISessionContext _sessionContext;

        private bool _isRatingVisible;
        private string _lastAiJson; // Храним JSON последнего ответа
        private string _lastUserRequest; // Храним текст последнего запроса
        IExperienceRepository _experienceRepository;


        private string _userInput;
        private string _chatHistory;
        private bool _isBusy;

        private bool _isRated;
        public bool IsRated { get => _isRated; set => Set(ref _isRated, value); }

        // Обновили конструктор, добавив SessionContext
        public ChatViewModel(IOllamaService ollamaService, IExperienceRepository _experienceRepo, IRevitApiService revitApiService, VoiceService voice, IUIDispatcherHelper dispatcher, ISessionContext sessionContext)
        {
            _ollamaService = ollamaService;
            _revitApiService = revitApiService;
            _voice = voice;
            _dispatcher = dispatcher;

            _experienceRepository = _experienceRepo;

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

            RateCommand = new RelayCommand<string>(ExecuteRateCommand);
        }

        /// <summary>
        /// Handles user feedback on the last AI response.
        /// </summary>
        /// <param name="ratingValue">Rating from -2 (Terrible) to 2 (Excellent).</param>
        private void ExecuteRateCommand(string ratingValue)
        {
            if (int.TryParse(ratingValue, out int rating))
            {
                // 1. Создаем запись на основе данных, которые мы припасли в SendMessage
                var record = new ExperienceRecord
                {
                    UserPrompt = _lastUserRequest,
                    AiJson = _lastAiJson,
                    Rating = rating,
                    Timestamp = DateTime.Now
                };

                // 2. Сохраняем в наш JSON-репозиторий
                _experienceRepository.Save(record);

                // 3. Визуальный отклик
                IsRated = true; // Кнопки станут яркими (100% Opacity) и заблокируются

                // 4. Опционально: благодарим пользователя в чате
                if (rating >= 1)
                    AddFormattedMessage("Pattern saved as gold standard. Confidence increased.", RevitMessageType.Info);
                else if (rating <= -1)
                    AddFormattedMessage("Logic criticized. Analyzing for future improvements.", RevitMessageType.Warning);

                // 5. Плавно скрываем панель через 3 секунды
                Task.Delay(3000).ContinueWith(_ => {
                    IsRatingVisible = false;
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
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

                // Сбрасываем состояние рейтинга перед новым запросом
                IsRatingVisible = false;
                IsRated = false;

                string userRequest = UserInput;
                AddFormattedMessage(userRequest, RevitMessageType.User);
                IsBusy = true;

                AddFormattedMessage("Thinking...", RevitMessageType.Ai);

                // Шаг 1: Первичный запрос к ИИ (Здесь рождается логика "паровоза")
                var aiResult = await _ollamaService.GetAiResponseAsync(userRequest, _sessionContext, _cts.Token);
                UserInput = string.Empty;

                if (!_cts.Token.IsCancellationRequested && aiResult != null)
                {
                    RemoveLastAiThinkingLine();
                    AddFormattedMessage(aiResult.Message, RevitMessageType.Ai);

                    // КЛЮЧЕВОЙ МОМЕНТ: Запоминаем данные для обучения
                    _lastUserRequest = userRequest;
                    _lastAiJson = aiResult.RawJson;

                    // Очищаем "короткую память" перед новым циклом
                    _revitApiService.SessionContext.Reset();

                    // Шаг 2: Наполнение очереди и выполнение в Revit
                    foreach (var action in aiResult.Actions)
                    {
                        action.Execute(_revitApiService);
                    }

                    // Ждем завершения всей пачки в потоке Revit
                    await _revitApiService.RaiseAsync();

                    // Шаг 3: Обработка всех типов накопившихся рапортов
                    var allReports = _revitApiService.SessionContext.Reports;
                    foreach (var report in allReports)
                    {
                        if (report.Type == RevitMessageType.Error)
                        {
                            AddFormattedMessage(report.Text, RevitMessageType.Error);
                        }
                        if (report.Type == RevitMessageType.Warning)
                        {
                            AddFormattedMessage(report.Text, RevitMessageType.Warning);
                        }
                    }

                    // Шаг 4: Сборка финального ответа на основе "Timeline"
                    var aiReports = _revitApiService.SessionContext.GetAiMessages();
                    string executionTimeline = string.Join("; ", aiReports);

                    if (string.IsNullOrEmpty(executionTimeline))
                        executionTimeline = "Actions executed, but no reports generated.";

                    string feedbackPrompt = $"SYSTEM_REPORT: {executionTimeline}. " +
                                            $"User request: '{userRequest}'. " +
                                            $"BASED ON THESE REAL FACTS, provide a brief final answer to the user.";

                    // Финальный штрих от ИИ
                    var finalAiResponse = await _ollamaService.GetAiResponseAsync(feedbackPrompt, _sessionContext, _cts.Token);

                    if (finalAiResponse != null && !string.IsNullOrEmpty(finalAiResponse.Message))
                    {
                        AddFormattedMessage(finalAiResponse.Message, RevitMessageType.Ai);

                        // ВКЛЮЧАЕМ СМАЙЛИКИ: Задача полностью завершена, юзер может оценить итог
                        IsRatingVisible = true;
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
        public bool IsRatingVisible { get => _isRatingVisible; set => Set(ref _isRatingVisible, value);}
        public bool IsBusy
        {
            get => _isBusy;
            set { if (Set(ref _isBusy, value)) ((RelayCommand)SendCommand).RaiseCanExecuteChanged(); }
        }
        public ICommand SendCommand { get; }
        public ICommand RecordVoiceCommand { get; }
        public ICommand RateCommand { get; }

        #endregion
    }
}