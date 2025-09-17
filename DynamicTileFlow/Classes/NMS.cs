using DynamicTileFlow.Classes;

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
                        .Select(d => new { d.x_max, d.x_min, d.y_max, d.y_min, d.Confidence })
                        .GroupBy(_ => 1)
                        .Select(g => new {
                            x_min = g.Min(r => r.x_min),
                            y_min = g.Min(r => r.y_min),
                            x_max = g.Max(r => r.x_max),
                            y_max = g.Max(r => r.y_max),
                            Confidence = g.Max(r => r.Confidence)
                        }).FirstOrDefault();


                    if (parentBox != null)
                    {
                        sortedBoxes.RemoveAll(box => IoU(box, current) > 0.85);
                        current.x_min = parentBox.x_min;
                        current.y_min = parentBox.y_min;
                        current.x_max = parentBox.x_max;
                        current.x_max = parentBox.x_max;
                        current.Confidence = parentBox.Confidence;
                    }

                }
            }

            return result;
        }

        public static float IoU(DetectionResult a, DetectionResult b)
        {
            float interLeft = Math.Max(a.x_min, b.x_min);
            float interTop = Math.Max(a.y_min, b.y_min);
            float interRight = Math.Min(a.x_max, b.x_max);
            float interBottom = Math.Min(a.y_max, b.y_max);

            float interWidth = Math.Max(0, interRight - interLeft);
            float interHeight = Math.Max(0, interBottom - interTop);
            float interArea = interWidth * interHeight;

            float areaA = (a.x_max - a.x_min) * (a.y_max - a.y_min);
            float areaB = (b.x_max - b.x_min) * (b.y_max - b.y_min);

            return interArea / (areaA + areaB - interArea);
        }

    }
}
