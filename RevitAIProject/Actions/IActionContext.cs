using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Actions
{
    public interface IActionContext
    {
        UIApplication UIApp { get; }
        UIDocument UIDoc { get; }
        // Только чтение и запись переменных, без методов управления очередью
        Dictionary<string, ElementId> Variables { get; }

        void Report(string message, RevitMessageType messageType);
    }
}
