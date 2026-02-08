using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject
{
    [Transaction(TransactionMode.Manual)]
    public class ShowAiPane : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            // Принудительно обновляем ссылку на UiApp при открытии панели
            //App.UiApp = data.Application;

            // Передаем документ в Handler, чтобы он был доступен сразу
            //App.RevitProxy.ActiveDoc = data.Application.ActiveUIDocument.Document;

            var pane = data.Application.GetDockablePane(Views.ChatView.PaneId);
            pane.Show();

            return Result.Succeeded;
        }
    }
}
