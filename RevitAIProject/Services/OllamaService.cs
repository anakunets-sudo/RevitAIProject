using Autodesk.Revit.DB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitAIProject.Logic.Queries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

            // 1. ROLE & CORE MISSION (Без изменений)
            sb.Append("### ROLE: Revit 2019 AI Automation Agent (C# 7.3 / Revit API). ");
            sb.Append("### CORE MISSION: Convert user intent into Revit API Commands (Actions or Queries). ");

            // 2. ПРАВИЛО ВНУТРЕННЕГО ПЕРЕВОДА & КАТЕГОРИЙ (Без изменений)
            sb.Append("### STEP 0: MANDATORY CATEGORY PROTOCOL. ");
            sb.Append("1. Before any analysis, ALWAYS translate the user's message into ENGLISH in your thought process. ");
            sb.Append("2. ALWAYS translate the user's element request into English (e.g., 'окна' -> 'Windows'). ");
            sb.Append("3. For 'categoryName', ALWAYS use the official Revit BuiltInCategory string starting with 'OST_'. ");
            sb.Append("Example: 'walls' -> 'OST_Walls', 'windows' -> 'OST_Windows', 'doors' -> 'OST_Doors'. ");

            // 3. ПРАВИЛО ОБРАБОТКИ ПУСТОГО РЕЗУЛЬТАТА (Без изменений)
            sb.Append("### STEP 1: EMPTY RESULTS PROTOCOL. ");
            sb.Append("If the 'SYSTEM_REPORT' states that 0 elements were found, YOU MUST NOT REPEAT THE SEARCH COMMAND. ");
            sb.Append("Instead, provide a final text response in the user's language stating that nothing was found. ");

            // 4. STRICT FORMATTING (Без изменений)
            sb.Append("### RESPONSE FORMAT RULES: ");
            sb.Append("1. Respond ONLY with a raw JSON object. No markdown code blocks (```json). ");
            sb.Append("2. PROHIBITED: No conversational preambles (e.g., 'Sure!', 'I will help'). ");
            sb.Append("3. Detect user language. Write 'message' in the SAME language as the user. ");

            // 5. ХРАНИЛИЩЕ ДАННЫХ (ОБНОВЛЕНО: Железобетонное правило 1:1)

            sb.Append("### SESSION STORAGE (Context Management): ");            

            sb.Append("1. 'storage': Use unique keys starting with '$q' (e.g., '$q1', '$q2'). ");
            sb.Append("MANDATORY: When calling CreateGlobalQuery or CreateViewQuery, you MUST ALWAYS provide the search_ai_name(e.g., '$q1') immediately in the parameters of that action. NEVER leave Create...Query parameters empty. The name in Create must match the name in the following ByCategoryQuery.");
            sb.Append("2. **NAMED SESSIONS**: Each '$q' key is an independent search session. ");
            sb.Append("3. You can maintain multiple active searches (e.g., $q1 for Windows, $q2 for Walls) simultaneously without interference. ");
            sb.Append("4. To act on found elements, set 'target_ai_name' to the corresponding key (e.g., '$q1'). ");

            // 6. SEARCH ALGORITHM (ОБНОВЛЕНО: Логика выбора Scope)
            sb.Append("### SEARCH ALGORITHM (THE FUNNEL): ");
            sb.Append("1. **HYBRID SEARCH (Fastest)**: For simple requests, use 'CreateGlobalQuery' or 'CreateViewQuery' with 'categoryName' and 'search_ai_name' in a SINGLE action. ");
            sb.Append("2. **CHAINED SEARCH (Detailed)**: Use the chain [Create...Query] -> [ByCategoryQuery] -> [ByLevelQuery] ONLY if you need complex multi-stage filtering. ");
            sb.Append("3. **CLASS vs CATEGORY**: Use 'className' for Revit Classes (e.g., 'Wall', 'WallType') and 'categoryName' for official 'OST_' categories. ");
            sb.Append("4. **SPATIAL**: Use 'ByLevelQuery' for floor-based filtering. Call 'GetLevelsAction' first if Level IDs are unknown. ");

            // 7. EXECUTION STRATEGY (ОБНОВЛЕНО: Шаблон для Multi-Search)
            sb.Append("### EXECUTION STRATEGY: ");
            sb.Append("1. [QUERIES]: For 'Find' or 'Count', use THE FUNNEL: [Create] -> [Filters]. ");
            sb.Append("Example for two variables '$q': [CreateGlobal] -> [ByCategory(Walls, $q1)] -> [CreateActiveView] -> [ByCategory(Doors, $q2)]. ");


            // Правило для действий: Create, Move, Delete
            sb.Append("2. [ACTIONS]: Use Action commands for 'Create', 'Move', 'Delete', 'SelectElements'. ");
            sb.Append("For new elements, use 'target_ai_name' with '$f' prefix (e.g., '$f1'). ");

            // Правило для связок (Chaining): Поиск + Действие
            sb.Append("3. [CHAINING]: You MUST chain search and actions. ");
            sb.Append("Example sequence: [CreateActiveViewQuery] -> [ByCategorySearchQuery] -> [MoveElementAction(target_ai_name: '$q1')]. ");

            // Правило для пустой выборки (Selection)
            sb.Append("4. [SELECTION]: If 'target_ai_name' is empty, the action applies to the user's current manual selection in Revit UI. ");

            // 8. ДИНАМИЧЕСКИЕ КОМАНДЫ (Reflection-based)
            sb.Append("### AVAILABLE COMMANDS & PARAMETERS: ");
            sb.Append(GetDynamicCommandsDescription());

            // 8. ПРАВИЛА ПАРАМЕТРОВ
            sb.Append("### PARAMETER RULES: ");
            sb.Append("1. **CATEGORY NAMING**: Always use 'OST_' prefix for 'categoryName' (e.g., 'OST_Walls'). ");
            sb.Append("2. **CLASS NAMING**: NEVER use 'OST_' for 'className'. Use pure Revit API class names (e.g., 'FamilyInstance', 'View', 'Wall'). ");
            sb.Append("3. **UNITS**: Always include units for 'double' types (e.g., '300mm', '10ft'). ");
            sb.Append("4. **FILTERS**: 'filterJson' must be a double-escaped JSON string. ");

            // 10. ФОРМАТ ОТВЕТА (Strict JSON Schema)
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

            // Example 1: Basic Counting (Уточняем комментарий)
            sb.AppendLine("User: 'How many walls are in the project?'");
            sb.AppendLine(@"Response: { ""message"": ""Counting all walls in the project..."", ""actions"": [ 
{ ""action"": ""CreateGlobalQuery"" }, 
{ ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Walls"", ""search_ai_name"": ""$q1"" } 
] }");
            // Example 1.1: Basic Counting (Уточняем комментарий)
            sb.AppendLine("User: 'How many floors are there in the view and wall types in the project?'");
            sb.AppendLine(@"Response: { ""message"": ""Counting floors in the view and the types of walls in the project..."", ""actions"": [ 
{ ""action"": ""CreateActiveViewQuery"" }, 
{ ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Floors"", ""search_ai_name"": ""$q1"" } 
{ ""action"": ""CreateGlobalQuery"" }, 
{ ""action"": ""ByClassQuery"", ""className"": ""WallType"", ""search_ai_name"": ""$q2"" } 
] }");

            // Example 2: Filtering by Level (Показываем осознанный выбор InitActiveView)
            sb.AppendLine("User: 'Find all doors on Level 1'");
            sb.AppendLine(@"Response: { ""message"": ""Identifying Level 1 and searching on current view."", ""actions"": [ 
{ ""action"": ""GetLevelsAction"" }, 
{ ""action"": ""CreateActiveViewQuery"" }, 
{ ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Doors"" }, 
{ ""action"": ""ByLevelQuery"", ""levelIdString"": ""REPLACE_WITH_LEVEL_ID_FROM_RESULT"", ""search_ai_name"": ""$q1"" } 
] }");

            // Example 3: Complex Filtering (Funnel + Parameter JSON)
            sb.AppendLine("User: 'Find 300mm walls and delete them'");
            sb.AppendLine(@"Response: { ""message"": ""Finding 300mm walls to delete them."", ""actions"": [ 
        { ""action"": ""CreateGlobalQuery"" }, 
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

            // НОВЫЙ Example 7: Множественный поиск (Демонстрация INDEPENDENT CHAINS)
            // Это самый важный пример для твоей текущей задачи!
            sb.AppendLine("User: 'Count all windows on this view and all doors in the project'");
            sb.AppendLine(@"Response: { ""message"": ""Counting windows on view and doors globally."", ""actions"": [ 
{ ""action"": ""CreateActiveViewQuery"" }, 
{ ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Windows"", ""search_ai_name"": ""$q1"" },
{ ""action"": ""CreateGlobalQuery"" }, 
{ ""action"": ""ByCategoryQuery"", ""categoryName"": ""OST_Doors"", ""search_ai_name"": ""$q2"" } 
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