using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("CreateActiveView", Description = "ONLY creates the scope by active view for a new search. Does not support filtering.")]
    public class CreateActiveViewQuery : BaseSearchQuery
    {
        protected override void Execute(IRevitContext context)
        {
            var collector = new FilteredElementCollector(context.UIDoc.Document, context.UIDoc.Document.ActiveView.Id);

            string key = !string.IsNullOrEmpty(SearchAiName) ? SearchAiName : "$q_current";

            context.Storage.Store(key, collector);

            Report($"This collector {key} for collecting on ACTIVE VIEW was created.", Services.RevitMessageType.AiReport);
        }
    }
}
