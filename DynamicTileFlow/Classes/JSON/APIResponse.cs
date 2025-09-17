using Newtonsoft.Json;

namespace DynamicTileFlow.Classes.JSON
{
    public class APIResponse
    {
        [JsonProperty("predictions")]
        public List<DetectionResult> Predictions { get; set; } = new List<DetectionResult>();
    }
}
