using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("CreateGlobalQuery", Description = "ONLY creates the scope for the entire project for a new search. Does not support filtering.")]
    public class CreateGlobalQuery : BaseRevitQuery
    {
        protected override void Execute(IRevitContext context)
        {
            var collector = new FilteredElementCollector(context.UIDoc.Document);

            context.Storage.Store(SearchAiName, new FilteredElementCollector(context.UIDoc.Document));

            Report($"The collector for collecting in the ENTIRE project was created.", Services.RevitMessageType.AiReport);
        }
    }
}
