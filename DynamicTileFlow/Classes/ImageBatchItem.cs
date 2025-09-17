using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace DynamicTileFlow.Classes
{
    public class ImageBatchItem
    {
        public Image<Rgba32> Image { get; set; }
        public int OriginalX { get; set; }
        public int OriginalY { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }        
        public ImageBatchItem(Image<Rgba32> Image)
        {
            this.Image = Image;
            OriginalX = 0;
            OriginalY = 0;
            OriginalWidth = Image.Width;
            OriginalHeight = Image.Height;
        }
        public ImageBatchItem(Image<Rgba32> Image, int OriginalX, int OriginalY, int OriginalWidth, int OriginalHeight)
        {
            this.Image = Image;
            this.OriginalX = OriginalX;
            this.OriginalY = OriginalY;
            this.OriginalWidth = OriginalWidth;
            this.OriginalHeight = OriginalHeight;
        }
    }
}
