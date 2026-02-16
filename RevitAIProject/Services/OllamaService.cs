using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitAIProject.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434/api/generate";
        private readonly IExperienceRepository _experienceRepo;
        private readonly ISessionContext _sessionContext;

        public OllamaService(IExperienceRepository experienceRepo, ISessionContext sessionContext)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _experienceRepo = experienceRepo;
            _sessionContext = sessionContext;

        }

        public async Task<AiResponse> GetAiResponseAsync(string userMessage, ISessionContext sessionContext, CancellationToken ct)
        {
            try
            {
                string fullPrompt = GetSystemInstructions() +
                                    GetLearnedInstructions() +
                                    GetExamples() +
                                    "\n\n### USER REQUEST:\n" + userMessage;

                var requestData = new
                {
                    model = "qwen2.5:7b",
                    prompt = fullPrompt,
                    format = "json",
                    stream = false,
                    options = new { temperature = 0.0 }
                };

                string jsonPayload = JsonConvert.SerializeObject(requestData);
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(OllamaUrl, content, ct);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                JObject ollamaJson = JObject.Parse(responseBody);
                string rawAiResponse = ollamaJson["response"]?.ToString() ?? "{}";

                Debug.WriteLine(rawAiResponse, "AI Raw Output");
                return ParseToAiResponse(rawAiResponse);
            }
            catch (Exception ex)
            {
                return new AiResponse { Message = $"Error: {ex.Message}", Actions = new List<IRevitLogic>() };
            }
        }
        private string GetSystemInstructions()
        {
            // Динамические данные из живой сессии
            string currentVars = _sessionContext.Storage.Keys.Any()
                ? string.Join(", ", _sessionContext.Storage.Keys)
                : "None (Storage is empty)";

            string lastReports = string.Join(" | ", _sessionContext.GetAiMessages());

            return "### ROLE: Revit 2019 AI Automation Agent (C# 7.3 / Revit API).\n" +
                   "### CORE MISSION: \r\n1. Convert user intent ONLY into pre-defined Revit API Actions.\r\n2. STRICT RULE: If no specialized Action matches the user's request, DO NOT generate code. \r\n3. RESPONSE PROTOCOL: Politely inform the user that the requested function is currently \r\n   not supported and suggest available alternatives from your Action library \r\n   (e.g., searching, deleting, or moving elements)."+

                   "### STEP 1: CONTEXT AWARENESS (LIVE DATA):\n" +
                   "- CURRENT SESSION STORAGE: [" + currentVars + "]\n" +
                   "- LAST SYSTEM FEEDBACK: " + (string.IsNullOrEmpty(lastReports) ? "System Ready" : lastReports) + "\n\n" +

                   "### STEP 2: MANDATORY CATEGORY PROTOCOL:\n" +
                   "1. Translate intent to ENGLISH. For 'category_name', use BuiltInCategory string starting with 'OST_'.\n" +

                   "### STEP 3: RESPONSE FORMAT RULES:\n" +
                   "1. Respond ONLY with raw JSON. No markdown. No preambles.\n" +
                   "2. 'message' in user language. All other keys strictly as defined.\n" +

                   "### STEP 4: JSON STRUCTURE:\n" +
                   "Always use: { \"message\": \"...\", \"actions\": [ { \"action\": \"...\", \"Parameters\": {...} } ] }\n" +

                   "### STEP 5: VARIABLE NAMING CONVENTION:\n" +
                   "1. 'assign_ai_name': For 'search_elements' results. Use '$a1', '$a2'.\n" +
                   "2. 'assign_ai_name': When CREATING new elements. Use '$a1', '$a2'.\n" +
                   "3. 'target_ai_name': To reference '$f' from STORAGE. If empty, system uses manual selection.\n" +

                   "### STEP 8: ADVANCED SEARCH ENGINE (search_elements):\n" +
                    "1. MANDATORY: Start with Scope: 'scope_project', 'scope_active_view' or 'scope_selection'.\n" +
                    "2. SELECTION: If user says 'this', 'selected' or 'highlighted', use 'scope_selection'.\n" +
                    "3. CHAINING: If you search to delete/modify, the 'assign_ai_name' (e.g. $a1) MUST be passed to the next action's 'target_ai_name'.\n" + 
                    "4. SEARCH RULE: Always search for different categories into DIFFERENT variables (e.g., Windows to \r\na2) to avoid manual filtering in C#."+

                   GetDynamicCommandsDescription() + "\n\n";
                   
        }

        private string GetExamples()
        {
            return "[EXAMPLES]\n" +
                // Case 1: Multiple entities -> Default to View
                "User: 'Find windows and walls'\n" +
                "Response: { \"message\": \"Searching for windows and walls on current view.\", \"actions\": [ " +
                "{ \"action\": \"search_elements\", \"Parameters\": { \"assign_ai_name\": \"$a1\", \"filters\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": \"OST_Windows\" } ] } }, " +
                "{ \"action\": \"search_elements\", \"Parameters\": { \"assign_ai_name\": \"$q2\", \"filters\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": \"OST_Walls\" } ] } } ] }\n" +

                // Case 2: Mixed context
                "User: 'Find doors on view and walls in project'\n" +
                "Response: { \"message\": \"Searching doors on view and walls globally.\", \"actions\": [ " +
                "{ \"action\": \"search_elements\", \"Parameters\": { \"assign_ai_name\": \"$a1\", \"filters\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": \"OST_Doors\" } ] } }, " +
                "{ \"action\": \"search_elements\", \"Parameters\": { \"assign_ai_name\": \"$a2\", \"filters\": [ { \"Kind\": \"scope_project\" }, { \"Kind\": \"category\", \"Value\": \"OST_Walls\" } ] } } ] }\n" +

                // Case 3: Chaining (Search + Move)
                "User: 'Find walls and move them up 500mm'\n" +
                "Response: { \"message\": \"Finding walls and moving them.\", \"actions\": [ " +
                "{ \"action\": \"search_elements\", \"Parameters\": { \"assign_ai_name\": \"$a1\", \"filters\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": \"OST_Walls\" } ] } }, " +
                "{ \"action\": \"move_elements\", \"Parameters\": { \"target_ai_name\": \"$a1\", \"dz\": \"500mm\" } } ] }\n" +

                // Case 4: Creation with Naming ($f1)
                "User: 'Создай плиту со смещением 500мм'\n" +
                "Response: { \"message\": \"Создаю плиту ($a1) со смещением 500мм\", \"actions\": [ " +
                "{ \"action\": \"create_floor\", \"Parameters\": { \"assign_ai_name\": \"$a1\", \"offset\": \"500mm\" } } ] }\n" +

                // Case 5: Selection-based Action
                "User: 'Подними выделенное на 20 футов'\n" +
                "Response: { \"message\": \"Поднимаю выделенное на 20 футов.\", \"actions\": [ " +
                "{ \"action\": \"move_elements\", \"Parameters\": { \"target_ai_name\": \"\", \"dz\": \"20ft\" } } ] }\n" +
                "[/EXAMPLES]";
        }

        private string GetDynamicCommandsDescription()
        {
            StringBuilder sb = new StringBuilder();

            // 1. Описание фильтров для поиска (Kind)
            sb.Append("\n### 1. SEARCH FILTERS (Inside 'filters' array): ");
            var filterTypes = TypeRegistry.GetFilterTypes();
            foreach (var entry in filterTypes)
            {
                var attr = entry.Value.GetCustomAttribute<AiParamAttribute>();
                // Используем entry.Key, так как он уже нормализован в TypeRegistry
                sb.Append($"- Kind: {entry.Key} (Description: {attr?.Description ?? "No description"}). ");
            }

            // 2. Описание глобальных действий и их параметров
            sb.Append("\n### 2. GLOBAL ACTIONS: ");
            var logicTypes = TypeRegistry.GetLogicTypes();

            // Distinct, так как один класс может быть зарегистрирован под разными ключами
            foreach (var type in logicTypes.Values.Distinct())
            {
                var classAttr = type.GetCustomAttribute<AiParamAttribute>();
                if (classAttr == null) continue;

                // Имя команды (например, create_floor) и её описание
                sb.Append($"\n- Action: '{classAttr.Name}' (Description: {classAttr.Description}). ");
                sb.Append("Parameters: ");

                // Собираем только свойства, помеченные [AiParam]
                var props = type.GetProperties()
                    .Select(p => p.GetCustomAttribute<AiParamAttribute>())
                    .Where(a => a != null);

                foreach (var pAttr in props)
                {
                    // Используем pAttr.Name — это то, что ИИ должен написать в JSON
                    sb.Append($"{pAttr.Name}, ");
                }
            }

            return sb.ToString();
        }

        private string GetLearnedInstructions()
        {
            var records = _experienceRepo.GetLearningSet(10);
            if (records == null || !records.Any()) return "";

            StringBuilder sb = new StringBuilder("\n### LEARNED PATTERNS:\n");
            foreach (var r in records)
            {
                string status = r.Rating >= 1 ? "STRICTLY FOLLOW" : "AVOID";
                sb.AppendLine($"{status}: Request '{r.UserPrompt}' -> JSON: {r.AiJson}");
            }
            return sb.ToString();
        }

        private AiResponse ParseToAiResponse(string rawJson)
        {
            // Сохраняем сырой JSON для истории/обучения
            var result = new AiResponse { RawJson = rawJson, Actions = new List<IRevitLogic>() };

            try
            {
                string json = CleanJson(rawJson);
                JObject data = JObject.Parse(json);

                // Извлекаем сообщение для пользователя
                result.Message = data["message"]?.ToString() ?? "Processing AI Intent...";

                if (data["actions"] is JArray actions)
                {
                    // Здесь нам нужен доступ к текущему контексту сессии (SessionContext)
                    // Предположим, он доступен через поле класса _sessionContext
                    foreach (var item in actions)
                    {
                        if (!(item is JObject actionObj)) continue;

                        // Извлекаем имя экшена (поддерживаем разные стили от ИИ)
                        string name = (actionObj["action"] ?? actionObj["Action"] ?? actionObj["name"])?.ToString();

                        // Извлекаем блок параметров
                        JToken pars = actionObj["Parameters"] ?? actionObj["params"] ?? actionObj["Params"] ?? actionObj;

                        // КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Передаем _sessionContext в фабрику
                        // Теперь фабрика сама вызовет .SetContext() у созданной команды
                        var logic = LogicFactory.CreateLogic(name, pars, _sessionContext);

                        if (logic != null)
                        {
                            result.Actions.Add(logic);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "AI Response parsing failed: " + ex.Message;
            }

            return result;
        }

        private string CleanJson(string raw)
        {
            raw = raw.Replace("```json", "").Replace("```", "").Trim();
            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            return (start != -1 && end > start) ? raw.Substring(start, end - start + 1) : raw;
        }
    }


    /*public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434/api/generate";
        private readonly IExperienceRepository _experienceRepo;

        public OllamaService(IExperienceRepository experienceRepo)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _experienceRepo = experienceRepo;
        }

        public async Task<AiResponse> GetAiResponseAsync(string userMessage, CancellationToken ct)
        {
            try
            {
                string fullPrompt = GetSystemInstructions() +
                           GetLearnedInstructions() + // <-- ПАМЯТЬ ТУТ
                           GetExamples() +
                           "\n\n### USER REQUEST:\n" + userMessage;

                var requestData = new
                {
                    model = "qwen2.5:7b",
                    prompt = fullPrompt,
                    format = "json",
                    stream = false,
                    options = new { temperature = 0.0 }
                };

                string jsonPayload = JsonConvert.SerializeObject(requestData);
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(OllamaUrl, content, ct);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                JObject ollamaJson = JObject.Parse(responseBody);
                string rawAiResponse = ollamaJson["response"]?.ToString() ?? "{}";

                Debug.WriteLine(rawAiResponse, "AI Raw Output");

                return ParseToAiResponse(rawAiResponse);
            }
            catch (OperationCanceledException)
            {
                return new AiResponse { Message = "Генерация прервана пользователем.", Actions = new List<IRevitLogic>() };
            }
            catch (Exception ex)
            {
                return new AiResponse { Message = $"Ошибка: {ex.Message}", Actions = new List<IRevitLogic>() };
            }
        }
        private string GetSystemInstructions()
        {
            return "### ROLE: Revit 2019 AI Automation Agent (C# 7.3 / Revit API). " +
                   "### CORE MISSION: Convert user intent into Revit API Commands. " +

                   "### STEP 1: MANDATORY CATEGORY PROTOCOL. " +
                   "1. Translate user intent to ENGLISH. " +
                   "2. For 'categoryName', ALWAYS use BuiltInCategory string starting with 'OST_'. " +
                   "### STEP 2: EMPTY RESULTS PROTOCOL. " +
                   "1. If 'SYSTEM_REPORT' shows 0 elements, DO NOT repeat search. State results in user language. " +
                   "### STEP 3: RESPONSE FORMAT RULES: " +
                   "1. Respond ONLY with raw JSON. No markdown. 2. No preambles. 3. 'message' in user language. " +

                   "### STEP 4: JSON STRUCTURE: "+
                   "Always respond with a JSON object containing: " +
                   "1. \"message\": A brief confirmation of what you are doing. " +
                   "2. \"actions\": An array of command objects." +

                   "### STEP 5: VARIABLE NAMING CONVENTION: " +
                   "1. 'search_ai_name': Used ONLY in 'search_elements'. Represents FOUND elements. Format: '$q1', '$q2'. MUST be unique for each search within a session. " +
                   "2. 'assign_ai_name': Used ONLY when CREATING new elements. Assigns a name to NEW objects. Format: '$f1', '$f2'. MUST be unique to avoid overwriting. " +
                   "3. 'target_ai_name': Target for Actions (Move, Delete, etc.). Use '$q' for found or '$f' for created elements. " +

                   "### STEP 6: ADVANCED SEARCH ENGINE (search_elements): " +
                   "This is a pipeline search. 'filters' is a JArray of steps. " +
                   "1. MANDATORY: Every search block MUST start with a Scope: 'scope_project' or 'scope_active_view'. " +
                   "2. SCOPE LOGIC: Determine scope for EACH entity individually. " +
                   "3. DEFAULT RULE: If context is unclear, ALWAYS use 'scope_active_view'. " +
                   "4. SPECIFIC RULES: Use 'scope_project' ONLY if user says 'in project', 'everywhere', 'all'. " +
                   "5. UNIQUE ID: Ensure 'search_ai_name' incrementing (e.g., use '$q2' if '$q1' is already defined). " +

                   "### STEP 7: ACTION RULES: " +
                   "1. Create: Needs 'categoryName', 'levelName' and 'assign_ai_name' (MUST be a new unique ID like '$f1'). " +
                   "2. Modify/Delete: MUST use 'target_ai_name' from a previous UNIQUE '$q' or '$f'. " +
                   "3. Units: Always include units (e.g. '300mm'). " +
                   GetDynamicCommandsDescription() +
                   "### STEP 7: RESPONSE SCHEMA: { \"message\": \"...\", \"actions\": [ { \"Action\": \"search_elements\", \"Parameters\": { \"search_ai_name\": \"$q1\", \"filters\": [...] } } ] }" +

                   "### STEP 8: PARAMETER MAPPING PROTOCOL: " +
                    "1. IGNORE C# property names (like 'TargetAiName'). " +
                    "2. USE ONLY names defined in [AiParam] attributes (target_ai_name')"+
                    "3. Use EXACT numbers from the request. If units (mm, in, ft) are provided, include them in the value string (e.g., '500mm'). "+

                    "### STEP 9: EMERGENCY DYNAMIC CODE PROTOCOL (FALLBACK): If NO existing Action or Query matches the user request, you MUST use 'dynamic_code'. 1. NO WRAPPERS: Write ONLY the logic body. 2. CONTEXT: 'doc' and 'uidoc' are PRE-DECLARED. 3. TRANSACTIONS: Always use: using (Transaction t = new Transaction(doc, \"AI Task\")) { t.Start(); ... t.Commit(); } 4. BRACKETS: Ensure every '{' is closed by '}' BEFORE starting an 'else' block. 5. NEW LINES: Use '\n' for every semicolon (;) to prevent bracket confusion."
                    ;
        }
        private string GetExamples()
        {
            return "[EXAMPLES] " +
                // Case 1: Multiple entities, no scope mentioned -> Default to View
                "User: 'Find windows and walls' " +
                "AI: { \"message\": \"Searching for windows and walls on current view.\", \"actions\": [ " +
                "{ \"Action\": \"search_elements\", \"Parameters\": { \"search_ai_name\": \"$q1\", \"filters\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": \"OST_Windows\" } ] } }, " +
                "{ \"Action\": \"search_elements\", \"Parameters\": { \"search_ai_name\": \"$q2\", \"filters\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": \"OST_Walls\" } ] } } ] } " +

                // Case 2: Mixed context (View and Project)
                "User: 'Find doors on view and walls in project' " +
                "AI: { \"message\": \"Searching doors on view and walls globally.\", \"actions\": [ " +
                "{ \"Action\": \"search_elements\", \"Parameters\": { \"search_ai_name\": \"$q1\", \"filters\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": \"OST_Doors\" } ] } }, " +
                "{ \"Action\": \"search_elements\", \"Parameters\": { \"search_ai_name\": \"$q2\", \"filters\": [ { \"Kind\": \"scope_project\" }, { \"Kind\": \"category\", \"Value\": \"OST_Walls\" } ] } } ] } " +

                // Case 3: Explicit Global scope
                "User: 'Select all windows in the building' " +
                "AI: { \"message\": \"Selecting all windows in the entire project.\", \"actions\": [ " +
                "{ \"action\": \"search_elements\", \"Parameters\": { \"search_ai_name\": \"$q1\", \"filters\": [ { \"Kind\": \"scope_project\" }, { \"Kind\": \"category\", \"Value\": \"OST_Windows\" } ] } } ] } " +
                "[/EXAMPLES]" +

                // Example 4: Chaining (Create + Move)
                "User: 'Find all doors on Level 1' " + 
                "Response: {  \"message\": \"Identifying Level 1 and searching on current view.\",  \"actions\": [  { \"action\": \"GetLevelsAction\" },  { \"action\": \"CreateActiveViewQuery\" },{  \"action\": \"search_elements\",  \" \"filter\": [ { \"Kind\": \"scope_active_view\" }, { \"Kind\": \"category\", \"Value\": ] } }  ] }" +

                // Example 5: Creation with Naming ($f1)
                "User:'Создай плиту со смещением 500 сантиметров' " +
                "Response:{\"message\":\"Создаю плиту ($f1) со смещением 50 см\",\"actions\":[{\"action\":\"CreateFloor\",\"Parameters\":{\"assign_ai_name\":\"$f1\",\"offset\":\"500см\"}}]}" +            

                // Example 6: Selection-based Action
                "User: 'Подними выделенное на 20 футов' " +
                "Response: { \"message\": \"Поднимаю выделенное на 20 футов.\", \"actions\": [ { \"action\": \"MoveElementAction\", \"target_ai_name\": \"\", \"dx\": 0, \"dy\": 0, \"dz\": 200ft } ] }" ;

        }
        private string GetDynamicCommandsDescription()
        {
            string desc = " ### 1. SEARCH FILTERS (Inside 'filters' array): ";
            var filterTypes = TypeRegistry.GetFilterTypes();
            foreach (var entry in filterTypes)
            {
                var attr = entry.Value.GetCustomAttribute<AiParamAttribute>();
                desc += "- Kind: " + entry.Key + " (" + (attr?.Description ?? "") + "). ";
            }

            desc += " ### 2. GLOBAL ACTIONS: ";
            var logicTypes = TypeRegistry.GetLogicTypes();
            foreach (var type in logicTypes.Values.Distinct())
            {
                var classAttr = type.GetCustomAttribute<AiParamAttribute>();
                if (classAttr == null) continue;
                desc += "- " + classAttr.Name + ": " + classAttr.Description + ". ";
                var props = type.GetProperties().Where(p => p.GetCustomAttribute<AiParamAttribute>() != null);
                foreach (var p in props) desc += p.Name + ", ";
            }
            return desc;
        }
        private string GetLearnedInstructions()
        {
            var records = _experienceRepo.GetLearningSet(10); // Берем 10 последних уроков
            if (records == null || !records.Any()) return "";

            string learned = "\n### USER FEEDBACK & LEARNED PATTERNS (Reinforcement Learning):\n";

            foreach (var record in records)
            {
                string confidence = "";
                string prefix = "";

                // Применяем твою шкалу вероятностей
                switch (record.Rating)
                {
                    case 2: confidence = "Confidence: 80% (GOLD STANDARD)"; prefix = "FOLLOW THIS PATTERN:"; break;
                    case 1: confidence = "Confidence: 60% (GOOD)"; prefix = "RECOMMENDED:"; break;
                    case -1: confidence = "Confidence: 40% (SUBOPTIMAL)"; prefix = "CRITICIZED ATTEMPT:"; break;
                    case -2: confidence = "Confidence: 20% (FAILING)"; prefix = "AVOID THIS LOGIC:"; break;
                    default: continue; // Нейтральные (0) не шлем
                }

                learned += $"Request: '{record.UserPrompt}'\n";
                learned += $"{prefix} {record.AiJson} ({confidence})\n\n";
            }

            learned += "INSTRUCTION: Use 'GOLD' patterns as priority and find alternatives for 'AVOID' patterns.\n";
            return learned;
        }
        /// <summary>
        /// Parses raw JSON from Ollama into an AiResponse object and preserves the original JSON for learning.
        /// </summary>
        private AiResponse ParseToAiResponse(string rawJson)
        {
            // Initializing the response and PRESERVING the raw input for the Experience Repository
            var result = new AiResponse
            {
                Message = "",
                Actions = new List<IRevitLogic>(),
                RawJson = rawJson // <--- This ensures AiJson won't be null in your JSON file
            };

            try
            {
                string json = CleanJson(rawJson);
                JObject data = JObject.Parse(json);

                // Extract natural language message for the UI
                result.Message = data.SelectToken("$.message")?.ToString() ?? "Processing...";

                var actionsArray = data.SelectToken("$.actions") as JArray;
                if (actionsArray != null)
                {
                    foreach (JToken token in actionsArray)
                    {
                        if (!(token is JObject actionObj)) continue;

                        // Flexible action name matching (Action/action/name)
                        string actionName = (actionObj["Action"] ?? actionObj["action"] ?? actionObj["name"])?.ToString();

                        // Flexible parameters matching (Parameters/params/Params or root)
                        JToken parameters = actionObj["Parameters"] ?? actionObj["params"] ?? actionObj["Params"] ?? actionObj;

                        var logicInstance = LogicFactory.CreateLogic(actionName, parameters);
                        if (logicInstance != null)
                        {
                            result.Actions.Add(logicInstance);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "AI Response parsing failed: " + ex.Message;
                // Even if parsing fails partially, we still keep the RawJson for debugging/logging
            }

            return result;
        }
        private string CleanJson(string raw)
        {
            raw = raw.Replace("```json", "").Replace("```", "").Trim();
            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start != -1 && end > start) return raw.Substring(start, end - start + 1);
            return raw;
        }
    }*/
}