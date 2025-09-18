using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace DynamicTileFlow.Classes.DynamicTiler
{
    public class DynamicProcessor
    {
        public static List<TileInfo> SplitAdaptive(Image<Rgba32> source, DynamicTilePlan plan)
        {
            var tiles = plan.TilePlans
                .AsParallel()
                .SelectMany(tilePlan =>
                    CropAndSplitWithOverlap(
                        source,
                        tilePlan.Width,
                        tilePlan.Y,
                        tilePlan.Height,
                        tilePlan.ScaleWidth / (float)tilePlan.Width,    
                        tilePlan.OverlapFactor))
                .ToList();

            return tiles;
        }
        private static List<TileInfo> CropAndSplitWithOverlap(
            Image<Rgba32> source,
            int widthPerTile,
            int yStart,
            int height,
            float scale,
            double overlapFactor)
        {
            // Get a list of tiles that will be added to the concurrent bag  
            var newTiles = new List<TileInfo>();

            int actualWidthPerTile = (int)(widthPerTile * (1.0d - (2 * overlapFactor)));
            int actualOverlapPixels = (int)(widthPerTile * overlapFactor);

            // Crop and resize each tile, run until the x of the tile is beyond the width of the image   
            for (int x = 0; x < source.Width; x += actualWidthPerTile + actualOverlapPixels)
            {
                // Set the width to be the remaining width if we are at the end of the image 
                var actualWidth = Math.Min(widthPerTile, source.Width - x);
                var actualHeight = Math.Min(height, source.Height - yStart);  
                // Get the cropped image 
                var cropped = source.Clone(ctx =>
                    ctx.Crop(new Rectangle(
                        x,
                        yStart,
                        actualWidth,
                        actualHeight)));

                // Scale if needed
                if (Math.Abs(scale - 1.0f) > 0.001f)
                {
                    cropped.Mutate(ctx => ctx.Resize((int)(widthPerTile * scale), (int)(height * scale)));
                }

                // Add to the list of tiles  
                newTiles.Add(new TileInfo
                {
                    Image = cropped,
                    YStart = yStart,
                    Scale = scale,
                    XStart = x,
                    Width = actualWidth,
                    Height = actualHeight
                });
            }
            return newTiles;
        }
    }
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
    }
}
