using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("InitGlobalQuery", Description = "Starts searching in the entire document.")]
    public class InitGlobalQuery : BaseRevitQuery
    {
        protected override void Execute(IRevitContext context)
        {       
            context.Storage.Store(new FilteredElementCollector(context.UIDoc.Document));

            Report($"The collector for collecting in the ENTIRE project was created.", Services.RevitMessageType.AiReport);
        }
    }
}
