using Newtonsoft.Json;

namespace DynamicTileFlow.Classes.JSON
{
    public class CodeProjectAIResponse : APIResponse
    {
        public string? Message { get; set; }
        public int? Count { get; set; }
        public bool? Success { get; set; }
        public int? ProcessMs { get; set; }
        public int? InferenceMs { get; set; }
        public string? ModuleId { get; set; }
        public string? ModuleName { get; set; }
        public int? Code { get; set; }
        public string? Command { get; set; }
        public string? RequestId { get; set; }
        public string? InferenceDevice { get; set; }
        public int? AnalysisRoundTripMs { get; set; }
        public string? ProcessedBy { get; set; }
        public string? TimestampUTC { get; set; }
    }
}
