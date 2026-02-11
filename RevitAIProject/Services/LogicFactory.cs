using Newtonsoft.Json.Linq;
using RevitAIProject.Logic;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitAIProject.Services
{
    public static class LogicFactory
    {
        /// <summary>
        /// Создает экземпляр логики (Action или Query) на основе имени и заполняет его параметры.
        /// </summary>
        public static IRevitLogic CreateLogic(string actionName, JToken data)
        {
            if (string.IsNullOrEmpty(actionName)) return null;

            // 1. Универсальный поиск типа (ищем любой IRevitLogic с атрибутом [AiParam])
            Type logicType = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IRevitLogic).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .FirstOrDefault(t =>
                {
                    // Сначала проверяем атрибут на самом классе (самый надежный способ)
                    var attr = t.GetCustomAttribute<AiParamAttribute>();
                    if (attr != null && actionName.Equals(attr.Name, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Запасной вариант: проверка по имени класса (GetElementsQuery -> GetElements)
                    string cleanClassName = t.Name.Replace("Action", "").Replace("Query", "");
                    return cleanClassName.Equals(actionName, StringComparison.OrdinalIgnoreCase)
                        || t.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase);
                });

            if (logicType == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ActionFactory]: Logic/Action '{actionName}' not found.");
                return null;
            }

            // 2. Создаем экземпляр
            IRevitLogic logicInstance = (IRevitLogic)Activator.CreateInstance(logicType);

            // 3. Заполняем свойства данными из JSON
            MapJsonToProperties(logicInstance, data);

            return logicInstance;
        }

        private static void MapJsonToProperties(IRevitLogic instance, JToken data)
        {
            if (data == null) return;

            // Извлекаем узел параметров из структуры { "name": "...", "Parameters": { ... } }
            JToken paramsNode = data["Parameters"] ?? data;
            PropertyInfo[] properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                // Ищем атрибут AiParam на свойстве
                var attr = prop.GetCustomAttribute<AiParamAttribute>();
                if (attr == null) continue;

                // Ищем значение в JSON по имени из атрибута (например, "target_ai_name") или по имени свойства
                string targetKey = attr.Name ?? prop.Name;

                // Проверяем разные варианты написания ключа (регистронезависимо)
                JToken jsonValue = paramsNode[targetKey]
                                 ?? paramsNode[targetKey.ToLower()]
                                 ?? paramsNode[targetKey.ToUpper()];

                if (jsonValue != null && prop.CanWrite)
                {
                    try
                    {
                        string stringValue = jsonValue.ToString();

                        // Логика конвертации типов для Revit 2019
                        if (prop.PropertyType == typeof(double))
                        {
                            // Используем твой парсер для перевода в футы
                            double feetValue = ParseToRevitFeet(stringValue);
                            prop.SetValue(instance, feetValue);
                        }
                        else if (prop.PropertyType == typeof(long))
                        {
                            prop.SetValue(instance, long.Parse(stringValue));
                        }
                        else if (prop.PropertyType == typeof(bool))
                        {
                            prop.SetValue(instance, bool.Parse(stringValue.ToLower()));
                        }
                        else
                        {
                            // Стандартная десериализация для строк и остальных типов
                            object convertedValue = jsonValue.ToObject(prop.PropertyType);
                            prop.SetValue(instance, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mapping Error]: Property {prop.Name} - {ex.Message}");
                    }
                }
            }
        }

        public static double ParseToRevitFeet(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            // Поддержка точки и запятой
            var match = Regex.Match(input.Replace(',', '.'), @"-?\d+(\.\d+)?");
            if (!match.Success) return 0;

            if (!double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return 0;

            string lowerInput = input.ToLower();

            // Конвертация в футы (Internal Revit Units)
            if (lowerInput.Contains("mm") || lowerInput.Contains("мм")) return value / 304.8;
            if (lowerInput.Contains("cm") || lowerInput.Contains("см")) return value / 30.48;
            if (lowerInput.Contains("m") || lowerInput.Contains("м")) return value / 0.3048;
            if (lowerInput.Contains("in") || lowerInput.Contains("\"")) return value / 12.0;
            if (lowerInput.Contains("ft") || lowerInput.Contains("'")) return value;

            // По умолчанию - миллиметры
            return value / 304.8;
        }
    }
}