using RevitAIProject.Logic.Queries.RevitAIProject.Logic.Queries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByClassQuery", Description = "Filters by Class: Wall, Floor, FamilyInstance, etc.")]
    public class ByClassQuery : BaseRevitQuery
    {
        [AiParam("targetClass", Description = "Revit class name: Wall, Floor, FamilyInstance, etc.")]
        public string ClassName { get; set; }
        protected override void Execute(IRevitContext context)
        {
            // 1. Быстрый фильтр по классу
            if (!string.IsNullOrEmpty(ClassName))
            {
                var type = ResolveRevitType(ClassName);

                if (type != null)
                {
                    var collector = context.SessionContext.CurrentCollector.OfClass(type);

                    context.SessionContext.Store(collector);

                    Debug.WriteLine($"ClassName - {context.SessionContext.CurrentCollector.GetElementCount()}\n", "collector ClassName");
                }
            }
        }
        private Type ResolveRevitType(string className)
        {
            if (string.IsNullOrEmpty(className)) return null;

            // Берем типы из уже загруженной RevitAPI.dll
            var revitApiAssembly = typeof(Autodesk.Revit.DB.Element).Assembly;

            // Ищем тип по имени (например, "Wall", "Floor", "FamilyInstance")
            return revitApiAssembly.GetTypes()
                .FirstOrDefault(t => t.Name.Equals(className, StringComparison.OrdinalIgnoreCase));
        }
    }
}
