using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitAIProject.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
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
        private string GetSystemInstructions()
        {
            StringBuilder sb = new StringBuilder();

            // 1. ROLE & CORE TASK
            sb.Append("### ROLE: Revit BIM Assistant. ");
            sb.Append("### TASK: Translate user requests into a JSON array of Revit API actions. ");
            sb.Append("### MULTI-ACTION RULE: If the user asks for multiple steps (e.g., 'create and move'), " +
                      "return ALL steps in the 'actions' array in the correct execution order. ");

            // 2. LANGUAGE & FORMAT
            sb.Append("### RULES: ");
            sb.Append("1. Respond ONLY with a raw JSON object. No markdown, no explanations. ");
            sb.Append("2. Detect user language. Write 'message' in the SAME language as the user. ");
            sb.Append("3. Use EXACT numbers from the request. If units (mm, in, ft) are provided, include them in the value string (e.g., '500mm'). ");

            // 3. VARIABLE SYSTEM ($) - Связка через TargetAiName и AssignAiName
            sb.Append("### VARIABLE SYSTEM ($): ");
            sb.Append("1. To label a NEW element for later use, invent a name starting with '$' (e.g., '$f1') and put it in 'assign_ai_name'. ");
            sb.Append("2. To refer to that element in later actions, put its name (e.g., '$f1') in 'target_ai_name'. ");
            sb.Append("3. SELECTION: If the user refers to 'this', 'selected' or 'current', leave 'target_ai_name' EMPTY to use the current Revit selection. ");

            // 4. COMMAND DISCOVERY (Reflection)
            sb.Append("### AVAILABLE COMMANDS: ");
            sb.Append(GetDynamicCommandsDescription());

            // 5. FINAL OUTPUT SCHEMA
            sb.Append("### OUTPUT FORMAT: Return ONLY valid JSON. ");
            sb.Append("Schema: { \"message\": \"User description\", \"actions\": [ { \"name\": \"ActionName\", \"Parameters\": { \"key\": \"string\" } } ] }. ");

            return sb.ToString();
        }        

        private string GetExamples()
        {
            StringBuilder sb = new StringBuilder();

            // Пример 1: Создание с именованием ($id)
            sb.AppendLine("User: 'Плита со смещением 500 дюймов'");
            sb.AppendLine("Response: { \"message\": \"Создаю перекрытие ($f1) со смещением 500 дюймов.\", \"actions\": [ { \"name\": \"CreateFloor\", \"Parameters\": { \"assign_ai_name\": \"$f1\", \"offset\": \"500in\" } } ] }");

            // Пример 2: Цепочка (Создание + Перемещение)
            sb.AppendLine("User: 'Создай пол и сдвинь его влево на 1 метр'");
            sb.AppendLine("Response: { \"message\": \"Создаю пол ($f1) и сдвигаю его на 1000мм влево.\", \"actions\": [ " +
                          "{ \"name\": \"CreateFloor\", \"Parameters\": { \"assign_ai_name\": \"$f1\", \"offset\": \"0mm\" } }, " +
                          "{ \"name\": \"MoveElement\", \"Parameters\": { \"target_ai_name\": \"$f1\", \"dx\": -1000мм, \"dy\": 0, \"dz\": 0 } } ] }");

            // Пример 3: Работа с выделением (пустой target_ai_name)
            sb.AppendLine("User: 'Сдвинь это вверх на 200мм'");
            sb.AppendLine("Response: { \"message\": \"Сдвигаю выбранный элемент на 200мм вверх.\", \"actions\": [ { \"name\": \"MoveElement\", \"Parameters\": { \"target_ai_name\": \"\", \"dx\": 0, \"dy\": 200, \"dz\": 0 } } ] }");

            // Пример 3: Работа с выделением (пустой target_ai_name)
            sb.AppendLine("User: 'Сдвинь это вверх по уровню на 200см'");
            sb.AppendLine("Response: { \"message\": \"Сдвигаю выбранный элемент на 200см по уровню вверх.\", \"actions\": [ { \"name\": \"MoveElement\", \"Parameters\": { \"target_ai_name\": \"\", \"dx\": 0, \"dy\": 200см, \"dz\": 0 } } ] }");

            return sb.ToString();
        }

        public async Task<AiResponse> GetAiResponseAsync(string userMessage, CancellationToken ct)
        {
            try
            {
                StringBuilder fullPrompt = new StringBuilder();

                fullPrompt.Append(GetSystemInstructions());
                fullPrompt.Append("\n\n### EXAMPLES:\n");
                fullPrompt.Append(GetExamples());
                fullPrompt.Append("\n\n### USER REQUEST:\n");
                fullPrompt.Append(userMessage);

                var requestData = new
                {
                    model = "qwen2.5:7b",
                    prompt = fullPrompt.ToString(),
                    format = "json",
                    stream = false,
                    options = new { temperature = 0.0 }
                };

                string jsonPayload = JsonConvert.SerializeObject(requestData);
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Передаем токен отмены в POST запрос
                HttpResponseMessage response = await _httpClient.PostAsync(OllamaUrl, content, ct);
                response.EnsureSuccessStatusCode();

                // Передаем токен в чтение контента
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject ollamaJson = JObject.Parse(responseBody);
                string rawAiResponse = ollamaJson["response"]?.ToString() ?? "{}";

                // В продакшене MessageBox лучше убрать или вызывать через диспетчер, 
                // так как это может заблокировать поток.
                 System.Windows.MessageBox.Show(rawAiResponse, "AI Raw Output");

                return ParseToAiResponse(rawAiResponse);
            }
            catch (OperationCanceledException)
            {
                // Возвращаем пустой ответ или уведомление о прерывании
                return new AiResponse
                {
                    Message = "Генерация была прервана пользователем.",
                    Actions = new List<IRevitAction>()
                };
            }
            catch (Exception ex)
            {
                return new AiResponse
                {
                    Message = $"Ошибка при обращении к ИИ: {ex.Message}",
                    Actions = new List<IRevitAction>()
                };
            }
        }

        /*
        public async Task<AiResponse> GetAiResponseAsync(string userMessage)
        {
            try
            {
                StringBuilder fullPrompt = new StringBuilder();

                fullPrompt.Append(GetSystemInstructions());
                fullPrompt.Append("\n\n### EXAMPLES:\n");
                fullPrompt.Append(GetExamples());
                fullPrompt.Append("\n\n### USER REQUEST:\n");
                fullPrompt.Append(userMessage);

                // Подготовка данных запроса для Ollama
                var requestData = new
                {
                    model = "qwen2.5:7b",
                    prompt = fullPrompt.ToString(),
                    format = "json",
                    stream = false,
                    options = new { temperature = 0.0 }
                };

                string jsonPayload = JsonConvert.SerializeObject(requestData);
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Выполнение запроса
                HttpResponseMessage response = await _httpClient.PostAsync(OllamaUrl, content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                // 1. Извлекаем ответ из оболочки Ollama
                JObject ollamaJson = JObject.Parse(responseBody);
                string rawAiResponse = ollamaJson["response"]?.ToString() ?? "{}";

                 System.Windows.MessageBox.Show(rawAiResponse, "AI Raw Output");

                return ParseToAiResponse(rawAiResponse);
            }
            catch (Exception ex)
            {
                AiResponse errorResponse = new AiResponse();
                errorResponse.Message = "Ошибка сервиса Ollama: " + ex.Message;
                errorResponse.Actions = new List<IRevitAction>();
                return errorResponse;
            }
        }*/

        private string GetDynamicCommandsDescription()
        {
            var sb = new StringBuilder();

            // 1. Находим все классы экшенов в текущей сборке
            var actionTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(IRevitAction).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in actionTypes)
            {
                // Создаем временный экземпляр, чтобы прочитать свойства (или берем через Reflection)
                var instance = Activator.CreateInstance(type) as IRevitAction;

                // 2. Ищем атрибут на свойстве ActionName
                var nameProp = type.GetProperty(nameof(IRevitAction.ActionName));
                var nameAttr = nameProp?.GetCustomAttribute<AiParamAttribute>();

                // Улучшенная логика: если имя в атрибуте null ИЛИ пустая строка ""
                string cmdName = string.IsNullOrWhiteSpace(nameAttr?.Name)
                                 ? instance.ActionName
                                 : nameAttr.Name;

                // Описание: если атрибута нет или Description пустой
                string cmdDesc = string.IsNullOrWhiteSpace(nameAttr?.Description)
                                 ? $"Executes the {cmdName} command."
                                 : nameAttr.Description;

                sb.AppendLine($"- **{cmdName}**: {cmdDesc}");

                // 3. Собираем параметры (свойства с AiParam)
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.GetCustomAttribute<AiParamAttribute>() != null);

                foreach (var prop in props)
                {
                    // Пропускаем само свойство ActionName, так как мы его уже описали выше
                    if (prop.Name == nameof(IRevitAction.ActionName)) continue;

                    var pAttr = prop.GetCustomAttribute<AiParamAttribute>();

                    // Здесь тоже можно добавить fallback, если нужно
                    string pName = pAttr.Name;
                    string pDesc = pAttr.Description ?? "No description provided.";

                    sb.AppendLine($"  * {pName}: {pDesc}");
                }

                sb.AppendLine(); // Разделитель между командами
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