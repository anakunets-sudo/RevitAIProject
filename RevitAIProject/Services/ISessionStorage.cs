using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface ISessionStorage
    {
        IReadOnlyDictionary<string, List<ElementId>> Storage {  get; }
        void Store(string key, FilteredElementCollector collector);
        void Store(string key, IEnumerable<ElementId> foundIds);
        bool StorageValue(string key, out List<ElementId> foundIds);
        bool CollectorValue(string key, out FilteredElementCollector collector);
    }
}
