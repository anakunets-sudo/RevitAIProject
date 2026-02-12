using Autodesk.Revit.DB;
using RevitAIProject.Logic;
using RevitAIProject.Logic.Actions;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("", Description = "Internal name of the query.")]
    public abstract class BaseRevitQuery : IRevitQuery
    {
        [AiParam("search_ai_name", Description = "Give your completed search a name (e.g. search_walls) to use later")]
        public string SearchAiName { get; set; }

        protected BuiltInCategory ResolveCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return BuiltInCategory.INVALID;

            // 1. Максимальная очистка строки
            string target = categoryName.Trim().Replace("\"", "").Replace("'", "");

            // 2. Список вариантов для проверки (OST_Windows, Windows, Window)
            var variants = new System.Collections.Generic.List<string> { target };
            if (!target.StartsWith("OST_")) variants.Add("OST_" + target);
            if (!target.EndsWith("s")) variants.Add(target + "s");
            if (!target.StartsWith("OST_") && !target.EndsWith("s")) variants.Add("OST_" + target + "s");

            // 3. Прямой перебор всех имен BuiltInCategory
            // В Revit 2019 это гарантированно найдет совпадение без учета регистра
            var allNames = Enum.GetNames(typeof(BuiltInCategory));

            foreach (var variant in variants)
            {
                foreach (var bInCategoryName in allNames)
                {
                    if (string.Equals(bInCategoryName, variant, StringComparison.OrdinalIgnoreCase))
                    {
                        return (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), bInCategoryName);
                    }
                }
            }

            // 4. Если ничего не нашли - пишем в лог то, что пришло РЕАЛЬНО
            Debug.WriteLine($"[CRITICAL] Category NOT FOUND: '{target}' (Length: {target.Length})");

            return BuiltInCategory.INVALID;
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
                        apiService.Report($"{this.GetHashCode()}{report.Value}", report.Key);

                        Debug.WriteLine(report.Value + "\n", this.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    apiService.Report($"{ex.Message}", RevitMessageType.Error);

                    Debug.WriteLine($"Error: {ex.Message}\n", this.GetType().Name);
                }
            });
        }

        protected void RegisterSearched(IRevitContext context, IEnumerable<ElementId> newIds)
        {
            if (newIds != null)
            {
                string key = !string.IsNullOrEmpty(SearchAiName) ? SearchAiName : $"$q_{Guid.NewGuid().ToString().Substring(0, 4)}";

                context.Storage.Store(key, newIds);  

                Debug.WriteLine($"Items found: {newIds.Count()}", this.GetType().Name);
            }
        }

        private SortedList<RevitMessageType, string> _reports = new SortedList<RevitMessageType, string>();

        protected void Report(string message, RevitMessageType type)
        {
            _reports.Add(type, message);
        }

        // ВНУТРЕННИЙ МЕТОД (реализует программист в MoveAction и т.д.)
        // Здесь НЕТ доступа к apiService, только к контексту выполнения
        protected abstract void Execute(IRevitContext context);
    }
}
