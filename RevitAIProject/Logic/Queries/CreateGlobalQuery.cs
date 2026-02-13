using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("CreateGlobal", Description = "ONLY creates the scope for the entire project for a new search. Does not support filtering.")]
    public class CreateGlobalQuery : BaseSearchQuery
    {
        protected override void Execute(IRevitContext context)
        {
            var collector = new FilteredElementCollector(context.UIDoc.Document);

            string key = !string.IsNullOrEmpty(SearchAiName) ? SearchAiName : "$q_current";

            context.Storage.Store(key, collector);

            Report($"The collector {key} for collecting in the ENTIRE project was created.", Services.RevitMessageType.AiReport);
        }
    }
}
