using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DynamicTileFlow.Classes.DynamicTiler
{
    public class TileInfo
    {
        public Image<Rgba32> Image { get; set; } = null!;
        public int YStart { get; set; }
        public int XStart { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public float Scale { get; set; }
        public int XEnd => XStart + Width;
        public int YEnd => YStart + Height;
        public int ColWidth => Width;
        public int? TilePlanIndex { get; set; } = null;
    }
}
