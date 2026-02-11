using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries.RevitAIProject.Logic.Queries;
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
            context.SessionContext.Store(new FilteredElementCollector(context.UIDoc.Document));
        }
    }
}
