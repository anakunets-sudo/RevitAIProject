using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public class RevitTaskHandler : IExternalEventHandler
    {
        public RevitTaskHandler(TaskCompletionSource<bool> tcs)
        {
            _tcs = tcs;
        }

        // Очередь действий для выполнения
        private readonly Queue<Action<UIApplication>> _actions = new Queue<Action<UIApplication>>();
        private readonly object _lock = new object();
        private TaskCompletionSource<bool> _tcs;

        public void Enqueue(Action<UIApplication> action)
        {
            lock (_lock)
            {
                _actions.Enqueue(action);
            }
        }

        public void Execute(UIApplication app)
        {
            _tcs = new TaskCompletionSource<bool>();

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

                _tcs?.TrySetResult(true);
            }
        }

        public string GetName()
        {
            return "Revit AI Task Handler";
        }
    }
}
