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
        public ImageBatchItem(Image<Rgba32> image)
        {
            Image = image;
            OriginalX = 0;
            OriginalY = 0;
            OriginalWidth = Image.Width;
            OriginalHeight = Image.Height;
        }
        public ImageBatchItem(Image<Rgba32> image, int originalX, int originalY, int originalWidth, int originalHeight)
        {
            Image = image;
            OriginalX = originalX;
            OriginalY = originalY;
            OriginalWidth = originalWidth;
            OriginalHeight = originalHeight;
        }
    }
}
