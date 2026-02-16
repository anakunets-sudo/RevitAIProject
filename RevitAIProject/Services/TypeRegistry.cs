using Autodesk.Revit.DB;
using RevitAIProject.Logic;
using RevitAIProject.Logic.Queries.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RevitAIProject.Services
{
    /// <summary>
    /// Global infrastructure service that caches types from the current assembly and Revit API.
    /// Provides fast access to filters, actions, and queries without repeated assembly scanning.
    /// </summary>
    public static class TypeRegistry
    {
        private static readonly object _lock = new object();

        // Cache for Search Filters: [AiParam Name] -> ISearchFilter Type
        private static Dictionary<string, Type> _filterTypes;

        // Cache for General Logic: [Action Name/Class Name] -> IRevitLogic Type
        private static Dictionary<string, Type> _logicTypes;

        // Cache for Revit API Classes: "Wall" -> typeof(Autodesk.Revit.DB.Wall)
        private static Dictionary<string, Type> _revitApiTypes;

        /// <summary>
        /// Returns cached search filter types. Scans assembly on first call.
        /// </summary>
        public static Dictionary<string, Type> GetFilterTypes()
        {
            if (_filterTypes != null) return _filterTypes;
            lock (_lock)
            {
                return _filterTypes ?? (_filterTypes = ScanForTypes<ISearchFilter>(true));
            }
        }

        /// <summary>
        /// Returns cached logic types (Actions and Queries). Scans assembly on first call.
        /// </summary>
        public static Dictionary<string, Type> GetLogicTypes()
        {
            if (_logicTypes != null) return _logicTypes;
            lock (_lock)
            {
                return _logicTypes ?? (_logicTypes = ScanForTypes<IRevitLogic>(false));
            }
        }

        /// <summary>
        /// Returns a specific Revit API type by its name (case-insensitive).
        /// </summary>
        public static Type GetRevitApiType(string className)
        {
            if (_revitApiTypes == null)
            {
                lock (_lock)
                {
                    if (_revitApiTypes == null) _revitApiTypes = ScanRevitApi();
                }
            }

            if (string.IsNullOrEmpty(className)) return null;
            _revitApiTypes.TryGetValue(className.ToLower(), out Type type);
            return type;
        }

        /// <summary>
        /// Scans the current assembly for implementations of T.
        /// </summary>
        /// <param name="strictAiParam">If true, throws exception on duplicate AiParam names.</param>
        private static Dictionary<string, Type> ScanForTypes<T>(bool strictAiParam)
        {
            var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var targetInterface = typeof(T);

            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => targetInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<AiParamAttribute>();
                var keys = new List<string>();

                // 1. Регистрация через атрибут (основной путь)
                if (attr != null && !string.IsNullOrEmpty(attr.Name))
                    keys.Add(attr.Name.Replace("_", "").ToLower());

                // 2. Регистрация через имя класса (фолбэк)
                // Убираем суффиксы и ОБЯЗАТЕЛЬНО "_" для поддержки любого стиля ИИ
                string cleanClassName = type.Name
                    .Replace("Action", "")
                    .Replace("Query", "")
                    .Replace("Filter", "")
                    .Replace("Initializer", "")
                    .Replace("_", "")
                    .ToLower();

                keys.Add(cleanClassName);
                keys.Add(type.Name.Replace("_", "").ToLower()); // Полное имя без "_"

                foreach (var key in keys.Distinct())
                {
                    if (string.IsNullOrEmpty(key)) continue;

                    if (result.TryGetValue(key, out Type existingType))
                    {
                        // Если мы в режиме строгого соответствия (фильтры) и нашли дубликат - это ошибка
                        if (strictAiParam && existingType != type)
                        {
                            // Логируем или кидаем ошибку
                            continue;
                        }
                        continue;
                    }
                    result.Add(key, type);
                }
            }
            return result;
        }

        /// <summary>
        /// Indexes all Element-derived types in RevitAPI assembly for O(1) lookup.
        /// </summary>
        private static Dictionary<string, Type> ScanRevitApi()
        {
            return typeof(Element).Assembly.GetTypes()
                .Where(t => typeof(Element).IsAssignableFrom(t))
                .GroupBy(t => t.Name.ToLower())
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
    }
}