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
    public class DetectionController : ControllerBase
    {
        private readonly AIServerList _serverList;

        private readonly List<DynamicTilePlan> _dynamicTilePlans;

        private readonly float iouThreshold;
        private readonly float minConfidence;

        public DetectionController(
            IConfiguration configuration,
            AIServerList serverList,
            List<DynamicTilePlan> dynamicTilePlans)
        {
            _serverList = serverList;
            _dynamicTilePlans = dynamicTilePlans;

            iouThreshold = configuration.GetValue<float>("IOUThreshold");
            minConfidence = configuration.GetValue<float>("MinConfidence");
        }
        [HttpGet]
        public IActionResult GetServerStatus()
        {
            // Return the server state object
            return Ok(_serverList.Servers);
        }
        [HttpPost("simple")]
        public async Task<IActionResult> SimpleDetect(
            [FromForm(Name = "Image")] IFormFile image)
        {
            // Run the straight query against the server
            var Server = _serverList.GetAIEndpoint();
            APIResponse? result = null;

            if (Server != null)
            {
                // Get the image from the form 
                using var imageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(image.OpenReadStream());

                result = await Server.SendRequest(imageActual);

                if (result != null)
                {
                    var response = new CodeProjectAIResponse();
                    response.Predictions = result.Predictions.Where(p => p.Confidence >= minConfidence).ToList();
                    // Set the server that processed the request for each prediction
                    response.Predictions.ForEach(p => p.ServerName = Server.Name);
                    response.ProcessedBy = Server.Name;
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
            [FromForm(Name ="Image")] IFormFile image,
            [FromQuery] float? resizeRatio = null,
            [FromQuery] bool? includeDetections = null,
            [FromQuery] bool? includeTiles = null,
            [FromQuery] int tileStrategy = 1)
        {
            // Colors to rotate through while drawing boxes
            var Colors = new List<Color>() {
                Color.Red,
                Color.Lime,
                Color.Blue,
                Color.Yellow,
                Color.Pink,
                Color.Violet,
                Color.Turquoise,
                Color.Brown
            };

            var ColorIdx = 0;

            // Get the image from the form 
            using var ImageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(image.OpenReadStream());

            // Get original width and height
            var OriginalHeight = ImageActual.Height;
            var OriginalWidth = ImageActual.Width;


            // Get the plan referenced by the resize strategy    
            var Plan = _dynamicTilePlans.Where(d => d.TilePlanId == tileStrategy).FirstOrDefault();

            var Tiles = new List<TileInfo>();

            if (Plan != null)
            {
                // Get the list of tiles to be processed based on the plan
                Tiles = DynamicProcessor.SplitAdaptive(ImageActual, Plan);
            }
            else
            {
                return BadRequest("Invalid tiling strategy");
            }

            if (includeTiles == true)
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

            if (includeDetections == true)
            {

                var AllResults = await GetDetections(Tiles, _serverList, minConfidence, OriginalWidth, OriginalHeight);
                var Detections = AllResults.Select(r => r.Predictions).SelectMany(r => r).ToList();
                var Predictions = NMS.NonMaximumSuppressionByName(Detections, iouThreshold);

                foreach (var Prediction in Predictions)
                {
                    var rect = new RectangleF(
                        Prediction.X_min,
                        Prediction.Y_min,
                        Prediction.X_max - Prediction.X_min,
                        Prediction.Y_max - Prediction.Y_min);

                    // Outline pen (red, 3px thick)
                    var pen = Pens.Solid(Colors[ColorIdx], 12);
                    ColorIdx = ++ColorIdx % Colors.Count;

                    // Draw rectangle
                    ImageActual.Mutate(ctx => ctx.Draw(pen, rect));

                    var FontSize = 24;

                    if (resizeRatio != null)
                    {
                        // Bad dynamic font sizing, but it works okay for now 
                        FontSize = (int)(FontSize * (0.5f / resizeRatio));
                    }

                    DrawTextOnImage(
                        ImageActual,
                        Prediction.Label + " " + Prediction.Confidence.ToString("0.00"),
                        Prediction.X_min,
                        Prediction.Y_min - 30,
                        FontSize);
                }
            }

            if (resizeRatio != null)
            {
                var NewWidth = (int)(ImageActual.Width * resizeRatio);
                var NewHeight = (int)(ImageActual.Height * resizeRatio);

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
            [FromForm(Name = "Image")] IFormFile image,
            [FromQuery] int tileStrategy = 1)
        {
            // Start the total time stopwatch   
            var TotalTime = new Stopwatch();
            TotalTime.Start();

            // Generate a short unique ID for the request   
            string GUID = Guid.NewGuid().ToString().Substring(0, 5);

            // List of times spent calling the detection servers 
            var DetectionServerCalls = new ConcurrentBag<Tuple<DateTime, DateTime>>();

            // List of tiles to be returned in the response  
            var ActualTiles = new List<ResizedTileInfo>();

            // Get the image from the form 
            using var ImageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(image.OpenReadStream());

            // Get original width and height
            var OriginalHeight = ImageActual.Height;
            var OriginalWidth = ImageActual.Width;

            // Get the plan referenced by the resize strategy    
            var Plan = _dynamicTilePlans.Where(d => d.TilePlanId == tileStrategy).FirstOrDefault();

            var Tiles = new List<TileInfo>();

            // Make sure we have a valid plan   
            if (Plan != null)
            {
                // Get the list of tiles to be processed based on the plan
                Tiles = DynamicProcessor.SplitAdaptive(ImageActual, Plan);
            }
            else
            {
                return BadRequest("Invalid tiling strategy");
            }
            var InferenceTime = new Stopwatch();
            InferenceTime.Start();
            var AllResults = await GetDetections(Tiles, _serverList, minConfidence, OriginalWidth, OriginalHeight);
            InferenceTime.Stop();

            var ProcessingTime = new Stopwatch();
            ProcessingTime.Start();

            var Detections = AllResults.Select(r => r.Predictions).SelectMany(r => r).ToList();
            var Predictions = NMS.NonMaximumSuppressionByName(Detections, iouThreshold);
            var FinalResult = new DetectionResponse()
            {
                Predictions = Predictions,
                Code = 200,
                Command = "detect",
                ProcessedBy = string.Join(", ", Predictions.Select(p => p.ServerName).Distinct().ToList()),
                Success = true,
                Message = "Found " + string.Join(", ", Predictions.Select(d => d.Label.Trim()).Distinct().ToList()),
                RequestId = GUID,
                TileCount = Tiles.Count,
                OriginalImageSize = OriginalWidth.ToString() + "x" + OriginalHeight.ToString(),
                InferenceMs = (int)InferenceTime.Elapsed.TotalMilliseconds,
                ModuleId = string.Join(", ", AllResults.OfType<CodeProjectAIResponse>().Select(a => a.ModuleId).Distinct()),
                ModuleName = string.Join(", ", AllResults.OfType<CodeProjectAIResponse>().Select(a => a.ModuleName).Distinct()),
                InferenceDevice = string.Join(", ", AllResults.OfType<CodeProjectAIResponse>().Select(a => a.InferenceDevice).Distinct()),

            };

            FinalResult.ProcessMs = (int)ProcessingTime.Elapsed.TotalMilliseconds;
            FinalResult.AnalysisRoundTripMs = (int)TotalTime.Elapsed.TotalMilliseconds;

            return Ok(FinalResult);
        }
        static public async Task<IEnumerable<APIResponse>> GetDetections(
            List<TileInfo> tiles,
            AIServerList serverList,
            float confidenceThreshold,
            int originalWidth,
            int originalHeight)
        {
            var tasks = new List<Task<APIResponse>>();

            for (int i = 0; i < tiles.Count; i++)
            {
                var Tile = tiles[i];

                var task = Task.Run(async () =>
                {
                    APIResponse? Result = null;
                    AIServer? Server = serverList.GetAIEndpoint();

                    if (Server != null)
                    {
                        var startCall = DateTime.Now;

                        Result = await Server.SendRequest(Tile.Image);

                        if (Result != null)
                        {
                            var MappedDetections = new List<DetectionResult>();
                            if (Result != null)
                            {
                                foreach (var Detection in Result.Predictions.Where(p => p.Confidence >= confidenceThreshold))
                                {
                                    Detection.ServerName = Server.Name;
                                    MappedDetections.Add(
                                        DetectionMapper.MapToFullImage(Detection, Tile, originalWidth, originalHeight));
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
        public static void DrawTextOnImage(Image<Rgba32> image, string text, float x, float y, int FontSize)
        {
            // Load bundled font
            var collection = new FontCollection();
            FontFamily family = collection.Add("Resources/Roboto-Regular.ttf");

            // Create the font at your desired size
            Font font = family.CreateFont(FontSize, FontStyle.Regular);


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
