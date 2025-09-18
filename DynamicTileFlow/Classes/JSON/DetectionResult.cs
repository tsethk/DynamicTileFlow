using Newtonsoft.Json;

namespace DynamicTileFlow.Classes.JSON
{
    public class DetectionResult
    {
        public float Confidence { get; set; }
        public string Label { get; set; } = "";
        public int X_min { get; set; }
        public int Y_min { get; set; }
        public int X_max { get; set; }
        public int Y_max { get; set; }
        public int? BatchNumber { get; set; } = null;
        public string ServerName { get; set; } = "";

        // Shift the bounding box by offset
        public void Offset(int dx, int dy)
        {
            X_min += dx;
            X_max += dx;
            Y_min += dy;
            Y_max += dy;
        }
        public void Scale(float scaleBy)
        {
            X_min = (int)(X_min * scaleBy);
            X_max = (int)(X_max * scaleBy);
            Y_min = (int)(Y_min * scaleBy);
            Y_max = (int)(Y_max * scaleBy);
        }
        public void OffsetAndScale(int dx, int dy, float scaleBy)
        {
            Offset(dx, dy);
            Scale(scaleBy);
        }
    }
}
