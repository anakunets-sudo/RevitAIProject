using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Actions
{
    [AiParam("SelectExcept", Description = "Select objects in Revit UI except those that need to be excluded (e.g. '$q1' or '$q1').")]
    public class SelectExceptAction : BaseRevitAction
    {
        [AiParam("exclude_ai_name", Description = "The name of the memory object to be excluded (e.g. '$f1' or '$q1')")]
        public string ExcludeAiName { get; set; }

        [AiParam("search_ai_name", Description = "The name of the search result to select (e.g. '$q1')")]
        public string SearchAiName { get; set; }
        protected override void Execute(IRevitContext context)
        {
            // 1. Получаем ID объекта, который нужно исключить
            List<ElementId> idsToExclude = new List<ElementId>();

            if (!string.IsNullOrEmpty(ExcludeAiName))
            {
                context.SessionContext.Storage.TryGetValue(ExcludeAiName, out idsToExclude);
            }

            List<ElementId> idsToSelect = null;

            // Проверяем TargetAiName
            if (!string.IsNullOrEmpty(TargetAiName))
            {
                context.SessionContext.Storage.TryGetValue(TargetAiName, out idsToSelect);
            }

            // Если не нашли по TargetAiName, пробуем SearchAiName
            if (idsToSelect == null && !string.IsNullOrEmpty(SearchAiName))
            {
                context.SessionContext.Storage.TryGetValue(SearchAiName, out idsToSelect);
            }

            if (idsToSelect != null && idsToSelect.Count > 0)
            {
                idsToSelect = idsToSelect.Except(idsToExclude).ToList();

                context.UIDoc.Selection.SetElementIds(idsToSelect);
                RegisterCreatedElement(context, idsToSelect);
            }
            else
            {
                // Логируем, какой именно ключ не был найден или был null
                string missingKey = TargetAiName ?? SearchAiName ?? ExcludeAiName ?? "NULL_KEY";
                Debug.WriteLine($"Key '{missingKey}' not found or is null!", "SelectElementsAction Error");
            }
        }
    }
}
