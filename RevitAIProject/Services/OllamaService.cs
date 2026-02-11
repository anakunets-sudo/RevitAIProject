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
            sb.Append("### ROLE: Revit 2019 AI Automation Agent (C# 7.3 / Revit API). ");
            sb.Append("### CORE MISSION: Convert user intent into Revit API Commands (Actions or Queries). ");

            // 2. ПРАВИЛО ВНУТРЕННЕГО ПЕРЕВОДА & КАТЕГОРИЙ
            sb.Append("### STEP 0: MANDATORY CATEGORY PROTOCOL. ");
            sb.Append("1. Before any analysis, ALWAYS translate the user's message into ENGLISH in your thought process. ");
            sb.Append("2. ALWAYS translate the user's element request into English (e.g., 'окна' -> 'Windows'). ");
            sb.Append("3. For 'categoryName', ALWAYS use the official Revit BuiltInCategory string starting with 'OST_'. ");
            sb.Append("Example: 'walls' -> 'OST_Walls', 'windows' -> 'OST_Windows', 'doors' -> 'OST_Doors'. ");

            // 3. ПРАВИЛО ОБРАБОТКИ ПУСТОГО РЕЗУЛЬТАТА
            sb.Append("### STEP 1: EMPTY RESULTS PROTOCOL. ");
            sb.Append("If the 'SYSTEM_REPORT' states that 0 elements were found, YOU MUST NOT REPEAT THE SEARCH COMMAND. ");
            sb.Append("Instead, provide a final text response in the user's language stating that nothing was found. ");

            // 4. STRICT FORMATTING
            sb.Append("### RESPONSE FORMAT RULES: ");
            sb.Append("1. Respond ONLY with a raw JSON object. No markdown code blocks (```json). ");
            sb.Append("2. PROHIBITED: No conversational preambles (e.g., 'Sure!', 'I will help'). ");
            sb.Append("3. Detect user language. Write 'message' in the SAME language as the user. ");

            // 5. ХРАНИЛИЩЕ ДАННЫХ (SESSION CONTEXT & CHAINING)
            sb.Append("### SESSION STORAGE (Context Management): ");
            sb.Append("1. 'storage': Use unique keys starting with '$q' for searches (e.g., '$q1' for windows, '$q2' for walls). ");
            sb.Append("2. To act on elements found in a previous step, set 'target_ai_name' to the corresponding key (e.g., '$q1'). ");
            sb.Append("3. For new elements, use 'assign_ai_name' with '$f' prefix (e.g., '$f1'). ");
            sb.Append("4. If multiple categories are requested, generate multiple 'GetElements' actions with unique 'search_ai_name' keys. ");

            // 6. EXECUTION STRATEGY
            sb.Append("### EXECUTION STRATEGY: ");
            sb.Append("1. [QUERIES]: Use 'GetElements' for 'Find', 'Count', 'Select'. ALWAYS provide 'search_ai_name'. ");
            sb.Append("2. [ACTIONS]: Use Action commands for 'Create', 'Move', 'Delete', 'SelectElements'. ");
            sb.Append("3. [CHAINING]: You can search and then act in one turn. Example: [Action1: GetElements($q1), Action2: SelectElements(target:$q1)]. ");

            // 7. ДИНАМИЧЕСКИЕ КОМАНДЫ (Reflection-based)
            sb.Append("### AVAILABLE COMMANDS & PARAMETERS: ");
            sb.Append(GetDynamicCommandsDescription());

            // 8. ПРАВИЛА ПАРАМЕТРОВ
            sb.Append("### PARAMETER RULES: ");
            sb.Append("1. UNITS: Always include units for 'double' types (e.g., '300mm', '10ft'). ");
            sb.Append("2. FILTERS: 'filterJson' must be a double-escaped JSON string. ");

            // 9. ФОРМАТ ОТВЕТА (Strict JSON Schema)
            sb.Append("### RESPONSE SCHEMA: ");
            sb.Append("{ ");
            sb.Append("  \"message\": \"Description in user language\", ");
            sb.Append("  \"actions\": [ ");
            sb.Append("    { \"action\": \"CommandName\", \"params\": { \"categoryName\": \"OST_Walls\", \"search_ai_name\": \"$q1\" } } ");
            sb.Append("  ] ");
            sb.Append("} ");

            return sb.ToString();
        }

        private string GetExamples()
        {
            StringBuilder sb = new StringBuilder();

            // Example 1: Basic Counting (Query)
            // Переход на OST_Walls и именование через $q1
            sb.AppendLine("User: 'How many walls are in the project?'");
            sb.AppendLine(@"Response: { ""message"": ""Searching for all walls in the project..."", ""actions"": [ { ""action"": ""GetElements"", ""params"": { ""categoryName"": ""OST_Walls"", ""search_ai_name"": ""$q1"" } } ] }");

            // Example 2: Filtering by Parameter (Complex Query)
            // Использование OST_Doors и строкового имени категории
            sb.AppendLine("User: 'Find all doors on Level 1'");
            sb.AppendLine(@"Response: { ""message"": ""Searching for doors on Level 1."", ""actions"": [ { ""action"": ""GetElements"", ""params"": { ""categoryName"": ""OST_Doors"", ""search_ai_name"": ""$q1"", ""filterJson"": ""[{\""p\"":\""Level\"",\""o\"":\""equals\"",\""v\"":\""Level 1\""}]"" } } ] }");

            // Example 3: Sequence Query -> Action (Using storage)
            // Связка: Найти в $q1 -> Удалить содержимое $q1
            sb.AppendLine("User: 'Find 300mm walls and delete them'");
            sb.AppendLine(@"Response: { ""message"": ""Finding 300mm walls and deleting them from the model."", ""actions"": [ " +
                          @"{ ""action"": ""GetElements"", ""params"": { ""categoryName"": ""OST_Walls"", ""search_ai_name"": ""$q1"", ""filterJson"": ""[{\""p\"":\""Width\"",\""o\"":\""equals\"",\""v\"":\""300mm\""}]"" } }, " +
                          @"{ ""action"": ""DeleteElements"", ""params"": { ""target_ai_name"": ""$q1"" } } ] }");

            // Example 4: Создание с именованием ($f1)
            sb.AppendLine("User: 'Плита со смещением 500 мм'");
            sb.AppendLine(@"Response: { ""message"": ""Создаю перекрытие ($f1) со смещением 500мм."", ""actions"": [ { ""action"": ""CreateFloor"", ""params"": { ""assign_ai_name"": ""$f1"", ""offset"": ""500mm"" } } ] }");

            // Example 5: Цепочка (Создание + Перемещение)
            // Использование созданной переменной $f1 во второй команде
            sb.AppendLine("User: 'Создай пол и сдвинь его влево на 1 метр'");
            sb.AppendLine(@"Response: { ""message"": ""Создаю пол ($f1) и сдвигаю его на 1 метр влево."", ""actions"": [ " +
                          @"{ ""action"": ""CreateFloor"", ""params"": { ""assign_ai_name"": ""$f1"", ""offset"": ""0mm"" } }, " +
                          @"{ ""action"": ""MoveElement"", ""params"": { ""target_ai_name"": ""$f1"", ""dx"": ""-1000mm"", ""dy"": ""0mm"", ""dz"": ""0mm"" } } ] }");

            // Example 6: Работа с выделением (пустой target_ai_name)
            sb.AppendLine("User: 'Сдвинь это вверх на 200мм'");
            sb.AppendLine(@"Response: { ""message"": ""Сдвигаю выбранный элемент на 200мм вверх."", ""actions"": [ { ""action"": ""MoveElement"", ""params"": { ""target_ai_name"": """", ""dx"": ""0mm"", ""dy"": ""200mm"", ""dz"": ""0mm"" } } ] }");

            // Example 7: Несколько категорий сразу (Множественный поиск)
            // Демонстрируем ИИ, как разделять окна и стены по разным ключам
            sb.AppendLine("User: 'Найди все окна и все стены'");
            sb.AppendLine(@"Response: { ""message"": ""Ищу все окна и стены в проекте."", ""actions"": [ " +
                          @"{ ""action"": ""GetElements"", ""params"": { ""categoryName"": ""OST_Windows"", ""search_ai_name"": ""$q1"" } }, " +
                          @"{ ""action"": ""GetElements"", ""params"": { ""categoryName"": ""OST_Walls"", ""search_ai_name"": ""$q2"" } } ] }");

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

            // --- СЕКЦИЯ 1: КОМАНДЫ (Actions & Queries из Примеров 1 и 2) ---
            sb.AppendLine("### AVAILABLE COMMANDS (JSON 'actions' list):");
            var commandTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<Logic.AiParamAttribute>() != null
                       && !t.Name.Contains("SessionContext")); // Исключаем сам контекст из списка команд

            foreach (var type in commandTypes)
            {
                var classAttr = type.GetCustomAttribute<Logic.AiParamAttribute>();
                string category = typeof(Logic.Queries.IRevitQuery).IsAssignableFrom(type) ? "[QUERY]" : "[ACTION]";

                sb.AppendLine($"- **{classAttr.Name}** {category}: {classAttr.Description}");

                // Собираем параметры свойств (например, offset из Примера 1)
                var props = type.GetProperties().Where(p => p.GetCustomAttribute<Logic.AiParamAttribute>() != null);
                foreach (var prop in props)
                {
                    var pAttr = prop.GetCustomAttribute<Logic.AiParamAttribute>();
                    sb.AppendLine($"  * {pAttr.Name} ({prop.PropertyType.Name}): {pAttr.Description}");
                }
                sb.AppendLine();
            }

            // --- СЕКЦИЯ 2: ХРАНИЛИЩЕ (SessionContext из Примера 3) ---
            sb.AppendLine("### DATA STORAGE (Where results are saved):");
            var contextProps = typeof(Services.SessionContext).GetProperties()
                .Where(p => p.GetCustomAttribute<Logic.AiParamAttribute>() != null);

            foreach (var prop in contextProps)
            {
                var attr = prop.GetCustomAttribute<Logic.AiParamAttribute>();
                sb.AppendLine($"- {attr.Name}: {attr.Description}");
            }

            return sb.ToString();
        }

        private AiResponse ParseToAiResponse(string rawJson)
        {
            var result = new AiResponse { Message = "", Actions = new List<Logic.IRevitLogic>() };
            try
            {
                string json = CleanJson(rawJson);
                JObject data = JObject.Parse(json);

                result.Message = data.SelectToken("$.message")?.ToString() ?? "Processing...";

                var actionsArray = data.SelectToken("$.actions") as JArray;
                if (actionsArray != null)
                {
                    foreach (JObject token in actionsArray)
                    {
                        // В твоем JSON обычно ключ "action", а не "name"
                        string actionName = (token["action"] ?? token["Action"] ?? token["name"])?.ToString();

                        // Извлекаем блок "params" (например, { "offset": 100 })
                        var parameters = token["params"] ?? token["Params"];

                        // Фабрика создает объект и СРАЗУ заполняет его свойства через рефлексию (используя AiParam)
                        var logicInstance = LogicFactory.CreateLogic(actionName, parameters);

                        if (logicInstance != null)
                            result.Actions.Add(logicInstance);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "AI Response parsing failed. Please check JSON format. Error: " + ex.Message;
            }
            return result;
        }
    }
}