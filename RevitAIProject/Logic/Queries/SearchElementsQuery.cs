using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitAIProject.Logic.Queries.Filters;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries.Searches
{
    /// <summary>
    /// A query that acts as a Station Master: builds, validates, and executes the filter pipeline.
    /// </summary>
    [AiParam("search_elements", Description = "Advanced search engine using a pipeline of filters.")]
    public class SearchElementsQuery : BaseSearchQuery
    {
        [AiParam("filters", Description = "List of filter instructions (Kind, Value, Extra)")]
        public JArray Filters { get; set; }

        protected override void Execute(IRevitContext context)
        {
            try
            {
                // 1. Build the train (using the Factory we created)
                var factory = new AiSearchFactory();
                var filters = factory.CreateLogic(Filters);

                // 2. LOGIC VALIDATION (The "Station Master" part)
                // If AI forgot the locomotive, we add Active View as default
                if (!filters.Any(f => f is ISearchInitializer))
                {
                    filters.Insert(0, new ActiveViewFilterInitializer());
                    Report("No search scope provided. Defaulted to Active View.", RevitMessageType.Warning);
                }

                // 3. EXECUTION (The Pipeline)
                FilteredElementCollector currentCollector = null;

                System.Diagnostics.Debug.WriteLine($"SearchAiName: {AssignAiName}", this.GetType().Name);

                foreach (var wagon in filters)
                {
                    // Each filter gets the Document and the current Collector
                    currentCollector = wagon.Apply(context.UIDoc.Document, currentCollector);

                    System.Diagnostics.Debug.WriteLine($"SearchAiName: {currentCollector.ToString()}", filters.GetType().Name);
                }

                // 4. RESULTS
                var foundIds = currentCollector?.ToElementIds().ToList() ?? new List<ElementId>();

                RegisterSearched(foundIds);

                System.Diagnostics.Debug.WriteLine($"Items found: {foundIds.Count}", this.GetType().Name);
            }
            catch (Exception ex)
            {
                Report($"Search Task failed: {ex.Message}", RevitMessageType.Error);
                System.Diagnostics.Debug.WriteLine($"[SearchElementsQuery Error]: {ex.Message}");
            }
        }
    }
}
