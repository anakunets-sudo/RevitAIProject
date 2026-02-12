using RevitAIProject.Logic.Queries;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    [AiParam("ByClassSearch", Description = "Filters by Class: Wall, Floor, FamilyInstance, etc.")]
    public class ByClassSearchQuery : BaseRevitQuery
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
                    if (context.Storage.CollectorValue(SearchAiName, out var collector))
                    {
                        collector = collector.OfClass(type);

                        context.Storage.Store(SearchAiName, collector);

                        var ids = collector.ToElementIds();

                        RegisterSearched(context, ids);

                        string label = !string.IsNullOrEmpty(ClassName) ? $" ({ClassName})" : "";

                        Report($"Items found: {ids.Count}{label}.", RevitMessageType.AiReport);

                        Debug.WriteLine($"{collector.Count()}", this.GetType().Name);
                    }
                    else
                    {
                        Debug.WriteLine($"Collector '{SearchAiName}' not found", this.GetType().Name);
                    }
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
