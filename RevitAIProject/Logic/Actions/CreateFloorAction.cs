using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RevitAIProject.Logic.Actions
{
    public class CreateFloorAction : BaseRevitAction
    {
        [AiParam("CreateFloor", Description = "Creates a floor with an up (+) or down (-) offset, or an offset of 0 if the user does not specify one.")]
        public override string Name => "CreateFloor";

        [AiParam("offset", Description = "The offset can be up (+) and down (-)")]
        public double OffsetFt { get; set; }

        protected override void Execute(IRevitContext context)
        {
                UIDocument uiDoc = context.UIDoc;
                Document doc = uiDoc.Document;

            try
            {
                // 1. Сбор стен (Выделение или Коннектор)
                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                List<Wall> walls = new List<Wall>();

                if (selectedIds.Count > 0)
                {
                    foreach (ElementId id in selectedIds)
                    {
                        Wall wall = doc.GetElement(id) as Wall;
                        if (wall != null) walls.Add(wall);
                    }
                }

                if (walls.Count == 0)
                {
                    walls = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .Cast<Wall>()
                        .Where(w => w.WallType.Function == WallFunction.Exterior)
                        .ToList();
                }

                if (walls.Count == 0) throw new Exception("Стены не найдены.");

                // 2. Извлечение геометрии
                List<Curve> wallCurves = new List<Curve>();
                foreach (Wall wall in walls)
                {
                    LocationCurve lc = wall.Location as LocationCurve;
                    if (lc != null) wallCurves.Add(lc.Curve);
                }

                // 3. Сортировка "разнобоя" в цепочку
                CurveArray profile = Utils.GeometryUtils.SortCurves(wallCurves);

                // 4. ПРОВЕРКА КОНТУРА (Интегрирована)
                if (!Utils.GeometryUtils.IsCurveArrayClosed(profile))
                {
                    throw new Exception("Контур не замкнут. Начало первой стены не совпадает с концом последней.");
                }

                // 5. Создание перекрытия
                using (TransactionGroup tg = new TransactionGroup(doc, TransactionName))
                {
                    tg.Start();

                    using (Transaction tr = new Transaction(doc, TransactionName))
                    {

                        tr.Start();

                        Level level = doc.ActiveView.GenLevel ??
                                 new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().First();

                        FloorType fType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();

                        Floor floor = doc.Create.NewFloor(profile, fType, level, false);

                        if (!string.IsNullOrEmpty(AssignAiName))
                            context.SessionContext.Store(AssignAiName, floor.Id);

                        tr.Commit();

                        tr.Start();
                        // Установка смещения
                        if (Math.Abs(OffsetFt) > 0.001)
                        {
                            Parameter p = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                            if (p != null) p.Set(OffsetFt);
                        }
                        tr.Commit();
                    }

                    tg.Assimilate();

                    //apiService.Report("Готово! Стен: " + walls.Count, RevitMessageType.Info);
                }
            }
            catch (Exception ex)
            {
                context.Report(ex.Message, RevitMessageType.Error);
            }

            //apiService.CreateFloorByWalls("Exterior", /*ThicknessMm,*/ OffsetFt);
        }
    }
}
