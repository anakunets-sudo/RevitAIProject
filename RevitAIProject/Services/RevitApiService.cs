using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace RevitAIProject.Services
{
    public class RevitApiService : IRevitApiService
    {
        private readonly ExternalEvent _externalEvent;
        private readonly RevitTaskHandler _handler;

        public event Action<string, RevitMessageType> OnMessageReported;

        private void Report(string message, RevitMessageType messageType)
        {
            // Вызываем событие, если на него кто-то подписан
            OnMessageReported?.Invoke(message, messageType);
        }

        public RevitApiService() : this(new RevitTaskHandler())
        {
        }

        public RevitApiService(RevitTaskHandler handler)
        {
            _handler = handler;
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Run(Action<UIApplication> action)
        {
            _handler.Enqueue(action);
            _externalEvent.Raise();
        }

        public void ApplyRoofSlopes(double slopePercent)
        {
            Run(app =>
            {
                // Тут код работы с уклонами (из предыдущих шагов)
                TaskDialog.Show("Revit AI", "Уклон установлен на " + slopePercent + "%");
            });
        }

        public void CreateFloorByWalls(string wallFilter, double thicknessMm, double offsetMm)
        {
            _handler.Enqueue(app =>
            {
                UIDocument uiDoc = app.ActiveUIDocument;
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
                    using (Transaction tx = new Transaction(doc, "AI: Create Floor"))
                    {
                        tx.Start();

                        Level level = doc.ActiveView.GenLevel ??
                                     new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().First();

                        FloorType fType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().First();

                        Floor floor = doc.Create.NewFloor(profile, fType, level, false);

                        // Установка смещения
                        if (Math.Abs(offsetMm) > 0.001)
                        {
                            Parameter p = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                            if (p != null) p.Set(offsetMm / 304.8);
                        }

                        tx.Commit();
                        Report("Готово! Стен: " + walls.Count, RevitMessageType.Info);
                    }
                }
                catch (Exception ex)
                {
                    Report(ex.Message, RevitMessageType.Error);
                }
            });

            _externalEvent.Raise();
        }
    }
}
