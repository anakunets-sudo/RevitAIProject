using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Logic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{

    /// <summary>
    /// РОЛЬ: Предоставляет классам бепасный доступ к ExternalEvent и хранилищу сессионых данных, а также разделяет логику сервиса для разных классов путем реализации разных интерфейсов. Формирет очередь заданий, которые подготавливают классы реализующие RevitAIProject.Logic.IRevitLogic и дает Ревит команду на выполнение.
    /// </summary>
    public class RevitApiService : IRevitApiService, Logic.IRevitContext
    {
        public RevitApiService()
        {
            _handler = new RevitTaskHandler();
            _externalEvent = ExternalEvent.Create(_handler);
            SessionContext = new SessionContext();
        }

        /*
        public RevitApiService(RevitTaskHandler handler)
        {
            _handler = handler;
            _externalEvent = ExternalEvent.Create(_handler);
            SessionContext = new SessionContext();
        }
        */

        private readonly ExternalEvent _externalEvent;
        private readonly RevitTaskHandler _handler;
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public event Action<string, RevitMessageType> OnMessageReported;

        /// <summary>
        /// РОЛЬ: Безопасное размещение и хранение сессионных данных для ИИ.
        /// </summary>
        public SessionContext SessionContext { get; }

        public void Report(string message, RevitMessageType messageType)
        {
            // Вызываем событие, если на него кто-то подписан
            OnMessageReported?.Invoke(message, messageType);
        }

        // Реализация IActionContext (безопасные свойства)
        public UIApplication UIApp { get; private set; }
        public UIDocument UIDoc { get; private set; }

        private readonly List<Action<IRevitContext>> _queue = new List<Action<IRevitContext>>();
        public void AddToQueue(Action<IRevitContext> task) => _queue.Add(task);

        /// <summary>
        /// РОЛЬ: Подготавливае очедедь заданий, которые формируют, например, классы реализующие интерфейс RevitAIProject.LogicIRevitLogic. В завершении ExternalEvent выполняет Raise().
        /// </summary>
        public async Task RaiseAsync()
        {
            if (_queue.Count == 0) return;

            var tasks = _queue.ToList();

            _queue.Clear();

            // 1. Инициализируем ожидание в хендлере
            // Метод PrepareTask должен вернуть _tcs.Task внутри RevitTaskHandler
            var completionTask = _handler.PrepareTask();

            // 2. Ставим команды в очередь хендлера
            _handler.Enqueue(uiApp => {

                this.UIApp = uiApp;
                this.UIDoc = uiApp.ActiveUIDocument;

                foreach (var task in tasks)
                {
                    task(this);
                }
            });

            // 3. Запускаем событие Revit
            _externalEvent.Raise();

            // 4. КРИТИЧЕСКИЙ МОМЕНТ: Ждем, пока Revit выполнит Execute и вызовет SetResult
            await completionTask;
        }
    }
}

       
