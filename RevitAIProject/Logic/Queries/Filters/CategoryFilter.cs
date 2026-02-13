using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries.Filters
{
    /// <summary>
    /// Fast filter for Revit BuiltInCategories. 
    /// Usually applied after ClassFilter to narrow down the element collection.
    /// </summary>
    public class CategoryFilter : ISearchFilter
    {
        /// <summary>
        /// Priority 2: Runs after ClassFilter (1) but before slow parameter filters (10).
        /// </summary>
        public int Priority => 2;

        [AiParam("categoryName", Description = "Revit BuiltInCategory name (e.g. OST_Walls)")]
        public string CategoryName { get; set; }

        /// <summary>
        /// Applies category filter to the collector.
        /// </summary>
        public FilteredElementCollector Apply(Document doc, FilteredElementCollector collector)
        {
            var bic = ResolveCategory(CategoryName);

            if (bic == BuiltInCategory.INVALID) return collector;

            return collector.OfCategory(bic);
        }

        /// <summary>
        /// Resolves string category name into BuiltInCategory enum with fuzzy matching.
        /// </summary>
        protected BuiltInCategory ResolveCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return BuiltInCategory.INVALID;

            string target = categoryName.Trim().Replace("\"", "").Replace("'", "");

            var variants = new List<string> { target };
            if (!target.StartsWith("OST_")) variants.Add("OST_" + target);
            if (!target.EndsWith("s")) variants.Add(target + "s");

            var allNames = Enum.GetNames(typeof(BuiltInCategory));

            foreach (var variant in variants)
            {
                foreach (var bInCategoryName in allNames)
                {
                    if (string.Equals(bInCategoryName, variant, StringComparison.OrdinalIgnoreCase))
                    {
                        return (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), bInCategoryName);
                    }
                }
            }
            return BuiltInCategory.INVALID;
        }
    }
}