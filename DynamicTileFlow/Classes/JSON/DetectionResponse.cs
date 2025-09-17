namespace DynamicTileFlow.Classes.JSON
{
    public class DetectionResponse
    {
        public string Message { get; set; } = "";
        public int Count => Predictions.Count;
        public List<DetectionResult> Predictions { get; set; } = new List<DetectionResult>();
        public bool Success { get; set; } = true;
        public int ProcessMs { get; set; } = 100;
        public int InferenceMs { get; set; } = 200;
        public string ModuleId { get; set; } = "ObjectDetectionYOLOv8";
        public string ModuleName { get; set; } = "Object Detection (YOLOv8)";
        public int Code { get; set; } = 200;
        public string Command { get; set; } = "detect";
        public string RequestId { get; set; } = "";
        public string InferenceDevice { get; set; } = "GPU";
        public int AnalysisRoundTripMs { get; set; } = 300;
        public string ProcessedBy { get; set; } = "localhost";
        public string TimestampUTC => DateTime.Now.ToUniversalTime().ToString();
        public string TileSizes { get; set; } = "";
        public int TileCount { get; set; } = 0; 
        public int ResizeSmallestDimension { get; set; } = 640;
        public string OriginalImageSize { get; set; } = "";
        public string ResizedImageSize { get; set; } = "";
        public List<ResizedTileInfo>? TileList { get; set; } = null;
        public string ServerName { get; set; } = "";    
    }
}
