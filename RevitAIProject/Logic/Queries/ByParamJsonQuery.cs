using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries.RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByParamJsonQuery", Description = "Complex filter using JSON rules for parameters.")]
    public class ByParamJsonQuery : BaseRevitQuery
    {
        [AiParam("filterJson", Description = "JSON: [{'param':'Mark', 'op':'Equals', 'val':'A101'}]")]
        public string FilterJson { get; set; }

        protected override void Execute(IRevitContext context)
        {
            if (string.IsNullOrEmpty(FilterJson)) return;

            var bic = ResolveCategory(); // Берем из базового класса, если ИИ указал категорию

            if (bic != BuiltInCategory.INVALID)
            {
                // Используем твой FilterJsonBuilder с Гитхаба
                ElementFilter dynamicFilter = FilterJsonBuilder.Build(context.UIDoc.Document, FilterJson, bic);

                if (dynamicFilter != null)
                {
                    var collector = context.SessionContext.CurrentCollector.WherePasses(dynamicFilter);

                    context.SessionContext.Store(collector);

                }
            }
        }
    }
}
