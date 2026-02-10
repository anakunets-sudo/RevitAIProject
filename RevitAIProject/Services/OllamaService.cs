using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            sb.Append("### TASK: Translate user requests into a JSON array of Revit API actions or queries. ");
            sb.Append("### MULTI-ACTION RULE: If the user asks for multiple steps (e.g., 'find and delete'), " +
                      "return ALL steps in the 'actions' array in the correct execution order. ");

            // 2. LANGUAGE & FORMAT (Твои правила сохранены)
            sb.Append("### LANGUAGE RULES: ");
            sb.Append("1. Respond ONLY with a raw JSON object. No markdown, no explanations. ");
            sb.Append("2. Detect user language. Write 'message' in the SAME language as the user. ");
            sb.Append("3. All technical parameters (p, o, v, action name) must be in ENGLISH. ");
            sb.Append("4. Use EXACT numbers from the request. If units (mm, in, ft) are provided, include them in the value string (e.g., '500mm'). ");

            // 3. VARIABLE SYSTEM ($) & SESSION CONTEXT
            sb.Append("### CONTEXT SYSTEM: ");
            sb.Append("1. To label a NEW element for later use, invent a name starting with '$' (e.g., '$f1') and put it in 'assign_ai_name'. ");
            sb.Append("2. To refer to that element later, put its name (e.g., '$f1') in 'target_ai_name'. ");
            sb.Append("3. SELECTION: If the user refers to 'this', 'selected' or 'current', leave 'target_ai_name' EMPTY. ");
            sb.Append("4. QUERIES: If the user refers to 'found elements' or 'them' after a search, use 'LAST_QUERY_RESULT' in 'target_ai_name'. ");

            // 4. QUERY & FILTER RULES (Новое)
            sb.Append("### QUERY RULES: ");
            sb.Append("1. Use 'GetElements' for any search or counting task. ");
            sb.Append("2. 'filterJson' must be a JSON string of rules: [{\"p\": \"ParamName\", \"o\": \"Operator\", \"v\": \"Value\"}]. ");
            sb.Append("3. Operators for filterJson: equals, notequals, greater, less, contains. ");

            // 5. COMMAND DISCOVERY (Reflection)
            sb.Append("### AVAILABLE COMMANDS: ");
            sb.Append(GetDynamicCommandsDescription());

            // 6. FINAL OUTPUT SCHEMA
            sb.Append("### OUTPUT FORMAT: Return ONLY valid JSON. ");
            sb.Append("Schema: { \"message\": \"User description\", \"actions\": [ { \"name\": \"ActionName\", \"Parameters\": { \"key\": \"string\" } } ] }. ");

            return sb.ToString();
        }

        private string GetExamples()
        {
            StringBuilder sb = new StringBuilder();

            // Пример 1: Поиск и подсчет (Query)
            sb.AppendLine("User: 'Сколько дверей в проекте?'");
            sb.AppendLine("Response: { \"message\": \"Считаю общее количество дверей в проекте.\", \"actions\": [ { \"name\": \"GetElements\", \"Parameters\": { \"categoryId\": -2000023 } } ] }");

            // Пример 2: Сложный поиск через FilterJson
            sb.AppendLine("User: 'Найди стены толщиной 300мм'");
            sb.AppendLine("Response: { \"message\": \"Ищу стены с параметром толщины 300мм.\", \"actions\": [ { \"name\": \"GetElements\", \"Parameters\": { \"categoryId\": -2000011, \"filterJson\": \"[{\\\"p\\\":\\\"Width\\\",\\\"o\\\":\\\"equals\\\",\\\"v\\\":\\\"300mm\\\"}]\" } } ] }");

            // Пример 3: Цепочка Поиск -> Действие (Использование LAST_QUERY_RESULT)
            sb.AppendLine("User: 'Найди все окна на 2 этаже и удали их'");
            sb.AppendLine("Response: { \"message\": \"Нахожу окна на 2 этаже и удаляю их.\", \"actions\": [ " +
                          "{ \"name\": \"GetElements\", \"Parameters\": { \"categoryId\": -2000023, \"filterJson\": \"[{\\\"p\\\":\\\"Level\\\",\\\"o\\\":\\\"equals\\\",\\\"v\\\":\\\"Level 2\\\"}]\" } }, " +
                          "{ \"name\": \"DeleteElements\", \"Parameters\": { \"target_ai_name\": \"LAST_QUERY_RESULT\" } } ] }");

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
                    Actions = new List<Logic.Actions.IRevitAction>()
                };
            }
            catch (Exception ex)
            {
                return new AiResponse
                {
                    Message = $"Ошибка при обращении к ИИ: {ex.Message}",
                    Actions = new List<Logic.Actions.IRevitAction>()
                };
            }
        }

        private string GetDynamicCommandsDescription()
        {
            var sb = new StringBuilder();

            // 1. Ищем ВСЕ классы в сборке, у которых есть атрибут [AiParam] на самом классе
            // Это позволит нам не привязываться к интерфейсам и видеть оба неймспейса (Actions и Queries)
            var commandTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<Logic.AiParamAttribute>() != null);

            foreach (var type in commandTypes)
            {
                // Получаем атрибут самого класса (описание команды)
                var classAttr = type.GetCustomAttribute<Logic.AiParamAttribute>();

                string cmdName = classAttr.Name;
                string cmdDesc = classAttr.Description ?? "No description provided.";

                // Определяем тип команды для подсказки ИИ (Action или Query)
                string category = typeof(Logic.Queries.IRevitQuery).IsAssignableFrom(type) ? "[QUERY]" : "[ACTION]";

                sb.AppendLine($"- **{cmdName}** {category}: {cmdDesc}");

                // 2. Собираем параметры этого класса (свойства с атрибутом [AiParam])
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.GetCustomAttribute<Logic.AiParamAttribute>() != null);

                foreach (var prop in props)
                {
                    var pAttr = prop.GetCustomAttribute<Logic.AiParamAttribute>();

                    // Если в атрибуте свойства имя не указано, берем имя самого свойства в коде
                    string pName = string.IsNullOrWhiteSpace(pAttr.Name) ? prop.Name : pAttr.Name;
                    string pDesc = pAttr.Description ?? "No description provided.";

                    // Добавляем информацию о типе данных, чтобы ИИ знал, слать строку или число
                    string pType = prop.PropertyType.Name;

                    sb.AppendLine($"  * {pName} ({pType}): {pDesc}");
                }

                sb.AppendLine(); // Разделитель для читаемости промпта
            }

            return sb.ToString();
        }

        private AiResponse ParseToAiResponse(string json)
        {
            AiResponse result = new AiResponse();
            result.Actions = new List<Logic.Actions.IRevitAction>();

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
                        Logic.Actions.IRevitAction action = ActionFactory.CreateAction(actionName, token);

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