using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public class SessionContext
    {
        // "Корзина" для ID элементов из последнего запроса
        public List<ElementId> LastFoundIds { get; set; } = new List<ElementId>();

        // Метаданные (имена параметров, GUID, Enum и т.д.)
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        public void UpdateElements(List<ElementId> ids)
        {
            LastFoundIds.Clear();
            if (ids != null) LastFoundIds.AddRange(ids);
        }

        public void Clear()
        {
            LastFoundIds.Clear();
            Metadata.Clear();
        }
    }
}
