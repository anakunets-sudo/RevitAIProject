using Autodesk.Revit.DB;
using RevitAIProject.Logic.Actions;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    namespace RevitAIProject.Logic.Queries
    {
        [AiParam("", Description = "Internal name of the query.")]
        public abstract class BaseRevitQuery : IRevitQuery
        {
            [AiParam("search_ai_name", Description = "Give your completed search a name (e.g. search_walls) to use later")]
            public string SearchAiName { get; set; }

            [AiParam("categoryName", Description = "Revit BuiltInCategory name (e.g. OST_Walls)")]
            public string CategoryName { get; set; }
            /*protected void RegisterFoundedElements(IRevitContext context, IEnumerable<ElementId> foundIds)
            {
                string key = !string.IsNullOrEmpty(SearchAiName) ? SearchAiName : $"$q_{Guid.NewGuid().ToString().Substring(0, 4)}";

                context.SessionContext.Store(key, foundIds);
            }*/
            protected BuiltInCategory ResolveCategory()
            {
                if (string.IsNullOrEmpty(CategoryName)) return BuiltInCategory.INVALID;

                // 1. Прямой парсинг (OST_Walls)
                if (Enum.TryParse(CategoryName, true, out BuiltInCategory bic)) return bic;

                // 2. Префикс OST_ (Walls -> OST_Walls)
                string fuzzyName = CategoryName.StartsWith("OST_") ? CategoryName : "OST_" + CategoryName;
                if (Enum.TryParse(fuzzyName, true, out BuiltInCategory bicFuzzy)) return bicFuzzy;

                // 3. Обработка множественного числа (Wall -> OST_Walls)
                string pluralName = fuzzyName.EndsWith("s") ? fuzzyName : fuzzyName + "s";
                if (Enum.TryParse(pluralName, true, out BuiltInCategory bicPlural)) return bicPlural;

                return BuiltInCategory.INVALID;
            }

            // ВНЕШНИЙ МЕТОД (вызывает фабрика/контроллер)
            public void Execute(IRevitApiService apiService)
            {
                // Просто регистрируем лямбду. Наследник об этом даже не знает.
                apiService.AddToQueue(context => Execute(context));

                // Авто-сохранение состояния CurrentCollector после выполнения наследника
                if (apiService.SessionContext.CurrentCollector != null)
                {
                    var foundIds = apiService.SessionContext.CurrentCollector.ToElementIds();

                    string key = !string.IsNullOrEmpty(SearchAiName) ? SearchAiName : $"$q_{Guid.NewGuid().ToString().Substring(0, 4)}";

                    apiService.SessionContext.Store(key, foundIds);
                }
            }

            // ВНУТРЕННИЙ МЕТОД (реализует программист в MoveAction и т.д.)
            // Здесь НЕТ доступа к apiService, только к контексту выполнения
            protected abstract void Execute(IRevitContext context);
        }
    }
}
