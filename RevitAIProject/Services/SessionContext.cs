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
    public class SessionContext
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

        public void Reset()
        {
            CurrentCollector = null;
            Storage.Clear();
        }
    }
}
