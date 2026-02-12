using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByLevelSearch", Description = "Fast filter elements by their Level ID.")]
    public class ByLevelSearchQuery : BaseRevitQuery
    {
        [AiParam("levelId", Description = "The ElementId of the Level.")]
        public string LevelIdString { get; set; }

        protected override void Execute(IRevitContext context)
        {
            if (int.TryParse(LevelIdString, out int idInt))
            {
                if (context.Storage.CollectorValue(SearchAiName, out var collector))
                {
                    var filter = new ElementLevelFilter(new ElementId(idInt));

                    collector = collector.WherePasses(filter);

                    context.Storage.Store(SearchAiName, collector);

                    var ids = collector.ToElementIds();

                    RegisterSearched(context, ids);

                    string label = !string.IsNullOrEmpty(LevelIdString) ? $" ({LevelIdString})" : "";

                    Report($"Items found: {ids.Count}{label}.", RevitMessageType.AiReport);

                    Debug.WriteLine($"{collector.Count()}", this.GetType().Name);
                }
                else
                {
                    Debug.WriteLine($"Collector '{SearchAiName}' not found", this.GetType().Name);
                }
            }
            else
            {
                Debug.WriteLine($"Level '{LevelIdString}' not found", this.GetType().Name);
            }
        }
    }
}
