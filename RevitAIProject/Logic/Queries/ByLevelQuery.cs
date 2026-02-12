using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByLevelQuery", Description = "Fast filter elements by their Level ID.")]
    public class ByLevelQuery : BaseRevitQuery
    {
        [AiParam("levelId", Description = "The ElementId of the Level.")]
        public string LevelIdString { get; set; }

        protected override void Execute(IRevitContext context)
        {
            if (int.TryParse(LevelIdString, out int idInt))
            {
                var filter = new ElementLevelFilter(new ElementId(idInt));
                var collector = context.Storage.CurrentCollector.WherePasses(filter);
                context.Storage.Store(collector);

                ReportAndRegisterSearched(context, collector.ToElementIds());
            }
        }
    }
}
