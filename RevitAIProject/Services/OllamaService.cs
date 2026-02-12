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

            // 3. THE SEARCH FUNNEL (PRIORITY)
            sb.Append("### SEARCH ALGORITHM (THE FUNNEL): ");
            sb.Append("1. SCOPE: Call 'InitActiveViewQuery' (default) or 'InitGlobalQuery'. ");
            sb.Append("2. FAST FILTER: Apply 'ByCategoryQuery' (e.g., OST_Walls) or 'ByClassQuery'. ");
            sb.Append("3. SPATIAL: Use 'ByLevelQuery' for floor-based filtering. If Level IDs are unknown, call 'GetLevelsAction' first. ");
            sb.Append("4. DEEP FILTER: Use 'ByParamJsonQuery' for specific parameters (Mark, Material, etc.) using JSON rules. ");

            // 6. EXECUTION STRATEGY
            sb.Append("### EXECUTION STRATEGY: ");

            // Правило для запросов: напоминаем про Воронку (Init -> Category -> Filter)
            sb.Append("1. [QUERIES]: For 'Find', 'Count', or 'Identify', use THE FUNNEL sequence. ");
            sb.Append("Start with 'InitActiveViewQuery' (default) or 'InitGlobalQuery'. ");
            sb.Append("Always set 'search_ai_name' (e.g., '$q1') in the LAST query of the chain to save results. ");

            // Правило для действий: Create, Move, Delete
            sb.Append("2. [ACTIONS]: Use Action commands for 'Create', 'Move', 'Delete', 'SelectElements'. ");
            sb.Append("For new elements, use 'target_ai_name' with '$f' prefix (e.g., '$f1'). ");

            // Правило для связок (Chaining): Поиск + Действие
            sb.Append("3. [CHAINING]: You MUST chain search and actions. ");
            sb.Append("Example sequence: [InitActiveViewQuery] -> [ByCategoryQuery] -> [MoveElementAction(target_ai_name: '$q1')]. ");

            // Правило для пустой выборки (Selection)
            sb.Append("4. [SELECTION]: If 'target_ai_name' is empty, the action applies to the user's current manual selection in Revit UI. ");

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

            // Example 1: Basic Counting (Using the Funnel)
            sb.AppendLine("User: 'How many walls are in the project?'");
            sb.AppendLine(@"Response: { ""message"": ""Counting all walls in the project..."", ""actions"": [ 
        { ""action"": ""InitGlobalQuery"" }, 
        { ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Walls"", ""search_ai_name"": ""$q1"" } 
    ] }");

            // Example 2: Filtering by Level (Using GetLevelsAction + ByLevelQuery)
            sb.AppendLine("User: 'Find all doors on Level 1'");
            sb.AppendLine(@"Response: { ""message"": ""First, I will identify Level 1 ID, then find the doors."", ""actions"": [ 
        { ""action"": ""GetLevelsAction"" }, 
        { ""action"": ""InitActiveViewQuery"" }, 
        { ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Doors"" }, 
        { ""action"": ""ByLevelQuery"", ""levelIdString"": ""REPLACE_WITH_LEVEL_ID_FROM_RESULT"", ""search_ai_name"": ""$q1"" } 
    ] }");

            // Example 3: Complex Filtering (Funnel + Parameter JSON)
            sb.AppendLine("User: 'Find 300mm walls and delete them'");
            sb.AppendLine(@"Response: { ""message"": ""Finding 300mm walls to delete them."", ""actions"": [ 
        { ""action"": ""InitGlobalQuery"" }, 
        { ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Walls"" }, 
        { ""action"": ""ByParamJsonQuery"", ""filterJson"": ""[{\""p\"":\""Width\"",\""o\"":\""equals\"",\""v\"":\""300\""}]"", ""search_ai_name"": ""$q1"" }, 
        { ""action"": ""DeleteElementsAction"", ""target_ai_name"": ""$q1"" } 
    ] }");

            // Example 4: Creation with Naming ($f1)
            sb.AppendLine("User: 'Create a floor with 500mm offset'");
            sb.AppendLine(@"Response: { ""message"": ""Creating a new floor ($f1) with 500mm offset."", ""actions"": [ 
        { ""action"": ""CreateFloorAction"", ""offset"": 500, ""target_ai_name"": ""$f1"" } 
    ] }");

            // Example 5: Chaining (Create + Move)
            sb.AppendLine("User: 'Create a floor and move it left by 1 meter'");
            sb.AppendLine(@"Response: { ""message"": ""Creating floor ($f1) and moving it."", ""actions"": [ 
        { ""action"": ""CreateFloorAction"", ""target_ai_name"": ""$f1"" }, 
        { ""action"": ""MoveElementAction"", ""target_ai_name"": ""$f1"", ""dx"": -1000, ""dy"": 0, ""dz"": 0 } 
    ] }");

            // Example 6: Selection-based Action
            sb.AppendLine("User: 'Move selected elements up by 200mm'");
            sb.AppendLine(@"Response: { ""message"": ""Moving your current selection up."", ""actions"": [ 
        { ""action"": ""MoveElementAction"", ""target_ai_name"": """", ""dx"": 0, ""dy"": 0, ""dz"": 200 } 
    ] }");

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
                        // Извлекаем имя экшена
                        string actionName = (token["action"] ?? token["Action"] ?? token["name"])?.ToString();

                        // ИСПРАВЛЕНИЕ: Если ИИ прислал плоский JSON, передаем сам 'token'.
                        // Если прислал вложенный, берем 'params'.
                        JToken parameters = token["params"] ?? token["Params"] ?? token;

                        var logicInstance = LogicFactory.CreateLogic(actionName, parameters);

                        if (logicInstance != null)
                            result.Actions.Add(logicInstance);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "AI Response parsing failed: " + ex.Message;
            }
            return result;
        }
    }
}