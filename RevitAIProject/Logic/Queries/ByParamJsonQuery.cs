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
    [AiParam("ByParamJson", Description = "Complex filter using JSON rules for parameters.")]
    public class ByParamJsonQuery : BaseSearchQuery
    {
        [AiParam("categoryName", Description = "Revit BuiltInCategory name (e.g. OST_Walls)")]
        public string CategoryName { get; set; }

        [AiParam("filterJson", Description = "JSON: [{'param':'Mark', 'op':'Equals', 'val':'A101'}]")]
        public string FilterJson { get; set; }

        protected override void Execute(IRevitContext context)
        {
            if (string.IsNullOrEmpty(FilterJson)) return;

            var bic = ResolveCategory(CategoryName); // Берем из базового класса, если ИИ указал категорию

            if (bic != BuiltInCategory.INVALID)
            {
                if (context.Storage.CollectorValue(SearchAiName, out var collector))
                {
                    ElementFilter dynamicFilter = FilterJsonBuilder.Build(context.UIDoc.Document, FilterJson, bic);

                    if (dynamicFilter != null)
                    {
                        collector = collector.WherePasses(dynamicFilter);

                        context.Storage.Store(SearchAiName, collector);

                        var ids = collector.ToElementIds();

                        RegisterSearched(context, ids);

                        string label = !string.IsNullOrEmpty(FilterJson) ? $" ({FilterJson})" : "";

                        Report($"Items found: {ids.Count}{label}.", RevitMessageType.AiReport);

                        Debug.WriteLine($"{collector.Count()}", this.GetType().Name);
                    }                    
                }
                else
                {
                    Debug.WriteLine($"Collector '{SearchAiName}' not found", this.GetType().Name);
                }

                // Используем твой FilterJsonBuilder с Гитхаба
                
            }
        }
    }
}
