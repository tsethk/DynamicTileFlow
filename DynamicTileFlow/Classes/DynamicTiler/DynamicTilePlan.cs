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

        /// <summary>
        /// The height at which this tile plan will execute
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// The height to take from the original image for the tile row
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// The nominal width to take from the original image for each tile in the row, width may be shortened
        /// if the end of the image is reached
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// The percentage to overlap the tiles, 50% (0.5) overlap would double the tile count, 0% would be the
        /// minimum tile count
        /// </summary>
        public double OverlapFactor { get; set; }

        /// <summary>
        /// The width to scale the image to before sending to the AI server, scaling will be done with the aspect ratio created by the 
        /// ratio of the Width and the ScaleWidth of the tile
        /// </summary>
        public int ScaleWidth { get; set; }

        /// <summary>
        /// Where to start the row of tiles in the X coordinates, a percentage of the total width
        /// of the image, null indicates no restrictions
        /// </summary>
        public double? XStartPercent { get; set; } = null;

        /// <summary>
        /// Where to end the row of tiles in the X coordinates, a percentage of the total width
        /// of the image, null indicates no restrictions
        /// </summary>
        public double? XEndPercent { get; set; } = null;
    }
}
