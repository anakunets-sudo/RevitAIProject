using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Utils
{
    public static class GeometryUtils
    {
        private const double Tolerance = 1.0 / 304.8; // 1 мм в футах

        /// <summary>
        /// Сортирует список кривых в последовательную цепочку.
        /// </summary>
        public static CurveArray SortCurves(List<Curve> sourceCurves)
        {
            if (sourceCurves == null || sourceCurves.Count == 0) return new CurveArray();

            CurveArray sortedArray = new CurveArray();
            List<Curve> remaining = sourceCurves.ToList();

            // Берем первую кривую за точку отсчета
            Curve current = remaining[0];
            sortedArray.Append(current);
            remaining.RemoveAt(0);

            XYZ lastPoint = current.GetEndPoint(1);

            while (remaining.Count > 0)
            {
                bool found = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    Curve next = remaining[i];
                    XYZ start = next.GetEndPoint(0);
                    XYZ end = next.GetEndPoint(1);

                    if (start.DistanceTo(lastPoint) < Tolerance)
                    {
                        sortedArray.Append(next);
                        lastPoint = end;
                        remaining.RemoveAt(i);
                        found = true;
                        break;
                    }
                    else if (end.DistanceTo(lastPoint) < Tolerance)
                    {
                        sortedArray.Append(next.CreateReversed());
                        lastPoint = start;
                        remaining.RemoveAt(i);
                        found = true;
                        break;
                    }
                }

                if (!found)
                    throw new Exception("Контур разорван. Кривые не образуют непрерывную цепочку.");
            }

            return sortedArray;
        }

        /// <summary>
        /// Проверяет замкнутость CurveArray (начало первой совпадает с концом последней).
        /// </summary>
        public static bool IsCurveArrayClosed(CurveArray profile)
        {
            if (profile == null || profile.IsEmpty) return false;

            XYZ startPoint = profile.get_Item(0).GetEndPoint(0);
            XYZ endPoint = profile.get_Item(profile.Size - 1).GetEndPoint(1);

            return startPoint.DistanceTo(endPoint) < Tolerance;
        }
    }
}
