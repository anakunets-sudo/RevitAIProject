using Newtonsoft.Json.Linq;
using RevitAIProject.Logic.Queries.Filters;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json.Linq;
    using RevitAIProject.Services; // Убедись, что TypeRegistry здесь

    /// <summary>
    /// Factory that constructs and configures search filter chains from AI-provided JSON data.
    /// Uses a centralized TypeRegistry for fast filter discovery and reflection for property mapping.
    /// </summary>
    public class AiSearchFactory
    {
        /// <summary>
        /// Transforms JArray into a sorted sequence of Revit filters using the TypeRegistry cache.
        /// </summary>
        /// <param name="instructions">JSON array containing filter definitions (Kind, Value, Extra).</param>
        /// <returns>A list of configured ISearchFilter objects sorted by execution priority.</returns>
        public List<ISearchFilter> CreateLogic(JArray instructions)
        {
            var train = new List<ISearchFilter>();
            if (instructions == null) return train;

            // 1. Получаем кэш типов фильтров из централизованного реестра
            var cachedFilterTypes = TypeRegistry.GetFilterTypes();

            foreach (var token in instructions)
            {
                if (!(token is JObject filterObj)) continue;

                // 2. ГИБКОСТЬ: Ищем "Kind" или "kind"
                string rawKind = (filterObj["Kind"] ?? filterObj["kind"])?.ToString();
                if (string.IsNullOrEmpty(rawKind)) continue;

                // 3. НОРМАЛИЗАЦИЯ: "active_view" -> "activeview", "Category" -> "category"
                string cleanKind = rawKind.Replace("_", "").ToLower();

                // 4. Мгновенный поиск в реестре
                if (cachedFilterTypes.TryGetValue(cleanKind, out Type filterType))
                {
                    // Создаем экземпляр конкретного класса фильтра
                    var filter = (ISearchFilter)Activator.CreateInstance(filterType);

                    // 5. МАППИНГ: Наполняем фильтр данными через [AiParam]
                    // Передаем весь объект фильтра (включая Value), чтобы MapJsonToFilterProperties его разобрал
                    MapJsonToFilterProperties(filter, filterObj);

                    train.Add(filter);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AiSearchFactory]: Unknown filter kind '{rawKind}' (cleaned: '{cleanKind}') skipped.");
                }
            }

            // 6. СОРТИРОВКА: Возвращаем "поезд" фильтров, отсортированный по Priority
            // Initializers (0) -> Fast (1-5) -> Slow (10+)
            return train.OrderBy(f => f.Priority).ToList();
        }

        /// <summary>
        /// Fills filter properties based on JToken data by matching [AiParam] attributes.
        /// Supports automatic unit conversion to Revit internal feet for double values.
        /// </summary>
        /// <param name="filter">The filter instance to configure.</param>
        /// <param name="data">The JSON data source for this filter.</param>
        private static void MapJsonToFilterProperties(ISearchFilter filter, JToken data)
        {
            if (data == null || filter == null) return;

            // 1. Получаем все публичные свойства фильтра
            PropertyInfo[] properties = filter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                var attr = prop.GetCustomAttribute<AiParamAttribute>();
                if (attr == null) continue;

                // 2. ОПРЕДЕЛЯЕМ КЛЮЧ: Приоритет имени из атрибута (target_ai_name и т.д.)
                string targetKey = attr.Name ?? prop.Name;

                // 3. УМНЫЙ ПОИСК ЗНАЧЕНИЯ:
                // Ищем по конкретному ключу ИЛИ в общем поле Value/extra (игнорируя регистр)
                JToken jsonValue = null;
                if (data is JObject obj)
                {
                    jsonValue = obj.GetValue(targetKey, StringComparison.OrdinalIgnoreCase)
                             ?? obj.GetValue("Value", StringComparison.OrdinalIgnoreCase)
                             ?? obj.GetValue("value", StringComparison.OrdinalIgnoreCase);
                }
                else if (data is JValue) // Если прислали просто строку вместо объекта
                {
                    jsonValue = data;
                }

                if (jsonValue == null || jsonValue.Type == JTokenType.Null) continue;

                try
                {
                    string stringValue = jsonValue.ToString().Trim();
                    if (string.IsNullOrEmpty(stringValue)) continue;

                    // 4. ТИПИЗИРОВАННАЯ КОНВЕРТАЦИЯ
                    if (prop.PropertyType == typeof(double))
                    {
                        // Используем твой централизованный парсер единиц (мм, см, ft)
                        prop.SetValue(filter, LogicFactory.ParseToRevitFeet(stringValue));
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        prop.SetValue(filter, stringValue);
                    }
                    else if (prop.PropertyType == typeof(bool))
                    {
                        // Безопасный парсинг bool
                        if (bool.TryParse(stringValue.ToLower(), out bool boolVal))
                            prop.SetValue(filter, boolVal);
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        // Поддержка Enum (например, OST_ категории или типы сравнения)
                        object enumVal = Enum.Parse(prop.PropertyType, stringValue, true);
                        prop.SetValue(filter, enumVal);
                    }
                    else
                    {
                        // Универсальный фолбэк для остальных типов (int, long и т.д.)
                        prop.SetValue(filter, jsonValue.ToObject(prop.PropertyType));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AiSearchFactory Error]: Mapping {prop.Name} in {filter.GetType().Name} failed. {ex.Message}");
                }
            }
        }
    }
}
