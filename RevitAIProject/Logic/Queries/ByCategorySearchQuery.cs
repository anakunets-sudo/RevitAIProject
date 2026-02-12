using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByCategorySearch", Description = "Filters by BuiltInCategory.")]
    public class ByCategorySearchQuery : BaseRevitQuery
    {
        [AiParam("categoryName", Description = "Revit BuiltInCategory name (e.g. OST_Walls)")]
        public string CategoryName { get; set; }
        protected override void Execute(IRevitContext context)
        {
            Debug.WriteLine($"'{CategoryName}'");

            Debug.WriteLine($"Execute", this.GetType().Name);

            var bic = ResolveCategory(CategoryName);

            Debug.WriteLine($"{bic}", this.GetType().Name);

            if(context.Storage.CollectorValue(SearchAiName, out var collector))
            {
                collector = collector.OfCategory(bic);

                context.Storage.Store(SearchAiName, collector);

                var ids = collector.ToElementIds();

                RegisterSearched(context, ids);

                string label = !string.IsNullOrEmpty(CategoryName) ? $" ({CategoryName})" : "";

                Report($"Items found: {ids.Count}{label}.", RevitMessageType.AiReport);

                Debug.WriteLine($"{collector.Count()}", this.GetType().Name);
            }
            else
            {
                Debug.WriteLine($"Collector '{SearchAiName}' not found", this.GetType().Name);
            }
        }
    }
}
