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
        public abstract class BaseRevitQuery : IRevitQuery
        {
            [AiParam("", Description = "Internal name of the query.")]
            public virtual string Name => GetType().Name.Replace("Query", "");
            //public virtual string GetQueryResultSummary() => $"Found {SessionContext.LastFoundIds.Count} elements.";

            // ВНЕШНИЙ МЕТОД (вызывает фабрика/контроллер)
            public void Execute(IRevitApiService apiService)
            {
                // Просто регистрируем лямбду. Наследник об этом даже не знает.
                apiService.AddToQueue(context => Execute(context));
            }

            // ВНУТРЕННИЙ МЕТОД (реализует программист в MoveAction и т.д.)
            // Здесь НЕТ доступа к apiService, только к контексту выполнения
            protected abstract void Execute(IRevitContext context);
        }
    }
}
