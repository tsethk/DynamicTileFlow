namespace DynamicTileFlow.Classes.DynamicTiler
{
    public class DynamicTilePlan
    {
        public int TilePlanId { get; set; }
        public string TilePlanName { get; set; } = "";
        public int ImageWidthExpected { get; set; }
        public int ImageHeightExpected { get; set; }
        public List<TilePlan> TilePlans { get; set; } = new List<TilePlan>();
    }

    public class TilePlan
    {
        public int Y { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public double OverlapFactor { get; set; }
        public int ScaleWidth { get; set; }
    }       
}
