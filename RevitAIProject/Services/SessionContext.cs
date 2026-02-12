using Autodesk.Revit.DB;
using RevitAIProject.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RevitAIProject.Services
{
    /// <summary>
    /// РОЛЬ: Обеспечивет ИИ хранилищем для различного типа данных.
    /// ВВОД: Содержит методы Store для безопасного помещения данных в хранилище. Свойства для чтения доступны свободно.
    /// </summary>
    [AiParam("сurrentCollector", Description = "Provider of storage for various types of data")]
    public class SessionContext : ISessionContext
    {
        [AiParam("сurrentCollector", Description = "Temporary Collector for storing intermediate search results")]
        public FilteredElementCollector CurrentCollector { get; private set; }

        [AiParam("storage", Description = "Unified session storage for all entities. " +
                                  "Stores search results ($q1, $q2) and created/modified elements ($f1, $f2) as lists of ElementIds. " +
                                  "Use these keys in 'target_ai_name' of subsequent actions to reference previously found or created objects.")]
        public Dictionary<string, List<ElementId>> Storage { get; } = new Dictionary<string, List<ElementId>>();

        public void Store(FilteredElementCollector collector)
        {
            CurrentCollector = collector;
        }

        public void Store(string key, IEnumerable<ElementId> foundIds)
        {
            Storage[key] = new List<ElementId>(foundIds);

            CurrentCollector = null;
        }
        public bool StorageValue(string key, out List<ElementId> foundIds)
        {
            Storage.TryGetValue(key, out List<ElementId> typedIds);

            foundIds = typedIds;
            return true;
        }        

        //ISessionReport (Шина событий) ---
        private readonly List<RevitMessage> _reports = new List<RevitMessage>();
        public IReadOnlyList<RevitMessage> Reports => _reports.AsReadOnly();
        public void Report(string message, RevitMessageType type)
        {
            // Создаем структуру сообщения. 
            // В C# 7.3 используем инициализатор объектов.
            var reportEntry = new RevitMessage
            {
                Text = message,
                Type = type,
                Timestamp = DateTime.Now // Фиксируем точное время события
            };

            // Просто добавляем в накопляемый список
            _reports.Add(reportEntry);
        }
        public IEnumerable<string> GetAiMessages()
        {
            return _reports
                .Where(r => r.Type == RevitMessageType.AiReport)
                .Select(r => r.Text);
        }
        public void Reset()
        {
            CurrentCollector = null;
            Storage.Clear();
            _reports.Clear();
        }
    }
}
