using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RevitAIProject.Actions;

namespace RevitAIProject.Services
{
    public static class ActionFactory
    {
        /// <summary>
        /// Автоматически находит и создает экземпляр команды на основе JSON от ИИ
        /// </summary>
        public static IRevitAction CreateAction(string actionName, JToken data)
        {
            if (string.IsNullOrEmpty(actionName)) return null;

            // 1. Ищем подходящий класс в пространстве имен RevitAIProject.Actions
            // Проверяем совпадение имени (например "CreateFloor" или "CreateFloorAction")
            Type actionType = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IRevitAction).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .FirstOrDefault(t => t.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase)
                                  || t.Name.Equals(actionName + "Action", StringComparison.OrdinalIgnoreCase));

            if (actionType == null) return null;

            // 2. Создаем объект команды
            IRevitAction action = (IRevitAction)Activator.CreateInstance(actionType);

            // 3. Маппим данные из JSON в свойства объекта
            MapJsonToProperties(action, data);

            return action;
        }

        private static void MapJsonToProperties(IRevitAction action, JToken data)
        {
            if (data == null) return;

            // Получаем все публичные свойства класса команды
            PropertyInfo[] properties = action.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                // Проверяем наличие атрибута [AiParam("имя")]
                AiParamAttribute attr = prop.GetCustomAttribute<AiParamAttribute>();
                string targetKey = attr != null ? attr.Name : prop.Name;

                // Ищем значение в JSON (пробуем заданный ключ и нижний регистр для надежности)
                JToken jsonValue = data[targetKey] ?? data[targetKey.ToLower()];

                if (jsonValue != null)
                {
                    try
                    {
                        /*// Автоматическое приведение типов (string -> double, int и т.д.)
                        object convertedValue = jsonValue.ToObject(prop.PropertyType);
                        if (prop.CanWrite)
                        {
                            prop.SetValue(action, convertedValue);
                        }
                        */
                         // Пробуем извлечь число, даже если оно в строке
                        if (prop.PropertyType == typeof(double))
                        {
                            // Используйте Regex для извлечения числа из "100мм"
                            string sVal = jsonValue.ToString().Replace("мм", "").Replace("mm", "").Trim();
                            if (double.TryParse(sVal, out double result))
                            {
                                prop.SetValue(action, result);
                            }
                        }
                        else
                        {
                             // Стандартная логика для других типов
                             object convertedValue = jsonValue.ToObject(prop.PropertyType);
                             if (prop.CanWrite) prop.SetValue(action, convertedValue);
                        }
                    }
                    catch (Exception)
                    {
                        // Ошибки типов игнорируем, чтобы не обрушить весь процесс
                    }
                }
            }
        }
    }
}