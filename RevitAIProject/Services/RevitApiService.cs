using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Logic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace RevitAIProject.Services
{

    public class RevitApiService : IRevitApiService, Logic.IRevitContext
    {
        public RevitApiService() : this(new RevitTaskHandler())
        {
        }

        public RevitApiService(RevitTaskHandler handler)
        {
            _handler = handler;
            _externalEvent = ExternalEvent.Create(_handler);
        }

        private readonly ExternalEvent _externalEvent;
        private readonly RevitTaskHandler _handler;

        public event Action<string, RevitMessageType> OnMessageReported;

        public void Report(string message, RevitMessageType messageType)
        {
            // Вызываем событие, если на него кто-то подписан
            OnMessageReported?.Invoke(message, messageType);
        }

        // Реализация IActionContext (безопасные свойства)
        public UIApplication UIApp { get; private set; }
        public UIDocument UIDoc { get; private set; }
        // Это наш "блокнот" для связи имен ИИ с реальными ID Revit
        public Dictionary<string, ElementId> Variables { get; } = new Dictionary<string, ElementId>();

        private readonly List<Action<IRevitContext>> _queue = new List<Action<IRevitContext>>();
        public void AddToQueue(Action<IRevitContext> task) => _queue.Add(task);

        public void Raise()
        {
            var tasks = _queue.ToList();
            _queue.Clear();

            _handler.Enqueue(uiApp => {

                this.UIApp = uiApp; // Устанавливаем текущий контекст

                this.UIDoc = UIApp.ActiveUIDocument; // Устанавливаем текущий контекст

                foreach (var task in tasks) task(this); // Передаем 'this' как IActionContext

            });

            _externalEvent.Raise();
        }
    }
}

       
