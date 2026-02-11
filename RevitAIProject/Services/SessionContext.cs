using Autodesk.Revit.DB;
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
    public class SessionContext
    {
        // Текущий "живой" итератор Revit
        public FilteredElementCollector CurrentCollector { get; private set; }

        /// <summary>
        /// РОЛЬ: Здесь хранятся Id итоговых результатов найденых и отфильтрованых Элементов.
        /// </summary>
        public List<ElementId> LastFoundIds { get; private set; } = new List<ElementId>();

        /// <summary>
        /// Variables - место для хранения предварительно запомненных ИИ данных со временным именем, например '$f1'. 
        /// </summary>
        public Dictionary<string, ElementId> Variables { get; private set; } = new Dictionary<string, ElementId>();

        public void Store(FilteredElementCollector collector)
        {
            CurrentCollector = collector;
        }
        public void Store(string assignAiName, ElementId elementId)
        {
            if (!string.IsNullOrEmpty(assignAiName))
                Variables[assignAiName] = elementId;
        }

        public void Store(IEnumerable<ElementId> lastFoundIds)
        {
            if(lastFoundIds != null && lastFoundIds.Count() > 0)
            {
                LastFoundIds.Clear(); 
                
                LastFoundIds.AddRange(lastFoundIds);

                CurrentCollector = null;
            }
        }

        public void Reset()
        {
            CurrentCollector = null;
            LastFoundIds.Clear();
            Variables.Clear();
        }
    }
}
