using DynamicTileFlow.Classes.JSON;

namespace DynamicTileFlow.Processors
{
    public static class NMS
    {
        public static List<DetectionResult> NonMaximumSuppression(List<DetectionResult> boxes, float iouThreshold = 0.5f)
        {
            var result = new List<DetectionResult>();

            // Sort boxes by confidence score descending
            var sortedBoxes = boxes.OrderByDescending(b => b.Confidence).ToList();

            while (sortedBoxes.Count > 0)
            {
                var current = sortedBoxes[0];
                result.Add(current);
                sortedBoxes.RemoveAt(0);

                // Remove boxes that have high overlap with current box
                sortedBoxes = sortedBoxes.Where(box => IoU(current, box) < iouThreshold).ToList();
            }

            return result;
        }
        public static List<DetectionResult> NonMaximumSuppressionByName(List<DetectionResult> boxes, float iouThreshold = 0.5f)
        {
            var result = new List<DetectionResult>();

            foreach (var label in boxes.Select(b => b.Label).Distinct())
            {
                var sortedBoxes = boxes.Where(b=>b.Label == label).OrderByDescending(b => b.Confidence).ToList();

                while (sortedBoxes.Count > 0)
                {
                    var current = sortedBoxes[0];
                    result.Add(current);
                    sortedBoxes.RemoveAt(0);

                    // Remove boxes that have high overlap with current box
                    sortedBoxes = sortedBoxes.Where(box => IoU(current, box) < iouThreshold).ToList();
                }
            }

            return result;
        }

        public static List<DetectionResult> MaximumSuppressionByName(List<DetectionResult> boxes, float iouThreshold = 0.5f)
        {
            var result = new List<DetectionResult>();

            foreach (var label in boxes.Select(b => b.Label).Distinct())
            {
                var sortedBoxes = boxes.Where(b => b.Label == label).OrderByDescending(b => b.Confidence).ToList();

                while (sortedBoxes.Count > 0)
                {
                    var current = sortedBoxes[0];
                    result.Add(current);
                    sortedBoxes.RemoveAt(0);

                    var parentBox = sortedBoxes
                        .Where(box => IoU(box, current) > 0.66)
                        .Select(d => new { d.X_max, d.X_min, d.Y_max, d.Y_min, d.Confidence })
                        .GroupBy(_ => 1)
                        .Select(g => new {
                            x_min = g.Min(r => r.X_min),
                            y_min = g.Min(r => r.Y_min),
                            x_max = g.Max(r => r.X_max),
                            y_max = g.Max(r => r.Y_max),
                            confidence = g.Max(r => r.Confidence)
                        }).FirstOrDefault();


                    if (parentBox != null)
                    {
                        sortedBoxes.RemoveAll(box => IoU(box, current) > 0.85);
                        current.X_min = parentBox.x_min;
                        current.Y_min = parentBox.y_min;
                        current.X_max = parentBox.x_max;
                        current.X_max = parentBox.x_max;
                        current.Confidence = parentBox.confidence;
                    }

                }
            }

            return result;
        }

        public static float IoU(DetectionResult a, DetectionResult b)
        {
            float interLeft = Math.Max(a.X_min, b.X_min);
            float interTop = Math.Max(a.Y_min, b.Y_min);
            float interRight = Math.Min(a.X_max, b.X_max);
            float interBottom = Math.Min(a.Y_max, b.Y_max);

            float interWidth = Math.Max(0, interRight - interLeft);
            float interHeight = Math.Max(0, interBottom - interTop);
            float interArea = interWidth * interHeight;

            float areaA = (a.X_max - a.X_min) * (a.Y_max - a.Y_min);
            float areaB = (b.X_max - b.X_min) * (b.Y_max - b.Y_min);

            return interArea / (areaA + areaB - interArea);
        }

    }
}
