using DynamicTileFlow.Classes.DynamicTiler;
using DynamicTileFlow.Classes.JSON;

namespace DynamicTileFlow.Classes
{
    public class DetectionMapper
    {
        ///<summary>
        ///Maps a detection from a scaled/cropped tile back into full coordinates.
        ///</summary>
        public static DetectionResult MapToFullImage(
            DetectionResult det,
            TileInfo tile,
            int originalImageWidth,
            int originalImageHeight)
        {
            // Copy to avoid mutating the original detection
            var mapped = new DetectionResult
            {
                Confidence = det.Confidence,
                Label = det.Label,
                BatchNumber = det.BatchNumber,
                ServerName = det.ServerName
            };

            float invScale = 1.0f / tile.Scale;

            mapped.X_min = (int)(det.X_min * invScale) + tile.XStart;
            mapped.X_max = (int)(det.X_max * invScale) + tile.XStart;

            mapped.Y_min = (int)(det.Y_min * invScale) + tile.YStart;
            mapped.Y_max = (int)(det.Y_max * invScale) + tile.YStart;

            mapped.X_min = Math.Max(0, mapped.X_min);
            mapped.Y_min = Math.Max(0, mapped.Y_min);
            mapped.X_max = Math.Min(originalImageWidth, mapped.X_max);
            mapped.Y_max = Math.Min(originalImageHeight, mapped.Y_max);

            return mapped;
        }

        ///<summary>
        ///Convenience method for a whole list of detections.
        ///</summary>
        public static List<DetectionResult> MapAllToFullImage(
            IEnumerable<DetectionResult> detections,
            TileInfo tile,
            int originalImageWidth,
            int originalImageHeight)
        {
            var results = new List<DetectionResult>();
            foreach (var detection in detections)
            {
                results.Add(MapToFullImage(detection, tile, originalImageWidth, originalImageHeight));
            }
            return results;
        }
    }
}
