using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            // 1. ROLE & CORE MISSION
            sb.Append("### ROLE: Revit 2019 AI Automation Agent. ");
            sb.Append("### TASK: Convert user intent into a sequence of Revit API commands. ");
            sb.Append("### EXECUTION RULE: If the user wants to 'find', 'count', 'show' or 'check' elements, you MUST use the 'GetElements' command first. ");
            sb.Append("### IMPORTANT: When you return a 'GetElements' action, your 'message' field should ONLY describe the intent (e.g., 'Searching for windows...'). ");
            sb.Append("DO NOT try to guess the count. Wait for the SYSTEM REPORT in the next turn to give the final answer. ");

            // 2. STRICT FORMATTING (Crucial for Ollama stability)
            sb.Append("### RESPONSE FORMAT RULES: ");
            sb.Append("1. Respond ONLY with a raw JSON object. ");
            sb.Append("2. PROHIBITED: No markdown code blocks (```json), no conversational preambles (e.g., 'Sure!', 'I will help'). ");
            sb.Append("3. LANGUAGE: Write the 'message' field in the user's language. All technical names (Command names, Parameter keys) MUST be in ENGLISH. ");
            sb.Append("2. Detect user language. Write 'message' in the SAME language as the user. ");

            // 3. CONTEXT & MEMORY (Session Management)
            sb.Append("### VARIABLE SYSTEM ($): ");
            sb.Append("1. To label a NEW element for later use, invent a name starting with '$' (e.g., '$f1') and put it in 'assign_ai_name'. ");
            sb.Append("2. To refer to that element in later actions, put its name (e.g., '$f1') in 'target_ai_name'. ");
            sb.Append("3. SELECTION: If the user refers to 'this', 'selected' or 'current', leave 'target_ai_name' EMPTY to use the current Revit selection. ");
            // 3. CONTEXT & MEMORY
            sb.Append("### SESSION CONTEXT: ");
            // Явно связываем техническое поле с понятием для ИИ
            sb.Append("1. Search results are stored in a system variable called 'LAST_QUERY_RESULT' (maps to LastFoundIds). ");
            sb.Append("2. If 'LAST_QUERY_RESULT' contains elements, I will inform you with 'SYSTEM: [Count] elements found'. ");
            sb.Append("3. To perform actions (like Move, Delete, Parameter set) on these elements, set 'target_ai_name': 'LAST_QUERY_RESULT'. ");

            // 4. PARAMETERS & FILTERS (C# 7.3 Compatibility)
            sb.Append("### PARAMETER RULES: ");
            sb.Append("1. 'filterJson' must be a double-escaped JSON string of rules. Example: \"[{\\\"p\\\":\\\"Level\\\",\\\"o\\\":\\\"equals\\\",\\\"v\\\":\\\"Level 1\\\"}]\". ");
            sb.Append("2. Operators: equals, notequals, greater, less, contains. ");
            sb.Append("3. Units: Always include units in values if provided (e.g., '300mm', '1500mm', '10ft'). ");

            // 5. DYNAMIC COMMANDS (Reflection-based)
            sb.Append("### AVAILABLE COMMANDS: ");
            sb.Append(GetDynamicCommandsDescription());

            // 6. UNIFIED OUTPUT SCHEMA (The most important part)
            sb.Append("### FINAL SCHEMA (ACTIONS ONLY): ");
            sb.Append("All operations (Queries and Actions) MUST be items in the 'actions' array. Use this EXACT structure: ");
            sb.Append("{ ");
            sb.Append("  \"message\": \"User-friendly description of what you are doing\", ");
            sb.Append("  \"actions\": [ ");
            sb.Append("    { \"name\": \"CommandName\", \"Parameters\": { \"key1\": \"value1\", \"key2\": \"value2\" } } ");
            sb.Append("  ] ");
            sb.Append("} ");

            return sb.ToString();
        }

        private string GetExamples()
        {
            StringBuilder sb = new StringBuilder();

            // Example 1: Basic Counting (Query)
            sb.AppendLine("User: 'How many walls are in the project?'");
            sb.AppendLine(@"Response: { ""message"": ""I am counting all walls in the project."", ""actions"": [ { ""name"": ""GetElements"", ""Parameters"": { ""categoryId"": -2000011 } } ] }");

            // Example 2: Filtering by Parameter (Complex Query)
            sb.AppendLine("User: 'Find all doors on Level 1'");
            sb.AppendLine(@"Response: { ""message"": ""Searching for doors on Level 1."", ""actions"": [ { ""name"": ""GetElements"", ""Parameters"": { ""categoryId"": -2000023, ""filterJson"": ""[{\""p\"":\""Level\"",\""o\"":\""equals\"",\""v\"":\""Level 1\""}]"" } } ] }");

            // Example 3: Sequence Query -> Action (Using LAST_QUERY_RESULT)
            sb.AppendLine("User: 'Find 300mm walls and delete them'");
            sb.AppendLine(@"Response: { ""message"": ""Finding 300mm walls and deleting them from the model."", ""actions"": [ " +
                          @"{ ""name"": ""GetElements"", ""Parameters"": { ""categoryId"": -2000011, ""filterJson"": ""[{\""p\"":\""Width\"",\""o\"":\""equals\"",\""v\"":\""300mm\""}]"" } }, " +
                          @"{ ""name"": ""DeleteElements"", ""Parameters"": { ""target_ai_name"": ""LAST_QUERY_RESULT"" } } ] }");

            // Example 4: View-specific search
            sb.AppendLine("User: 'Count windows in this view'");
            sb.AppendLine(@"Response: { ""message"": ""Counting windows on the current view."", ""actions"": [ { ""name"": ""GetElements"", ""Parameters"": { ""categoryId"": -2000014, ""viewScoped"": ""true"" } } ] }");

            // Пример 5: Создание с именованием ($id)
            sb.AppendLine("User: 'Плита со смещением 500 дюймов'");
            sb.AppendLine("Response: { \"message\": \"Создаю перекрытие ($f1) со смещением 500 дюймов.\", \"actions\": [ { \"name\": \"CreateFloor\", \"Parameters\": { \"assign_ai_name\": \"$f1\", \"offset\": \"500in\" } } ] }");

            // Пример 6: Цепочка (Создание + Перемещение)
            sb.AppendLine("User: 'Создай пол и сдвинь его влево на 1 метр'");
            sb.AppendLine("Response: { \"message\": \"Создаю пол ($f1) и сдвигаю его на 1000мм влево.\", \"actions\": [ " +
                          "{ \"name\": \"CreateFloor\", \"Parameters\": { \"assign_ai_name\": \"$f1\", \"offset\": \"0mm\" } }, " +
                          "{ \"name\": \"MoveElement\", \"Parameters\": { \"target_ai_name\": \"$f1\", \"dx\": -1000мм, \"dy\": 0, \"dz\": 0 } } ] }");

            // Пример 7: Работа с выделением (пустой target_ai_name)
            sb.AppendLine("User: 'Сдвинь это вверх на 200мм'");
            sb.AppendLine("Response: { \"message\": \"Сдвигаю выбранный элемент на 200мм вверх.\", \"actions\": [ { \"name\": \"MoveElement\", \"Parameters\": { \"target_ai_name\": \"\", \"dx\": 0, \"dy\": 200, \"dz\": 0 } } ] }");

            // Пример 8: Работа с выделением (пустой target_ai_name)
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

                CleanJson(responseBody);

                JObject ollamaJson = JObject.Parse(responseBody);
                string rawAiResponse = ollamaJson["response"]?.ToString() ?? "{}";

                // В продакшене MessageBox лучше убрать или вызывать через диспетчер, 
                // так как это может заблокировать поток.
                Debug.WriteLine(rawAiResponse, "AI Raw Output");

                return ParseToAiResponse(rawAiResponse);
            }
            catch (OperationCanceledException)
            {
                // Возвращаем пустой ответ или уведомление о прерывании
                return new AiResponse
                {
                    Message = "Генерация была прервана пользователем.",
                    Actions = new List<Logic.IRevitLogic>()
                };
            }
            catch (Exception ex)
            {
                return new AiResponse
                {
                    Message = $"Ошибка при обращении к ИИ: {ex.Message}",
                    Actions = new List<Logic.IRevitLogic>()
                };
            }
        }

        private string CleanJson(string raw)
        {
            // Убираем Markdown блоки, если они просочились
            raw = raw.Replace("```json", "").Replace("```", "").Trim();
            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start != -1 && end > start) return raw.Substring(start, end - start + 1);
            return raw;
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

        private AiResponse ParseToAiResponse(string rawJson)
        {
            var result = new AiResponse { Actions = new List<Logic.IRevitLogic>() };
            try
            {
                string json = CleanJson(rawJson); // Чистим от мусора
                JObject data = JObject.Parse(json);

                // Используем Case-Insensitive доступ
                result.Message = (data.GetValue("message", StringComparison.OrdinalIgnoreCase) ?? "Command processed.").ToString();

                var actionsArray = data.GetValue("actions", StringComparison.OrdinalIgnoreCase) as JArray;
                if (actionsArray != null)
                {
                    foreach (JToken token in actionsArray)
                    {
                        // Ищем имя команды, не боясь разного регистра
                        string actionName = (token["name"] ?? token["Name"])?.ToString();

                        // Передаем весь токен в фабрику
                        var action = LogicFactory.CreateLogic(actionName, token);
                        if (action != null) result.Actions.Add(action);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "JSON Parse Error: " + ex.Message;
            }
            return result;
        }
    }
}