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
        private readonly float _iouThreshold;
        private readonly float _minConfidence;

        public DetectionController(
            IConfiguration configuration,
            AIServerList serverList,
            List<DynamicTilePlan> dynamicTilePlans)
        {
            _serverList = serverList;
            _dynamicTilePlans = dynamicTilePlans;
            _iouThreshold = configuration.GetValue<float>("IOUThreshold");
            _minConfidence = configuration.GetValue<float>("MinConfidence");
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
                using var imageActual = Image.Load<Rgba32>(image.OpenReadStream());

                result = await Server.SendRequest(imageActual);

                if (result != null)
                {
                    var response = new CodeProjectAIResponse();
                    response.Predictions = result.Predictions.Where(p => p.Confidence >= _minConfidence).ToList();
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
            [FromForm(Name = "Image")] IFormFile image,
            [FromQuery] float? resizeRatio = null,
            [FromQuery] bool? includeDetections = null,
            [FromQuery] bool? includeTiles = null,
            [FromQuery] bool oneColorPerPlan = false,
            [FromQuery] int tileStrategy = 1)
        {
            // Colors to rotate through while drawing boxes
            var colors = new List<Color>() {
                Color.Red,
                Color.Lime,
                Color.Blue,
                Color.Yellow,
                Color.Pink,
                Color.Violet,
                Color.Turquoise,
                Color.Brown
            };

            var colorIdx = 0;

            // Get the image from the form 
            using var imageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(image.OpenReadStream());

            // Get original width and height
            var originalHeight = imageActual.Height;
            var originalWidth = imageActual.Width;


            // Get the plan referenced by the resize strategy    
            var plan = _dynamicTilePlans.Where(d => d.TilePlanId == tileStrategy).FirstOrDefault();

            var tiles = new List<TileInfo>();

            if (plan != null)
            {
                // Get the list of tiles to be processed based on the plan
                tiles = DynamicProcessor.SplitAdaptive(imageActual, plan);
            }
            else
            {
                return BadRequest("Invalid tiling strategy");
            }

            if (includeTiles == true)
            {
                foreach (var tile in tiles)
                {
                    var rect = new RectangleF(
                        tile.XStart,
                        tile.YStart,
                        tile.Width,
                        tile.Height);

                    Color color = colors[
                        oneColorPerPlan == true && tile.TilePlanIndex != null ?
                            tile.TilePlanIndex.Value % colors.Count :
                            colorIdx
                        ];

                    // Outline pen (red, 3px thick)
                    var pen = Pens.Solid(color, 3);
                    colorIdx = ++colorIdx % colors.Count;

                    // Draw rectangle
                    imageActual.Mutate(ctx => ctx.Draw(pen, rect));
                }
            }

            if (includeDetections == true)
            {

                var allResults = await GetDetections(tiles, _serverList, _minConfidence, originalWidth, originalHeight);
                var detections = allResults.Select(r => r.Predictions).SelectMany(r => r).ToList();
                var predictions = NMS.NonMaximumSuppressionByName(detections, _iouThreshold);

                foreach (var prediction in predictions)
                {
                    var rect = new RectangleF(
                        prediction.X_min,
                        prediction.Y_min,
                        prediction.X_max - prediction.X_min,
                        prediction.Y_max - prediction.Y_min);

                    // Outline pen (red, 3px thick)
                    var pen = Pens.Solid(colors[colorIdx], 12);
                    colorIdx = ++colorIdx % colors.Count;

                    // Draw rectangle
                    imageActual.Mutate(ctx => ctx.Draw(pen, rect));

                    var fontSize = 24;

                    if (resizeRatio != null)
                    {
                        // Bad dynamic font sizing, but it works okay for now 
                        fontSize = (int)(fontSize * (0.5f / resizeRatio));
                    }

                    DrawTextOnImage(
                        imageActual,
                        prediction.Label + " " + prediction.Confidence.ToString("0.00"),
                        prediction.X_min,
                        prediction.Y_min - 30,
                        fontSize);
                }
            }

            if (resizeRatio != null)
            {
                var newWidth = (int)(imageActual.Width * resizeRatio);
                var newHeight = (int)(imageActual.Height * resizeRatio);

                imageActual.Mutate(ctx => ctx.Resize(newWidth, newHeight));
            }

            var ms = new MemoryStream();
            imageActual.Save(ms, new PngEncoder());
            ms.Position = 0;

            // Return as an image/png HTTP response
            return File(ms, "image/png");
        }
        [HttpPost("dynamic-tiler")]
        public async Task<IActionResult> DynamicTiler(
            [FromForm(Name = "Image")] IFormFile image,
            [FromQuery] int tileStrategy = 1)
        {
            var processingTime = new Stopwatch();
            processingTime.Start();
            // Start the total time stopwatch   
            var totalTime = new Stopwatch();
            totalTime.Start();

            // Generate a short unique ID for the request   
            string guid = Guid.NewGuid().ToString().Substring(0, 5);

            // List of times spent calling the detection servers 
            var detectionServerCalls = new ConcurrentBag<Tuple<DateTime, DateTime>>();

            // List of tiles to be returned in the response  
            var actualTiles = new List<ResizedTileInfo>();

            // Get the image from the form 
            using var imageActual = SixLabors.ImageSharp.Image.Load<Rgba32>(image.OpenReadStream());

            // Get original width and height
            var originalHeight = imageActual.Height;
            var originalWidth = imageActual.Width;

            // Get the plan referenced by the resize strategy    
            var plan = _dynamicTilePlans.Where(d => d.TilePlanId == tileStrategy).FirstOrDefault();

            var tiles = new List<TileInfo>();

            // Make sure we have a valid plan   
            if (plan != null)
            {
                // Get the list of tiles to be processed based on the plan
                tiles = DynamicProcessor.SplitAdaptive(imageActual, plan);
            }
            else
            {
                return BadRequest("Invalid tiling strategy");
            }

            var inferenceTime = new Stopwatch();

            processingTime.Stop();
            inferenceTime.Start();

            var allResults = await GetDetections(tiles, _serverList, _minConfidence, originalWidth, originalHeight);

            processingTime.Start();
            inferenceTime.Stop();

            var detections = allResults.Select(r => r.Predictions).SelectMany(r => r).ToList();
            var predictions = NMS.NonMaximumSuppressionByName(detections, _iouThreshold);
            var finalResult = new DetectionResponse()
            {
                Predictions = predictions,
                Code = 200,
                Command = "detect",
                ProcessedBy = string.Join(", ", predictions.Select(p => p.ServerName).Distinct().ToList()),
                Success = true,
                Message = "Found " + string.Join(", ", predictions.Select(d => d.Label.Trim()).Distinct().ToList()),
                RequestId = guid,
                TileCount = tiles.Count,
                OriginalImageSize = originalWidth.ToString() + "x" + originalHeight.ToString(),
                InferenceMs = (int)inferenceTime.Elapsed.TotalMilliseconds,
                ModuleId = string.Join(", ", allResults.OfType<CodeProjectAIResponse>().Select(a => a.ModuleId).Distinct()),
                ModuleName = string.Join(", ", allResults.OfType<CodeProjectAIResponse>().Select(a => a.ModuleName).Distinct()),
                InferenceDevice = string.Join(", ", allResults.OfType<CodeProjectAIResponse>().Select(a => a.InferenceDevice).Distinct()),
                ServerName = string.Join(", ", allResults.OfType<CodeProjectAIResponse>().Select(a => a.ProcessedBy).Distinct())
            };

            finalResult.ProcessMs = (int)processingTime.Elapsed.TotalMilliseconds;
            finalResult.AnalysisRoundTripMs = (int)totalTime.Elapsed.TotalMilliseconds;

            LogMessage(guid + " Processed in " + totalTime.Elapsed.TotalMilliseconds + "ms, Inference: " + inferenceTime.Elapsed.TotalMilliseconds + "ms, Tiles: " + tiles.Count + ", Detections: " + finalResult.Predictions.Count);
            LogMessage(guid + " FinalResult: " + System.Text.Json.JsonSerializer.Serialize(finalResult));

            return Ok(finalResult);
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
                var tile = tiles[i];

                var task = Task.Run(async () =>
                {
                    APIResponse? result = null;
                    AIServer? server = serverList.GetAIEndpoint();

                    if (server != null)
                    {
                        var startCall = DateTime.Now;

                        result = await server.SendRequest(tile.Image);

                        if (result != null)
                        {
                            var MappedDetections = new List<DetectionResult>();
                            if (result != null)
                            {
                                foreach (var Detection in result.Predictions.Where(p => p.Confidence >= confidenceThreshold))
                                {
                                    Detection.ServerName = server.Name;
                                    MappedDetections.Add(
                                        DetectionMapper.MapToFullImage(Detection, tile, originalWidth, originalHeight));
                                }
                                result.Predictions = MappedDetections;
                            }
                        }
                    }
                    return result ?? new APIResponse() { Predictions = new List<DetectionResult>() };
                });

                tasks.Add(task);
            }

            var allResults = await Task.WhenAll(tasks);

            return [.. allResults];
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
        public static void LogMessage(string message)
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }
            var logFile = Path.Combine(logPath, "dynamictileflow.log");
            using (var writer = new StreamWriter(logFile, append: true))
            {
                writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            }
        }
    }
}
