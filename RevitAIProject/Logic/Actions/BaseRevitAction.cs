using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Actions
{
    [AiParam("", Description = "The unique name of the command to execute.")]
    public abstract class BaseRevitAction : IRevitAction
    {
        internal string TransactionName => "AI: " + this.GetType().GetCustomAttribute<Logic.AiParamAttribute>()?.Name;

        [AiParam("target_ai_name", Description = "The name of the element from the previous commands (e.g. $f1) or leave blank for selected objects")]
        public string TargetAiName { get; set; }

        [AiParam("assign_ai_name", Description = "Give the created object a name (e.g. $f1) to use it later")]
        public string AssignAiName { get; set; }

        [AiParam("search_ai_name", Description = "The name of the search result to select (e.g. '$q1')")]
        public string SearchAiName { get; set; }

        // Универсальный метод поиска целей для ВСЕХ наследников
        protected List<ElementId> ResolveTargets(IRevitContext context)
        {
            // 1. Пытаемся найти ключ ($q1, $f1 и т.д.) в едином хранилище
            if (!string.IsNullOrEmpty(TargetAiName) &&
                context.Storage.StorageValue(TargetAiName, out List<ElementId> storedIds))
            {
                return storedIds; // Возвращаем список (хоть 1, хоть 1000 элементов)
            }

            // 2. Если это просто числовой ID (ручной ввод)
            if (int.TryParse(TargetAiName, out int idInt))
            {
                return new List<ElementId> { new ElementId(idInt) };
            }

            // 3. Если ничего не указано — берем текущее выделение в Revit
            var selection = context.UIDoc.Selection.GetElementIds();
            return selection.ToList();
        }

        protected void RegisterCreatedElement(IRevitContext context, IEnumerable<ElementId> newIds)
        {
            if (!string.IsNullOrEmpty(AssignAiName) && AssignAiName.StartsWith("$f") && (newIds != null && newIds.Count() > 0))
            {
                context.Storage.Store(AssignAiName, newIds );
            }
        }

        // ВНЕШНИЙ МЕТОД (вызывает фабрика/контроллер)
        public void Execute(IRevitApiService apiService)
        {
            // Просто регистрируем лямбду. Наследник об этом даже не знает.
            apiService.AddToQueue(context =>
            {
                try
                {
                    Execute(context);

                    foreach(var report in _reports)
                    {
                        apiService.Report(report.Value, report.Key);

                        Debug.WriteLine($"{report.Value}\n", this.GetType().Name);
                    }

                }
                catch (Exception ex)
                {
                    apiService.Report($"{ex.Message}", RevitMessageType.Error);

                    Debug.WriteLine($"Error: {ex.Message}\n", this.GetType().Name);
                }
            });
        }

        private SortedList<RevitMessageType, string> _reports = new SortedList<RevitMessageType, string>();

        protected void Report(string message, RevitMessageType type)
        {
            // Если это НЕ отчет для ИИ — просто пробрасываем текст как есть
            if (type != RevitMessageType.AiReport)
            {
                _reports.Add(type, message);
            }
            else
            {
                // Формируем "умный" рапорт для ИИ
                string className = this.GetType().GetCustomAttribute<Logic.AiParamAttribute>()?.Name;
                var labels = new System.Collections.Generic.List<string>();

                // Собираем ВСЕ заполненные метки
                if (!string.IsNullOrEmpty(TargetAiName)) labels.Add($"Target: '{TargetAiName}'");
                if (!string.IsNullOrEmpty(SearchAiName)) labels.Add($"Search: '{SearchAiName}'");
                if (!string.IsNullOrEmpty(AssignAiName)) labels.Add($"Assign: '{AssignAiName}'");

                // Склеиваем метки через запятую, если они есть
                string labelInfo = labels.Count > 0
                    ? $" ({string.Join(", ", labels)})"
                    : string.Empty;

                string fullReport = $"[{className}]{labelInfo}: {message}";

                _reports.Add(type, fullReport);
            }
        }

        // ВНУТРЕННИЙ МЕТОД (реализует программист в MoveAction и т.д.)
        // Здесь НЕТ доступа к apiService, только к контексту выполнения
        protected abstract void Execute(IRevitContext context);
    }
}
