using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries.RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("InitViewQuery", Description = "Starts searching only in the active view.")]
    public class InitActiveViewQuery : BaseRevitQuery
    {
        protected override void Execute(IRevitContext context)
        {
            context.SessionContext.Store(new FilteredElementCollector(context.UIDoc.Document, context.UIDoc.Document.ActiveView.Id));
        }
    }
}
