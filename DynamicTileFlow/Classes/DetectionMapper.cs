using DynamicTileFlow.Classes.DynamicTiler;

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
            int OriginalImageWidth,
            int OriginalImageHeight)
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

            mapped.x_min = (int)(det.x_min * invScale) + tile.XStart;
            mapped.x_max = (int)(det.x_max * invScale) + tile.XStart;

            mapped.y_min = (int)(det.y_min * invScale) + tile.YStart;
            mapped.y_max = (int)(det.y_max * invScale) + tile.YStart;

            // Put a 10 pixel buffer in there to see if it stops agent DVR from crashing
            mapped.x_min = Math.Max(10, mapped.x_min);
            mapped.y_min = Math.Max(10, mapped.y_min);
            mapped.x_max = Math.Min(OriginalImageWidth - 10, mapped.x_max);
            mapped.y_max = Math.Min(OriginalImageHeight - 10, mapped.y_max);

            return mapped;
        }

        ///<summary>
        ///Convenience method for a whole list of detections.
        ///</summary>
        public static List<DetectionResult> MapAllToFullImage(
            IEnumerable<DetectionResult> Detections,
            TileInfo Tile,
            int OriginalImageWidth,
            int OriginalImageHeight)
        {
            var results = new List<DetectionResult>();
            foreach (var det in Detections)
            {
                results.Add(MapToFullImage(det, Tile, OriginalImageWidth, OriginalImageHeight));
            }
            return results;
        }
    }
}
