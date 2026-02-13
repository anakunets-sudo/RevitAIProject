using Newtonsoft.Json.Linq;
using RevitAIProject.Logic.Queries.Filters;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json.Linq;
    using RevitAIProject.Services; // Убедись, что TypeRegistry здесь

    /// <summary>
    /// Factory that constructs and configures search filter chains from AI-provided JSON data.
    /// Uses a centralized TypeRegistry for fast filter discovery and reflection for property mapping.
    /// </summary>
    public class AiSearchFactory
    {
        /// <summary>
        /// Transforms JArray into a sorted sequence of Revit filters using the TypeRegistry cache.
        /// </summary>
        /// <param name="instructions">JSON array containing filter definitions (Kind, Value, Extra).</param>
        /// <returns>A list of configured ISearchFilter objects sorted by execution priority.</returns>
        public List<ISearchFilter> CreateLogic(JArray instructions)
        {
            var train = new List<ISearchFilter>();
            if (instructions == null) return train;

            // Using the centralized service to get all available filter types
            var cachedFilterTypes = TypeRegistry.GetFilterTypes();

            foreach (var token in instructions)
            {
                string kind = token["Kind"]?.ToString();
                if (string.IsNullOrEmpty(kind)) continue;

                // Instant lookup in the registry
                if (cachedFilterTypes.TryGetValue(kind.ToLower(), out Type filterType))
                {
                    // Create instance of the specific filter class
                    var filter = (ISearchFilter)Activator.CreateInstance(filterType);

                    // Map JSON data to properties marked with [AiParam]
                    MapJsonToFilterProperties(filter, token);

                    train.Add(filter);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AiSearchFactory]: Unknown filter kind '{kind}' skipped.");
                }
            }

            // Return the "train" sorted: Initializers(0) -> Fast(1-5) -> Slow(10+)
            return train.OrderBy(f => f.Priority).ToList();
        }

        /// <summary>
        /// Fills filter properties based on JToken data by matching [AiParam] attributes.
        /// Supports automatic unit conversion to Revit internal feet for double values.
        /// </summary>
        /// <param name="filter">The filter instance to configure.</param>
        /// <param name="data">The JSON data source for this filter.</param>
        private void MapJsonToFilterProperties(ISearchFilter filter, JToken data)
        {
            if (data == null || filter == null) return;

            PropertyInfo[] properties = filter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                var attr = prop.GetCustomAttribute<AiParamAttribute>();
                if (attr == null) continue;

                // Priority: Explicit Name in attribute -> Property Name
                string targetKey = attr.Name ?? prop.Name;

                // Search data in the specific key or fallback to generic Value/Extra fields
                JToken jsonValue = data[targetKey] ?? data["Value"] ?? data["Extra"];

                if (jsonValue != null && jsonValue.Type != JTokenType.Null)
                {
                    try
                    {
                        string stringValue = jsonValue.ToString().Trim();
                        if (string.IsNullOrEmpty(stringValue)) continue;

                        if (prop.PropertyType == typeof(double))
                        {
                            // Delegate unit conversion to your powerful LogicFactory logic
                            prop.SetValue(filter, LogicFactory.ParseToRevitFeet(stringValue));
                        }
                        else if (prop.PropertyType == typeof(string))
                        {
                            prop.SetValue(filter, stringValue);
                        }
                        else if (prop.PropertyType == typeof(bool))
                        {
                            prop.SetValue(filter, bool.Parse(stringValue.ToLower()));
                        }
                        else
                        {
                            // Generic conversion for Enums and other types
                            prop.SetValue(filter, jsonValue.ToObject(prop.PropertyType));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mapping Error]: Failed to set {prop.Name} in {filter.GetType().Name}. {ex.Message}");
                    }
                }
            }
        }
    }
}
