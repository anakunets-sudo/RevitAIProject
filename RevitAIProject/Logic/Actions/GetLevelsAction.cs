using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RevitAIProject.Logic.Actions
{
    [AiParam("GetLevelsAction", Description = "Returns a list of all levels in the project with their Names, IDs, and Elevations.")]
    public class GetLevelsAction : BaseRevitAction
    {
        protected override void Execute(IRevitContext context)
        {
            var doc = context.UIDoc.Document;

            // Быстрый сбор всех уровней через Collector
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation) // Сортируем от подвала к крыше
                .ToList();

            var levelIds = new List<ElementId>();

            foreach (var level in levels)
            {
                levelIds.Add(level.Id);

                Report($"Level name {level.Name} id {level.Id} elevation {level.Elevation} feet defined", Services.RevitMessageType.AiReport);
            }

            RegisterCreatedElements(levelIds);
        }
    }
}
