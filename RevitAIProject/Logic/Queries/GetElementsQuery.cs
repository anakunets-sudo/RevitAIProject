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
    public class GetElementsQuery : BaseRevitQuery
    {
        [AiParam("GetElements", Description = "Universal search. Use ClassName, CategoryId, or FilterJson for parameters.")]
        public override string Name => "GetElements";

        [AiParam("targetClass", Description = "Revit class name: Wall, Floor, FamilyInstance, etc.")]
        public string ClassName { get; set; }

        // 2. Поиск по ID категории (надежнее, чем строка)
        [AiParam("categoryId", Description = "The long value of the BuiltInCategory ID (e.g., -2000011 for Walls)")]
        public long CategoryId { get; set; }

        // ВОТ ОН - Универсальный вход для сотен фильтров
        [AiParam("filterJson", Description = "JSON rules for parameters: [{'param':'Mark', 'op':'Equals', 'val':'A101'}]")]
        public string FilterJson { get; set; }

        protected override void Execute(IRevitContext context)
        {
            Debug.WriteLine($"ClassName - {ClassName}, CategoryId - {CategoryId}, FilterJson - {FilterJson}\n", "GetElementsQuery Data");

            var doc = context.UIDoc.Document;
            var collector = new FilteredElementCollector(doc);

            // 1. Быстрый фильтр по классу
            if (!string.IsNullOrEmpty(ClassName))
            {
                var type = ResolveRevitType(ClassName);

                if (type != null) collector = collector.OfClass(type);

                var d = collector == null ? "null" : collector.Count().ToString();

                Debug.WriteLine($"ClassName - {d}\n", "collector ClassName");
            }

            // 2. Быстрый фильтр по категории
            if (CategoryId != 0)
            {
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id).OfCategory((BuiltInCategory)(int)CategoryId);//в int только для Ревит ниже 25

                Debug.WriteLine($"CategoryId - {collector.ToElementIds().Count().ToString()}\n", "collector CategoryId");
            }

            // 3. Умный фильтр по параметрам
            if (!string.IsNullOrEmpty(FilterJson))
            {
                // Приводим CategoryId к BuiltInCategory для билдера
                BuiltInCategory bic = (BuiltInCategory)CategoryId;
                ElementFilter dynamicFilter = FilterJsonBuilder.Build(doc, FilterJson, bic);
                if (dynamicFilter != null) collector = new FilteredElementCollector(doc).WherePasses(dynamicFilter);

                Debug.WriteLine($"FilterJson - {collector.Count().ToString()}\n", "collector FilterJson");
            }

            context.SessionContext.Store(collector.ToElementIds());

            //MessageBox.Show(context.SessionContext.LastFoundIds.Count.ToString());

            Debug.WriteLine($"context.SessionContext - {context.SessionContext.LastFoundIds.Count}\n", "context.SessionContext");

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
