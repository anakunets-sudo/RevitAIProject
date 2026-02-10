using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface IRevitApiService
    {
        event Action<string, RevitMessageType> OnMessageReported;

        void Report(string message, RevitMessageType messageType);

        // Это наш "блокнот" для связи имен ИИ с реальными ID Revit
        Dictionary<string, ElementId> Variables { get; }

        void AddToQueue(Action<Logic.IRevitContext> task);

        // Универсальный метод для выполнения кода внутри транзакции
        void Raise();
    }
}
