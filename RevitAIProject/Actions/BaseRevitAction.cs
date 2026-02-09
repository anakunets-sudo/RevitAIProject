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
    public abstract class BaseRevitAction : IRevitAction
    {
        [AiParam("", Description = "The unique name of the command to execute.")]
        public virtual string ActionName => GetType().Name.Replace("Action", "");
        internal string TransactionName => "AI: " + ActionName;

        [AiParam("target_ai_name", Description = "The name of the element from the previous commands (e.g. $f1) or leave blank for selected objects")]
        public string TargetAiName { get; set; }

        [AiParam("assign_ai_name", Description = "Give the created object a name (e.g. $f1) to use it later")]
        public string AssignAiName { get; set; }

        // Универсальный метод поиска целей для ВСЕХ наследников
        protected List<ElementId> ResolveTargets(IActionContext context)
        {
            var ids = new List<ElementId>();

            // 1. Поиск по переменной $id
            if (!string.IsNullOrEmpty(TargetAiName) && TargetAiName.StartsWith("$"))
            {
                if (context.Variables.TryGetValue(TargetAiName, out var storedId))
                    ids.Add(storedId);
            }
            // 2. Поиск по числовому ID
            else if (int.TryParse(TargetAiName, out int idInt))
            {
                ids.Add(new ElementId(idInt));
            }
            // 3. Если пусто или "selected" — берем выделение в Revit
            else
            {
                var selection = context.UIDoc.Selection.GetElementIds();
                if (selection.Any()) ids.AddRange(selection);
            }

            return ids;
        }

        // Регистрация: связываем виртуальное "имя ИИ" с реальным ID Revit
        protected void RegisterCreatedElement(IRevitApiService apiService, ElementId newId)
        {
            if (!string.IsNullOrEmpty(AssignAiName) && AssignAiName.StartsWith("$") && newId != ElementId.InvalidElementId)
            {
                apiService.Variables[AssignAiName] = newId;
            }
        }

        // ВНЕШНИЙ МЕТОД (вызывает фабрика/контроллер)
        public void Execute(IRevitApiService apiService)
        {
            // Просто регистрируем лямбду. Наследник об этом даже не знает.
            apiService.AddToQueue(context => Execute(context));
        }

        // ВНУТРЕННИЙ МЕТОД (реализует программист в MoveAction и т.д.)
        // Здесь НЕТ доступа к apiService, только к контексту выполнения
        protected abstract void Execute(IActionContext context);


    }
}
