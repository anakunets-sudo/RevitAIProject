using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace RevitAIProject.Services
{
    public class RevitTaskHandler : IExternalEventHandler
    {
        // Очередь действий для выполнения
        private readonly Queue<Action<UIApplication>> _actions = new Queue<Action<UIApplication>>();
        private readonly object _lock = new object();

        public void Enqueue(Action<UIApplication> action)
        {
            lock (_lock)
            {
                _actions.Enqueue(action);
            }
        }

        public void Execute(UIApplication app)
        {
            Action<UIApplication> action = null;

            lock (_lock)
            {
                if (_actions.Count > 0)
                {
                    action = _actions.Dequeue();
                }
            }

            // Выполняем действие в потоке Revit
            if (action != null)
            {
                try
                {
                    action(app);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Revit AI Error", ex.Message);
                }
            }
        }

        public string GetName()
        {
            return "Revit AI Task Handler";
        }
    }
}
