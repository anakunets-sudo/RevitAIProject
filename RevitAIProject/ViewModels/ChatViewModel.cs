using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using RevitAIProject.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RevitAIProject.ViewModels
{
    public class ChatViewModel : ViewModelBase
    {
        private readonly IOllamaService _ollamaService;
        private readonly IRevitApiService _revitApiService;

        private string _userInput;
        private string _chatHistory;
        private bool _isBusy;

        // Конструктор без параметров для XAML и Revit
        public ChatViewModel()
            : this(new Services.OllamaService(), new Services.RevitApiService()) // Передаем реальные сервисы
        {
        }

        // Конструктор с параметрами для гибкости и тестов
        public ChatViewModel(IOllamaService ollamaService, IRevitApiService revitApiService)
        {
            _ollamaService = ollamaService;
            _revitApiService = revitApiService;

            // Подписываемся на новый формат сообщений
            _revitApiService.OnMessageReported += AddFormattedMessage;

            SendCommand = new RelayCommand(async () => await SendMessage(), () => !IsBusy);
            ChatHistory = "Система: Ожидание вашего запроса...\n";
        }

        private void AddFormattedMessage(string text, RevitMessageType type)
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

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(UserInput)) return;

            string userRequest = UserInput;
            AddFormattedMessage(userRequest, RevitMessageType.User);
            UserInput = string.Empty;

            IsBusy = true;
            // Сразу уведомляем пользователя, что ИИ начал работу
            AddFormattedMessage("Думаю...", RevitMessageType.Ai);

            try
            {
                var aiResult = await _ollamaService.GetAiResponseAsync(UserInput);

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
