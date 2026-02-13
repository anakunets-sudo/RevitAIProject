using Autodesk.Revit.DB;
using RevitAIProject.Logic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly Dictionary<string, FilteredElementCollector> _collectors = new Dictionary<string, FilteredElementCollector>();

        [AiParam("сollectors", Description = "Temporary Collector for storing intermediate search results")]
        public IReadOnlyDictionary<string, FilteredElementCollector> Collectors => _collectors;

        private readonly Dictionary<string, List<ElementId>> _storage = new Dictionary<string, List<ElementId>>();

        [AiParam("storage", Description = "Unified session storage for all entities. " +
                                  "Stores search results ($q1, $q2) and created/modified elements ($f1, $f2) as lists of ElementIds. " +
                                  "Use these keys in 'target_ai_name' of subsequent actions to reference previously found or created objects.")]
        public IReadOnlyDictionary<string, List<ElementId>> Storage => _storage;

        public void Store(string key, FilteredElementCollector collector)
        {
            _collectors[key] = collector;
        }

        public bool CollectorValue(string key, out FilteredElementCollector collector)
        {
            _collectors.TryGetValue(key, out FilteredElementCollector coll);
            collector = coll;
            return collector == null ? false : true;
        }

        public void Store(string key, IEnumerable<ElementId> foundIds)
        {
            _storage[key] = new List<ElementId>(foundIds);
        }
        public bool StorageValue(string key, out List<ElementId> foundIds)
        {
            Storage.TryGetValue(key, out List<ElementId> typedIds);
            foundIds = typedIds;
            return foundIds == null ? false : true;
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
            _collectors.Clear();
            _storage.Clear();
            _reports.Clear();
        }
    }
}
