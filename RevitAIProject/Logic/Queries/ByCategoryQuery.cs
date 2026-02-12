using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByCategory", Description = "Filters by BuiltInCategory.")]
    public class ByCategoryQuery : BaseRevitQuery
    {
        protected override void Execute(IRevitContext context)
        {
            Debug.WriteLine($"'{CategoryName}'");

            Debug.WriteLine($"Execute", this.GetType().Name);

            var bic = ResolveCategory();

            Debug.WriteLine($"{bic}", this.GetType().Name);

            var collector = context.Storage.CurrentCollector.OfCategory(bic);

            Debug.WriteLine($"{collector.Count()}", this.GetType().Name);

            context.Storage.Store(collector);

            ReportAndRegisterSearched(context, collector.ToElementIds());
        }
    }
}
