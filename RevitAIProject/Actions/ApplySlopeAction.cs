using RevitAIProject.Services;

namespace RevitAIProject.Actions
{
    public class CreateFloorAction : IRevitAction
    {
        public string ActionName => "CreateFloor";

        [AiParam("thickness")] // ИИ пришлет "thickness": 300
        public double ThicknessMm { get; set; }

        [AiParam("offset")] // ИИ пришлет "offset": 500
        public double OffsetMm { get; set; }

        public void Execute(IRevitApiService apiService)
        {
            apiService.CreateFloorByWalls("Exterior", ThicknessMm, OffsetMm);
        }
    }
}
