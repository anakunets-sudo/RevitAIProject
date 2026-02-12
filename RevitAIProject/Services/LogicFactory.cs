using Newtonsoft.Json.Linq;
using RevitAIProject.Logic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitAIProject.Services
{
    /// <summary>
    /// РОЛЬ: Автоматическое создание экземпляров логики и маппинг параметров из JSON в свойства C# с конвертацией единиц.
    /// </summary>
    public static class LogicFactory
    {
        public static IRevitLogic CreateLogic(string actionName, JToken data)
        {
            if (string.IsNullOrEmpty(actionName)) return null;

            // 1. Поиск типа по атрибуту [AiParam] или имени класса
            Type logicType = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(IRevitLogic).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .FirstOrDefault(t =>
                {
                    var attr = t.GetCustomAttribute<AiParamAttribute>();
                    if (attr != null && actionName.Equals(attr.Name, StringComparison.OrdinalIgnoreCase))
                        return true;

                    string cleanClassName = t.Name.Replace("Action", "").Replace("Query", "");
                    return cleanClassName.Equals(actionName, StringComparison.OrdinalIgnoreCase)
                        || t.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase);
                });

            if (logicType == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LogicFactory]: Type '{actionName}' not found.");
                return null;
            }

            IRevitLogic logicInstance = (IRevitLogic)Activator.CreateInstance(logicType);

            // 2. Заполнение свойств данными
            MapJsonToProperties(logicInstance, data);

            return logicInstance;
        }

        private static void MapJsonToProperties(IRevitLogic instance, JToken data)
        {
            if (data == null) return;

            // 1. Пытаемся работать с данными как с объектом
            JObject paramsNode = data as JObject;
            if (paramsNode == null) return;

            // 2. Умный переход: если мы получили весь экшен, но параметры лежат внутри "params"
            if (paramsNode["params"] is JObject nested) paramsNode = nested;
            else if (paramsNode["Parameters"] is JObject nestedP) paramsNode = nestedP;

            PropertyInfo[] properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                var attr = prop.GetCustomAttribute<AiParamAttribute>();
                if (attr == null) continue;

                // Имя ключа из атрибута (например, "categoryName")
                string targetKey = attr.Name ?? prop.Name;

                // 3. КЛЮЧЕВАЯ ПРАВКА: Игнорируем регистр (categoryName == CategoryName)
                JToken jsonValue = paramsNode.GetValue(targetKey, StringComparison.OrdinalIgnoreCase);

                // Проверяем, что значение есть и оно не пустое
                if (jsonValue != null && jsonValue.Type != JTokenType.Null)
                {
                    try
                    {
                        string stringValue = jsonValue.ToString().Trim();
                        if (string.IsNullOrEmpty(stringValue)) continue;

                        // 4. Конвертация типов
                        if (prop.PropertyType == typeof(double))
                        {
                            prop.SetValue(instance, ParseToRevitFeet(stringValue));
                        }
                        else if (prop.PropertyType == typeof(long))
                        {
                            prop.SetValue(instance, long.Parse(stringValue));
                        }
                        else if (prop.PropertyType == typeof(bool))
                        {
                            prop.SetValue(instance, bool.Parse(stringValue.ToLower()));
                        }
                        else if (prop.PropertyType == typeof(string))
                        {
                            prop.SetValue(instance, stringValue);
                        }
                        else
                        {
                            // Для Enum и других типов
                            object convertedValue = jsonValue.ToObject(prop.PropertyType);
                            prop.SetValue(instance, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mapping Error]: {prop.Name} <- {targetKey}. {ex.Message}");
                    }
                }
            }
        }

        public static double ParseToRevitFeet(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            // Очистка строки и парсинг числа
            var match = Regex.Match(input.Replace(',', '.'), @"-?\d+(\.\d+)?");
            if (!match.Success) return 0;

            if (!double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return 0;

            string lowerInput = input.ToLower();

            // Конвертация в Internal Revit Units (Футы)
            if (lowerInput.Contains("mm") || lowerInput.Contains("мм")) return value / 304.8;
            if (lowerInput.Contains("cm") || lowerInput.Contains("см")) return value / 30.48;
            if (lowerInput.Contains("m") || lowerInput.Contains("м")) return value / 0.3048;
            if (lowerInput.Contains("in") || lowerInput.Contains("\"")) return value / 12.0;
            if (lowerInput.Contains("ft") || lowerInput.Contains("'")) return value;

            // По умолчанию считаем, что ИИ прислал миллиметры
            return value / 304.8;
        }
    }
}