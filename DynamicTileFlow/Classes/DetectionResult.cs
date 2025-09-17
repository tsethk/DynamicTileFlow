using Newtonsoft.Json;

namespace DynamicTileFlow.Classes
{
    public class DetectionResult
    {
        public float Confidence { get; set; }
        public string Label { get; set; } = "";
        public int x_min { get; set; }
        public int y_min { get; set; }
        public int x_max { get; set; }
        public int y_max { get; set; }
        public int? BatchNumber { get; set; } = null;
        public string ServerName { get; set; } = "";

        // Shift the bounding box by offset
        public void Offset(int dx, int dy)
        {
            x_min += dx;
            x_max += dx;
            y_min += dy;
            y_max += dy;
        }
        public void Scale(float scaleBy)
        {
            x_min = (int)((float)x_min * scaleBy);
            x_max = (int)((float)x_max * scaleBy);
            y_min = (int)((float)y_min * scaleBy);
            y_max = (int)((float)y_max * scaleBy);
        }
        public void OffsetAndScale(int dx, int dy, float scaleBy)
        {
            Offset(dx, dy);
            Scale(scaleBy);
        }
    }
}
