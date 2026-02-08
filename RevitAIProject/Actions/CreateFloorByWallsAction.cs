using System;
using RevitAIProject.Services;

namespace RevitAIProject.Actions
{
    public class CreateFloorByWallsAction : IRevitAction
    {
        // Это свойство требуется интерфейсом, но АИ его не заполняет (оно фиксировано)
        public string ActionName { get { return "CreateFloor"; } }

        // Атрибут AiParam связывает JSON-поле "thickness" со свойством ThicknessMm
        [AiParam("thickness")]
        public double ThicknessMm { get; set; }

        // Атрибут AiParam связывает JSON-поле "offset" со свойством OffsetMm
        [AiParam("offset")]
        public double OffsetMm { get; set; }

        // Если АИ пришлет "filter", значение попадет сюда
        [AiParam("filter")]
        public string WallTypeFilter { get; set; }

        /// <summary>
        /// Метод выполнения команды. Вызывается из ViewModel.
        /// </summary>
        public void Execute(IRevitApiService apiService)
        {
            // Простейшая проверка на валидность данных перед отправкой в Revit
            if (ThicknessMm <= 0) ThicknessMm = 300; // Значение по умолчанию

            // Передаем управление сервису Revit, который работает в основном потоке
            apiService.CreateFloorByWalls(WallTypeFilter, ThicknessMm, OffsetMm);
        }
    }
}   