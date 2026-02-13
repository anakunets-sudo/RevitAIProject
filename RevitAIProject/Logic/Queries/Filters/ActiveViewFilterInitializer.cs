using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic.Queries.Filters
{
    /// <summary>
    /// Initializer that creates a base collector for the entire Revit project.
    /// Always has the highest priority (0) to start the search chain.
    /// </summary>
    public class ActiveViewFilterInitializer : ISearchFilter, ISearchInitializer
    {
        // <summary>
        /// Execution priority. 0 means it runs first.
        /// </summary>
        public int Priority => 0;

        /// <summary>
        /// Creates a new FilteredElementCollector for all elements in the document.
        /// </summary>
        /// <param name="doc">The Revit document to search in.</param>
        /// <param name="collector">Incoming collector (ignored by initializer).</param>
        /// <returns>A new collector containing all project elements.</returns>
        public FilteredElementCollector Apply(Document doc, FilteredElementCollector collector)
        {
            return new FilteredElementCollector(doc);
        }
    }
}
