using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface IRevitApiService
    {
        // Событие для передачи текстовых уведомлений в UI
        event Action<string, RevitMessageType> OnMessageReported;

        // Метод для выполнения произвольного действия в потоке Revit
        void Run(Action<Autodesk.Revit.UI.UIApplication> action);

        // Абстрактный метод для будущей реализации логики уклонов
        void ApplyRoofSlopes(double slopePercent);

        void CreateFloorByWalls(string WallTypeFilter, double ThicknessMm, double OffsetMm);

        // ... другие будущие команды (например, BuildWall, PlaceDrain)
    }
}
