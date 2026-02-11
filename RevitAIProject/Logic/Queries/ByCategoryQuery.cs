using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries.RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByCategory", Description = "Filters by BuiltInCategory.")]
    internal class ByCategoryQuery : BaseRevitQuery
    {
        protected override void Execute(IRevitContext context)
        {
            var bic = ResolveCategory();

            var collector = context.SessionContext.CurrentCollector.OfCategory(bic);

            context.SessionContext.Store(collector);

            Debug.WriteLine($"ClassName - {context.SessionContext.CurrentCollector.GetElementCount()}\n", "collector ClassName");
        }
    }
}
