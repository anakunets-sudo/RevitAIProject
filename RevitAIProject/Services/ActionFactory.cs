using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace RevitAIProject.Services
{
    public static class ActionFactory
    {
        public static Logic.Actions.IRevitAction CreateAction(string actionName, JToken data)
        {
            if (string.IsNullOrEmpty(actionName)) return null;

            // 1. Ищем тип класса в текущей сборке
            Type actionType = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(Logic.Actions.IRevitAction).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .FirstOrDefault(t =>
                {
                    // Убираем "Action" из имени класса для сравнения (MoveAction -> Move)
                    string cleanClassName = t.Name.Replace("Action", "");

                    return cleanClassName.Equals(actionName, StringComparison.OrdinalIgnoreCase)
                        || t.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase);
                });

            if (actionType == null)
            {
                // Полезно для отладки в Revit
                System.Diagnostics.Debug.WriteLine($"[ActionFactory]: Action '{actionName}' not found.");
                return null;
            }

            // 2. Создаем экземпляр экшена
            // Важно: у экшена должен быть пустой конструктор (по умолчанию он есть)
            Logic.Actions.IRevitAction action = (Logic.Actions.IRevitAction)Activator.CreateInstance(actionType);

            // 3. Заполняем свойства экшена данными из JSON (включая TargetAiName из базы)
            MapJsonToProperties(action, data);

            return action;
        }

        private static void MapJsonToProperties(Logic.Actions.IRevitAction action, JToken data)
        {
            if (data == null) return;

            // Ищем параметры во вложенном узле или в корне
            JToken paramsNode = data["Parameters"] ?? data;
            PropertyInfo[] properties = action.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                //AiParamAttribute attr = prop.GetCustomAttribute<AiParamAttribute>();
                //string targetKey = attr != null ? attr.Name : prop.Name;

                var attr = prop.GetCustomAttribute<Logic.AiParamAttribute>();
                string targetKey = attr?.Name ?? prop.Name; // Ищем "target_ai_name", а не "TargetAiName"

                JToken jsonValue = paramsNode[targetKey] ?? paramsNode[targetKey.ToLower()]
                                 ?? data[targetKey] ?? data[targetKey.ToLower()];

                if (jsonValue != null && prop.CanWrite)
                {
                    try
                    {
                        if (prop.PropertyType == typeof(double))
                        {
                            // Ключевой момент: Конвертируем строку с единицами в футы (Internal Revit Units)
                            double feetValue = ParseToRevitFeet(jsonValue.ToString());
                            prop.SetValue(action, feetValue);
                        }
                        else
                        {
                            object convertedValue = jsonValue.ToObject(prop.PropertyType);
                            prop.SetValue(action, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mapping Error]: {prop.Name} - {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Извлекает число и конвертирует его в футы на основе единиц измерения (метрических или имперских)
        /// </summary>
        public static double ParseToRevitFeet(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            // 1. Находим число (поддержка точки и запятой для безопасности)
            var match = Regex.Match(input.Replace(',', '.'), @"-?\d+(\.\d+)?");
            if (!match.Success) return 0;

            if (!double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return 0;

            // 2. Логика конвертации в футы (Internal Revit Units)
            string lowerInput = input.ToLower();

            // МЕТРИЧЕСКИЕ
            if (lowerInput.Contains("mm") || lowerInput.Contains("мм") || lowerInput.Contains("миллиметр"))
                return value / 304.8;

            if (lowerInput.Contains("cm") || lowerInput.Contains("см") || lowerInput.Contains("сантиметр"))
                return value / 30.48;

            if (lowerInput.Contains("m") || lowerInput.Contains("м") || lowerInput.Contains("метр"))
                return value / 0.3048;

            // ИМПЕРСКИЕ
            if (lowerInput.Contains("in") || lowerInput.Contains("\"") || lowerInput.Contains("дюйм"))
                return value / 12.0; // В одном футе 12 дюймов

            if (lowerInput.Contains("ft") || lowerInput.Contains("'") || lowerInput.Contains("фут"))
                return value; // Уже в футах

            // Если единицы не указаны — по умолчанию считаем миллиметры (самый частый кейс в BIM)
            // Либо можно возвращать 'value', если вы доверяете ИИ
            return value / 304.8;
        }
    }
}