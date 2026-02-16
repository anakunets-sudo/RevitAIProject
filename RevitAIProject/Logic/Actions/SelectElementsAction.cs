using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RevitAIProject.Logic.Actions
{
    [AiParam("SelectElements", Description = "Selects elements in Revit UI using a saved search name (e.g. $q1).")]
    public class SelectElementsAction : BaseRevitAction
    {
        protected override void Execute(IRevitContext context)
        {
            List<ElementId> idsToSelect = null;

            // Проверяем TargetAiName
            if (!string.IsNullOrEmpty(TargetAiName))
            {
                context.Storage.StorageValue(TargetAiName, out idsToSelect);

                Report($"Elements {TargetAiName} have been selected", Services.RevitMessageType.AiReport);
            }

            // Если не нашли по TargetAiName, пробуем SearchAiName
            if (idsToSelect == null && !string.IsNullOrEmpty(AssignAiName))
            {
                context.Storage.StorageValue(AssignAiName, out idsToSelect);

                Report($"Elements {AssignAiName} have been selected", Services.RevitMessageType.AiReport);
            }

            if (idsToSelect != null && idsToSelect.Count > 0)
            {
                context.UIDoc.Selection.SetElementIds(idsToSelect);
                RegisterCreatedElements(idsToSelect);                
            }
            else
            {
                // Логируем, какой именно ключ не был найден или был null
                string missingKey = TargetAiName ?? AssignAiName ?? "NULL_KEY";
                Debug.WriteLine($"Key '{missingKey}' not found or is null!", "SelectElementsAction Error");
            }
        }
    }
}
