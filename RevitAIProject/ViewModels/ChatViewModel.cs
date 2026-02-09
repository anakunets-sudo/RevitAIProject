using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using RevitAIProject.Services;
using RevitAIProject.Views;
using System;
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

        private string _voiceInsert;
        public string VoiceInsert { get => _voiceInsert; set => Set(ref _voiceInsert, value); }

        private string _userInput;
        private string _chatHistory;
        private bool _isBusy;
        private readonly object _uiLock = new object();

        // Конструктор с параметрами для гибкости и тестов
        public ChatViewModel(IOllamaService ollamaService, IRevitApiService revitApiService, VoiceService voice, IUIDispatcherHelper dispatcher)
        {
            _ollamaService = ollamaService;
            _revitApiService = revitApiService;
            _voice = voice;
            _dispatcher = dispatcher;

            // Подписываемся на новый формат сообщений
            _revitApiService.OnMessageReported += AddFormattedMessage;

            SendCommand = new RelayCommand(async () => await SendMessage(), () => !IsBusy);

            ChatHistory = "Система: Ожидание вашего запроса...\n";

            _voice.OnPartialTextReceived += (json) => {

                // Распарсил в фоне - молодец
                var data = JsonConvert.DeserializeObject<dynamic>(json);
                string text = data?.partial;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Используй BeginInvoke для "живого" текста
                    _dispatcher.Invoke(() => { UserInput = text; });
                }
            };

            _voice.OnTextRecognized += (json) => {

                if (string.IsNullOrWhiteSpace(json))
                { // Проверка, что строка не пустая
                    _dispatcher.Invoke(() => IsRecording = false);
                    return;
                }

                var data = JsonConvert.DeserializeObject<dynamic>(json);
                string text = data?.text;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    _dispatcher.Invoke(() => {
                        // Передаем текст в триггер вставки
                        VoiceInsert = text;
                        IsRecording = false;
                    });
                }
                else
                {
                    _dispatcher.Invoke(() => IsRecording = false);
                }
            };

            RecordVoiceCommand = new RelayCommand(async () => { await OnRecordVoice(); });
        }

        private async Task OnRecordVoice()
        {
            if (!IsRecording)
            {
                // 1. Подготовка
                if (IsBusy) _cts?.Cancel();

                // 2. Визуальный отклик
                IsRecording = true;

                // 3. ЗАПУСК (убедитесь, что метод Start() в VoiceService делает то, что мы обсуждали)
                _voice.Start();
            }
            else
            {
                // 1. Визуальный отклик
                IsRecording = false;

                // 2. ОСТАНОВКА
                await _voice.StopAsync();

                // 3. Отправка итогового текста в Ollama (если нужно)
                // SendToOllama(InputText); 
            }
        }

        private void AddFormattedMessage(string text, RevitMessageType type)
        {
            // Гарантируем обновление коллекции в UI потоке
            _dispatcher.Invoke(() => {
                string prefix = "";
                switch (type)
                {
                    case RevitMessageType.Info: prefix = "Система: "; break;
                    case RevitMessageType.Error: prefix = "Ошибка: "; break;
                    case RevitMessageType.Ai: prefix = "AI: "; break;
                    case RevitMessageType.User: prefix = "Вы: "; break;
                }

                // Добавляем в историю с новой строки
                ChatHistory += prefix + text + "\n";
            });
        }

        private void AddFormattedMessage2(string text, RevitMessageType type)
        {
            string prefix = "";
            switch (type)
            {
                case RevitMessageType.Info: prefix = "Система: "; break;
                case RevitMessageType.Error: prefix = "Ошибка: "; break;
                case RevitMessageType.Ai: prefix = "AI: "; break;
                case RevitMessageType.User: prefix = "Вы: "; break;
            }

            // Добавляем в историю с новой строки
            ChatHistory += prefix + text + "\n";
        }

        #region Свойства

        // Свойство для индикации записи (для смены цвета кнопки)
        private bool _isRecording;
        public bool IsRecording
        {
            get => _isRecording;
            set => Set(ref _isRecording, value);
        }

        public string UserInput
        {
            get => _userInput;
            set => Set(ref _userInput, value);
        }

        public string ChatHistory
        {
            get => _chatHistory;
            set => Set(ref _chatHistory, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (Set(ref _isBusy, value))
                    ((RelayCommand)SendCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand SendCommand { get; }

        #endregion
        // Команда для кнопки
        public ICommand RecordVoiceCommand { get; }

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(UserInput)) return;

            // Сброс старого токена и создание нового для текущего запроса
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            string userRequest = UserInput;
            AddFormattedMessage(userRequest, RevitMessageType.User);

            //UserInput = string.Empty;

            IsBusy = true;
            // Сразу уведомляем пользователя, что ИИ начал работу
            AddFormattedMessage("Думаю...", RevitMessageType.Ai);

            try
            {
                var aiResult = await _ollamaService.GetAiResponseAsync(userRequest, _cts.Token);
                UserInput = string.Empty;

                if (!_cts.Token.IsCancellationRequested)
                {

                    //после получения aiResult
                    if (string.IsNullOrEmpty(aiResult.Message) && (aiResult.Actions == null || aiResult.Actions.Count == 0))
                    {
                        aiResult.Message = "Извините, я не понял, как это выполнить. Попробуйте уточнить параметры.";
                    }

                    RemoveLastAiThinkingLine();

                    // Выводим финальный ответ ИИ через нашу систему типов
                    AddFormattedMessage(aiResult.Message, RevitMessageType.Ai);

                    // ViewModel просто перебирает действия и выполняет их через API
                    foreach (var action in aiResult.Actions)
                    {
                        action.Execute(_revitApiService);
                    }

                    _revitApiService.Raise();
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

        private void RemoveLastAiThinkingLine()
        {
            string target = "AI: Думаю...\n";
            if (ChatHistory.EndsWith(target))
            {
                ChatHistory = ChatHistory.Substring(0, ChatHistory.Length - target.Length);
            }
        }
    }
}
