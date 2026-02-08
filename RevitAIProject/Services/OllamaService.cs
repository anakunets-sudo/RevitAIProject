using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitAIProject.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434/api/generate";

        public OllamaService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        /*
        /// <summary>
        /// Формирует жесткие инструкции для Qwen 2.5 по генерации JSON для Revit
        /// </summary>
        private string GetSystemInstructions()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("### ROLE: Revit BIM Assistant. ");
            sb.Append("### TASK: Translate user requests into JSON actions for Revit. ");
            sb.Append("### OUTPUT FORMAT: Return ONLY JSON object: { \"message\": \"text\", \"actions\": [ { \"name\": \"string\", ... } ] }. ");

            sb.Append("### AVAILABLE COMMANDS:\n");
            sb.Append(GetDynamicCommandsDescription()); // ЗДЕСЬ МАГИЯ: список строится сам

            sb.Append("\n### RULES: Convert all units to millimeters (mm). If unknown, return empty actions.");

            sb.Append("### ОБРАБОТКА НЕОПРЕДЕЛЕННОСТИ: ");
            sb.Append("Если в запросе недостаточно данных (например, 'как у соседа'), ");
            sb.Append("в поле 'message' вежливо объясни, каких именно данных не хватает ");
            sb.Append("(например, числового значения уклона или выбора стен). ");
            sb.Append("НИКОГДА не повторяй запрос пользователя дословно. ");
            sb.Append("Если команда невыполнима, верни пустой список 'actions': []. ");

            return sb.ToString();
        }*/

        
        private string GetSystemInstructions()
        {
            StringBuilder sb = new StringBuilder();

            // 1. Определение роли и основной задачи
            sb.Append("### ROLE: Revit BIM Assistant. ");
            sb.Append("### TASK: Translate user requests into JSON actions for Autodesk Revit API. ");

            // 2. Жесткое правило по языку (Зеркальный ответ)
            sb.Append("### LANGUAGE RULE: Detect the user's language. ");
            sb.Append("Always write the 'message' field in the SAME language used by the user. ");
            sb.Append("If the user asks in Russian, answer in Russian. If in English, answer in English. ");

            // 3. Формат вывода
            sb.Append("### OUTPUT FORMAT: Return ONLY a valid JSON object. No markdown, no extra text. ");
            sb.Append("Schema: { \"message\": \"text\", \"actions\": [ { \"name\": \"string\", \"params...\" } ] }. ");

            // 4. Описание доступных команд (Discovery)
            sb.Append("### AVAILABLE COMMANDS: ");
            sb.Append(GetDynamicCommandsDescription()); // Автоматический список из ваших классов Actions

            // 5. Правила обработки данных и единиц измерения
            sb.Append("### RULES: 1. Convert all units (meters, centimeters) to millimeters (mm) and use them for the 'thickness' and 'offset' parameters. Always extract numbers.");
            sb.Append("2. For 'CreateFloor', if thickness or offset is not specified, ask the user to provide them in their language. ");

            // 6. Обработка неопределенности (Ваш запрос про "соседа" и "не понял")
            sb.Append("### UNCERTAINTY & ERRORS: If the request is unclear, missing data (like 'neighbor's floor'), ");
            sb.Append("or the command is unknown, provide a helpful and polite explanation in the user's language ");
            sb.Append("in the 'message' field and return an empty 'actions': [] list. ");
            sb.Append("NEVER repeat the user's prompt as the answer. ");

            // 7. Примеры для обучения (Few-Shot)
            sb.Append("### EXAMPLES: ");
            sb.Append("User: 'Плита 200мм' -> { \"message\": \"Создаю перекрытие толщиной 200мм...\", \"actions\": [{\"name\": \"CreateFloor\", \"thickness\": 200}] }. ");
            sb.Append("User: 'Make it 300' -> { \"message\": \"Applying 300mm thickness...\", \"actions\": [{\"name\": \"CreateFloor\", \"thickness\": 300}] }. ");
            sb.Append("User: 'Как у соседа' -> { \"message\": \"Извините, я не вижу параметры соседа. Укажите толщину плиты в мм.\", \"actions\": [] }. ");

            return sb.ToString();
        }

        private string GetExamples()
        {
            return "User: 'Плита 300' -> {\"message\": \"Создаю перекрытие 300мм...\", \"actions\": [{\"name\": \"CreateFloor\", \"thickness\": 300}]}\n" +
                   "User: 'Make it 200' -> {\"message\": \"Setting thickness to 200mm...\", \"actions\": [{\"name\": \"CreateFloor\", \"thickness\": 200}]}\n" +
                   "User: 'Сосед' -> {\"message\": \"Не вижу параметров соседа. Укажите толщину.\", \"actions\": []}";
        }

        public async Task<AiResponse> GetAiResponseAsync(string userMessage)
        {
            try
            {
                /*
                // Подготовка данных запроса для Ollama
                var requestData = new
                {
                    model = "qwen2.5:7b",
                    prompt = GetSystemInstructions() + "\n\nUser Request: " + userMessage,
                    format = "json", // Заставляет Qwen возвращать валидный JSON
                    stream = false,
                    options = new
                    {
                        temperature = 0.1 // Снижаем случайность для точных параметров
                    }
                };*/
                
                // Склеиваем всё в один большой "контекст" для модели
                StringBuilder fullPrompt = new StringBuilder();

                // 1. Сначала инструкции (Роль, Команды, Язык)
                fullPrompt.Append(GetSystemInstructions());

                // 2. Затем примеры (Few-Shot обучение)
                fullPrompt.Append("\n\n### EXAMPLES OF CORRECT RESPONSES:\n");
                fullPrompt.Append(GetExamples());

                // 3. И в конце сам запрос пользователя
                fullPrompt.Append("\n\n### CURRENT USER REQUEST:\n");
                fullPrompt.Append(userMessage);

                // Подготовка данных запроса для Ollama
                var requestData = new
                {
                    model = "qwen2.5:7b",
                    prompt = fullPrompt.ToString(),
                    format = "json",
                    stream = false,
                    options = new { temperature = 0.1 }
                };

                string jsonPayload = JsonConvert.SerializeObject(requestData);
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Выполнение запроса
                HttpResponseMessage response = await _httpClient.PostAsync(OllamaUrl, content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                // Извлекаем текстовое поле "response" из оболочки Ollama
                JObject ollamaJson = JObject.Parse(responseBody);
                string rawAiResponse = ollamaJson["response"] != null ? ollamaJson["response"].ToString() : "{}";

                return ParseToAiResponse(rawAiResponse);
            }
            catch (Exception ex)
            {
                AiResponse errorResponse = new AiResponse();
                errorResponse.Message = "Ошибка сервиса Ollama: " + ex.Message;
                errorResponse.Actions = new List<IRevitAction>();
                return errorResponse;
            }
        }

        private string GetDynamicCommandsDescription()
        {
            var sb = new StringBuilder();
            var actionTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IRevitAction).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in actionTypes)
            {
                // Убираем слово Action из имени для ИИ (CreateFloorAction -> CreateFloor)
                string shortName = type.Name.Replace("Action", "");
                sb.Append("- ").Append(shortName).Append(" params: { ");

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var paramNames = new List<string>();

                foreach (var prop in props)
                {
                    // Берем имя из атрибута или имя самого свойства
                    var attr = prop.GetCustomAttribute<AiParamAttribute>();
                    if (attr != null) paramNames.Add("\"" + attr.Name + "\": number|string");
                    else if (prop.Name != "ActionName") paramNames.Add("\"" + prop.Name.ToLower() + "\": number|string");
                }

                sb.Append(string.Join(", ", paramNames));
                sb.Append(" }\n");
            }

            return sb.ToString();
        }

        private AiResponse ParseToAiResponse(string json)
        {
            AiResponse result = new AiResponse();
            result.Actions = new List<IRevitAction>();

            try
            {
                JObject data = JObject.Parse(json);

                // 1. Получаем сообщение для пользователя
                result.Message = data["message"] != null ? data["message"].ToString() : "Команда обработана.";

                // 2. Парсим список действий через фабрику
                JArray actionsArray = data["actions"] as JArray;
                if (actionsArray != null)
                {
                    foreach (JToken token in actionsArray)
                    {
                        string actionName = token["name"] != null ? token["name"].ToString() : null;

                        // Используем созданную ранее фабрику для создания объектов действий
                        IRevitAction action = ActionFactory.CreateAction(actionName, token);

                        if (action != null)
                        {
                            result.Actions.Add(action);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "Ошибка разбора JSON от ИИ: " + ex.Message;
            }

            return result;
        }
    }
}