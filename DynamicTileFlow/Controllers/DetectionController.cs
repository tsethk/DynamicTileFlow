using DynamicTileFlow.Classes;
using DynamicTileFlow.Classes.DynamicTiler;
using DynamicTileFlow.Classes.JSON;
using DynamicTileFlow.Classes.Servers;
using DynamicTileFlow.Processors;
using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing; // for drawing extensions
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DynamicTileFlow.Controllers
{


    [ApiController]
    [Route("")]
    [Route("image-tiler")]
    public class DetectionController : ControllerBase
    {
        private readonly AIServerList ServerList;

        private readonly IConfiguration Configuration;

        private readonly List<DynamicTilePlan> DynamicTilePlans;

        private readonly float IouThreshold;
        float MinConfidence;

        public DetectionController(
            IConfiguration Configuration, 
            AIServerList ServerList, 
            List<DynamicTilePlan> DynamicTilePlans)
        {
            this.Configuration = Configuration;
            this.ServerList = ServerList;
            this.DynamicTilePlans = DynamicTilePlans;

            IouThreshold = Configuration.GetValue<float>("IOUThreshold");
            MinConfidence = Configuration.GetValue<float>("MinConfidence");
        }
        [HttpGet]
        public IActionResult GetServerStatus()
        {
            //Return the server state object
            return Ok(ServerList.Servers);
        }
        [HttpPost("simple")]
        public async Task<IActionResult> SimpleDetect(
            [FromForm] IFormFile Image)
        {
            //Run the straight query against the server
            var Server = ServerList.GetAIEndpoint();
            CodeProjectAIResponse? result = null;

            if (Server != null)
            {
                //Get the image from the form 
                using var imageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(Image.OpenReadStream());

                result = (CodeProjectAIResponse?)await Server.SendRequest(imageActual);

                if (result != null)
                {
                    //Set the server that processed the request for each prediction
                    result.Predictions.ForEach(p => p.ServerName = Server.Name);
                    result.ProcessedBy = Server.Name;
                    return Ok(result);
                }
                else
                {
                    return Problem("Server did not return");
                }
            }
            return Problem("No servers available");
        }
        [HttpPost("dynamic-tiler-image")]
        public async Task<IActionResult> DynamicTilerImage(
            [FromForm] IFormFile Image,
            [FromQuery] float? ResizeRatio = null,
            [FromQuery] bool? IncludeDetections = null,
            [FromQuery] bool? IncludeTiles = null,
            [FromQuery] int TileStrategy = 1)
        {

            var Colors = new List<Color>() {
                Color.Red,
                Color.Lime,
                Color.Blue,
                Color.Yellow,
                Color.Pink,
                Color.Violet,
                Color.Turquoise
            };

            var ColorIdx = 0;

            //Get the image from the form 
            using var ImageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(Image.OpenReadStream());

            //Get original width and height
            var OriginalHeight = ImageActual.Height;
            var OriginalWidth = ImageActual.Width;


            //Get the plan referenced by the resize strategy    
            var Plan = DynamicTilePlans.Where(d => d.TilePlanId == TileStrategy).FirstOrDefault();

            var Tiles = new List<TileInfo>();

            if (Plan != null)
            {
                //Get the list of tiles to be processed based on the plan
                Tiles = DynamicProcessor.SplitAdaptive(ImageActual, Plan);
            }
            else
            {
                return BadRequest("Invalid tiling strategy");
            }

            if (IncludeTiles == true)
            {
                foreach (var Tile in Tiles)
                {
                    var rect = new RectangleF(
                        Tile.XStart,
                        Tile.YStart,
                        Tile.Width,
                        Tile.Height);

                    // Outline pen (red, 3px thick)
                    var Pen = Pens.Solid(Colors[ColorIdx], 6);
                    ColorIdx = ++ColorIdx % Colors.Count;

                    // Draw rectangle
                    ImageActual.Mutate(ctx => ctx.Draw(Pen, rect));
                }
            }

            if(IncludeDetections == true)
            {

                var AllResults = await GetDetections(Tiles, ServerList, MinConfidence, OriginalWidth, OriginalHeight);
                var Detections = AllResults.Select(r => r.Predictions).SelectMany(r => r).ToList();
                var Predictions = NMS.NonMaximumSuppressionByName(Detections, IouThreshold);

                foreach(var Prediction in Predictions)
                {
                    var rect = new RectangleF(
                        Prediction.x_min,
                        Prediction.y_min,
                        Prediction.x_max - Prediction.x_min,
                        Prediction.y_max - Prediction.y_min);

                    // Outline pen (red, 3px thick)
                    var pen = Pens.Solid(Colors[ColorIdx], 12);
                    ColorIdx = ++ColorIdx % Colors.Count;

                    // Draw rectangle
                    ImageActual.Mutate(ctx => ctx.Draw(pen, rect));

                    var FontSize = 24;

                    if(ResizeRatio != null)
                    {
                        FontSize = (int)(FontSize * (0.5f / ResizeRatio));
                    }

                    DrawTextOnImage(
                        ImageActual, 
                        Prediction.Label + " " + Prediction.Confidence.ToString("0.00"), 
                        Prediction.x_min, 
                        Prediction.y_min - 30,
                        FontSize);     
                }


            }

            if (ResizeRatio != null)
            {
                var NewWidth = (int)(ImageActual.Width * ResizeRatio);
                var NewHeight = (int)(ImageActual.Height * ResizeRatio);

                ImageActual.Mutate(ctx => ctx.Resize(NewWidth, NewHeight));
            }

            var ms = new MemoryStream();
            ImageActual.Save(ms, new PngEncoder());
            ms.Position = 0;

            // Return as an image/png HTTP response
            return File(ms, "image/png");
        }
        [HttpPost("dynamic-tiler")]
        public async Task<IActionResult> DynamicTiler(
            [FromForm] IFormFile Image,
            [FromQuery] int TileStrategy = 1)
        {
            string guid = Guid.NewGuid().ToString().Substring(0, 5);

            int TotalProcessingTime = 0;
            var start = DateTime.Now;

            //List of times spent calling the detection servers 
            var DetectionServerCalls = new ConcurrentBag<Tuple<DateTime, DateTime>>();

            //List of tiles to be returned in the response  
            var ActualTiles = new List<ResizedTileInfo>();

            //Get the image from the form 
            using var ImageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(Image.OpenReadStream());

            //Get original width and height
            var OriginalHeight = ImageActual.Height;
            var OriginalWidth = ImageActual.Width;

            //Get the plan referenced by the resize strategy    
            var Plan = DynamicTilePlans.Where(d => d.TilePlanId == TileStrategy).FirstOrDefault();

            var Tiles = new List<TileInfo>();

            if (Plan != null)
            {
                //Get the list of tiles to be processed based on the plan
                Tiles = DynamicProcessor.SplitAdaptive(ImageActual, Plan);
            }
            else
            {
                return BadRequest("Invalid tiling strategy");
            }

            var AllResults = await GetDetections(Tiles, ServerList, MinConfidence, OriginalWidth, OriginalHeight);

            var StartProcessing = DateTime.Now;

            var Detections = AllResults.Select(r => r.Predictions).SelectMany(r => r).ToList();
            var Predictions = NMS.NonMaximumSuppressionByName(Detections, IouThreshold);
            var FinalResult = new DetectionResponse()
            {
                Predictions = Predictions,
                Code = 200,
                Command = "detect",
                ProcessedBy = string.Join(", ", Predictions.Select(p => p.ServerName).Distinct().ToList()),
                Success = true,
                Message = "Found " + string.Join(", ", Predictions.Select(d => d.Label).Distinct().ToList()),
                TileSizes = "", //string.Join(", ", Tiles.Select(t => t.Width.ToString() + "x" + t.Height.ToString()).Distinct().ToList()),
                RequestId = "request-id",
                ResizeSmallestDimension = -1,
                TileCount = Tiles.Count,
                OriginalImageSize = OriginalWidth.ToString() + "x" + OriginalHeight.ToString(),
                ResizedImageSize = ImageActual.Width.ToString() + "x" + ImageActual.Height.ToString(),
                InferenceMs = (int)GetTotalNonOverlappingTime(DetectionServerCalls).TotalMilliseconds,
                TileList = null //string.Join("; ", ActualTiles.Select(t => $"x:{t.Item1},y:{t.Item2},w:{t.Item3},h:{t.Item4}"))
            };

            FinalResult.ModuleId = string.Join(", ", AllResults.OfType<CodeProjectAIResponse>().Select(a => a.ModuleId).Distinct());
            FinalResult.ModuleName = string.Join(", ", AllResults.OfType<CodeProjectAIResponse>().Select(a => a.ModuleName).Distinct());
            FinalResult.InferenceDevice = string.Join(", ", AllResults.OfType<CodeProjectAIResponse>().Select(a => a.InferenceDevice).Distinct());

            TotalProcessingTime += (int)(DateTime.Now - StartProcessing).TotalMilliseconds;
            var msProcess = (DateTime.Now - start).TotalMilliseconds;
            FinalResult.ProcessMs = TotalProcessingTime;
            FinalResult.AnalysisRoundTripMs = (int)msProcess;

            return Ok(FinalResult);
        }
        static public async Task<IEnumerable<APIResponse>> GetDetections(
            List<TileInfo> Tiles, 
            AIServerList ServerList,
            float ConfidenceThreshold,
            int OriginalWidth, 
            int OriginalHeight)
        {
            var tasks = new List<Task<APIResponse>>();

            for (int i = 0; i < Tiles.Count; i++)
            {
                var Tile = Tiles[i];

                var task = Task.Run(async () =>
                {
                    APIResponse? Result = null;
                    AIServer? Server = ServerList.GetAIEndpoint();

                    if (Server != null)
                    {
                        var startCall = DateTime.Now;

                        Result = await Server.SendRequest(Tile.Image);

                        if (Result != null)
                        {
                            var StartOffsetScale = DateTime.Now;
                            var MappedDetections = new List<DetectionResult>();
                            if (Result != null)
                            {
                                foreach (var Detection in Result.Predictions.Where(p => p.Confidence >= ConfidenceThreshold))
                                {
                                    Detection.ServerName = Server.Name;
                                    MappedDetections.Add(
                                        DetectionMapper.MapToFullImage(Detection, Tile, OriginalWidth, OriginalHeight));
                                }
                                Result.Predictions = MappedDetections;
                            }
                        }
                    }
                    return Result ?? new APIResponse() { Predictions = new List<DetectionResult>() };
                });

                tasks.Add(task);
            }

            var AllResults = await Task.WhenAll(tasks);

            return [.. AllResults];
        }
        static TimeSpan GetTotalNonOverlappingTime(ConcurrentBag<Tuple<DateTime, DateTime>> Times)
        {
            var Intervals = Times
                .Select(t => (Start: t.Item1, End: t.Item2))
                .OrderBy(t => t.Start)
                .ToList();

            if (Intervals.Count == 0)
                return TimeSpan.Zero;

            var Merged = new List<(DateTime Start, DateTime End)>();
            var currentStart = Intervals[0].Start;
            var CurrentEnd = Intervals[0].End;

            foreach (var Interval in Intervals.Skip(1))
            {
                if (Interval.Start <= CurrentEnd) // overlap
                {
                    CurrentEnd = (Interval.End > CurrentEnd) ? Interval.End : CurrentEnd;
                }
                else
                {
                    Merged.Add((currentStart, CurrentEnd));
                    currentStart = Interval.Start;
                    CurrentEnd = Interval.End;
                }
            }
            Merged.Add((currentStart, CurrentEnd));

            return TimeSpan.FromTicks(Merged.Sum(i => (i.End - i.Start).Ticks));
        }
        public static void DrawTextOnImage(Image<Rgba32> image, string text, float x, float y, int FontSize)
        {
            // Load a font (you can also load from a file using FontCollection)

            // Get system font collection
            var systemFonts = SystemFonts.Collection;

            // Try to find Arial, otherwise fallback to first available font
            FontFamily family = systemFonts.Families.FirstOrDefault(f => f.Name == "Arial");

            var font = family.CreateFont(FontSize, FontStyle.Bold);

            var color = Color.White;
            var location = new PointF(x, y);


            // Measure text size
            var textOptions = new TextOptions(font);
            var size = TextMeasurer.MeasureSize(text, textOptions);

            // Draw black rectangle slightly bigger than text
            var padding = 4;
            var rect = new RectangleF(x - padding, y - padding, size.Width + padding * 2, size.Height + padding * 2);
            image.Mutate(ctx => ctx.Fill(Color.Black, rect));

            image.Mutate(ctx => ctx.DrawText(text, font, color, location));
        }
    }
}
