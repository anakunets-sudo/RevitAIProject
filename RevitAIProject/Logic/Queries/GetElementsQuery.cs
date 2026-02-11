using Autodesk.Revit.DB;
using RevitAIProject.Logic.Queries.RevitAIProject.Logic.Queries;
using RevitAIProject.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RevitAIProject.Logic.Queries
{
    /*
    [AiParam("GetElements", Description = "Universal search. Use ClassName, CategoryId, or FilterJson for parameters.")]
    public class GetElementsQuery : BaseRevitQuery
    {
        [AiParam("targetClass", Description = "Revit class name: Wall, Floor, FamilyInstance, etc.")]
        public string ClassName { get; set; }

        // ВОТ ОН - Универсальный вход для сотен фильтров
        [AiParam("filterJson", Description = "JSON rules for parameters: [{'param':'Mark', 'op':'Equals', 'val':'A101'}]")]
        public string FilterJson { get; set; }

        protected override void Execute(IRevitContext context)
        {
            Debug.WriteLine($"ClassName - {ClassName}, CategoryName - {CategoryName}, FilterJson - {FilterJson}\n", "GetElementsQuery Data");

            var doc = context.UIDoc.Document;
            var collector = new FilteredElementCollector(doc, doc.ActiveView.Id);

            // 1. Быстрый фильтр по классу
            if (!string.IsNullOrEmpty(ClassName))
            {
                var type = ResolveRevitType(ClassName);

                if (type != null) collector = collector.OfClass(type);

                var d = collector == null ? "null" : collector.Count().ToString();

                Debug.WriteLine($"ClassName - {d}\n", "collector ClassName");
            }

            var bic = ResolveCategory();

            // 2. Быстрый фильтр по категории
            if (bic != BuiltInCategory.INVALID)
            {
                collector.OfCategory(bic);

                Debug.WriteLine($"Searching for category: {bic}", "GetElementsQuery");
            }

            // 3. Умный фильтр по параметрам
            if (!string.IsNullOrEmpty(FilterJson))
            {
                ElementFilter dynamicFilter = FilterJsonBuilder.Build(doc, FilterJson, bic);
                if (dynamicFilter != null) collector = new FilteredElementCollector(doc).WherePasses(dynamicFilter);

                Debug.WriteLine($"FilterJson - {collector.Count().ToString()}\n", "collector FilterJson");
            }

            RegisterFoundedElements(context, collector.ToElementIds());

            Debug.WriteLine($"{collector.ToElementIds().Count().ToString()}\n");

            Debug.WriteLine($"context.SessionContext - {context.SessionContext.Storage.Count}\n", "context.SessionContext");

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
    */
}
