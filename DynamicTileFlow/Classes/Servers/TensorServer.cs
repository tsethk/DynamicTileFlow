using DynamicTileFlow.Classes.JSON;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using static System.Net.Mime.MediaTypeNames;

namespace DynamicTileFlow.Classes.Servers
{
    public class TensorServer : AIServer
    {
        private string[] _labels { get; set; }

        public TensorServer(string serverName, int port, string endpoint, bool isSSL, string name, int serverTimeout, float movingAverageAlpha, string[] labels, int maxBatchSize)
            : base(serverName, port, endpoint, isSSL, name, serverTimeout, movingAverageAlpha) // Pass required arguments to base constructor
        {
            this._labels = labels;
            this.MaxBatchSize = maxBatchSize;
        }
        public override async Task<APIResponse?> CallAPI(List<ImageBatchItem> images)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(ServerTimeout);   
                var tensorDetections = new APIResponse();

                var actualImages = images.Select(i => i.Image).ToList();    

                // All tiles should be the same height and width coming in for tensor input
                var tensorInput = TensorProcessor.CreateTensorInput(actualImages, 3, actualImages[0].Height, actualImages[0].Width);

                var content = new StringContent(JsonConvert.SerializeObject(tensorInput), System.Text.Encoding.UTF8, "application/json");

                // Replace with actual endpoint and adjust key names as needed
                var response = await client.PostAsync(ServiceUrl, content);

                if (!response.IsSuccessStatusCode) return null;

                var tensorResponse = await TensorProcessor.DecodeTensorApiResponse(response);

                List<DetectionResult>? detections = new List<DetectionResult>();

                var output = tensorResponse?.Outputs?.FirstOrDefault();

                if (output?.Data != null && output?.Shape?.Length >= 3)
                {
                    detections = TensorProcessor.DecodeDetections(
                        output.Data,
                        output.Shape[0],
                        output.Shape[1],
                        output.Shape[2],
                        0.5f,
                        _labels,
                        0);


                    foreach (var detection in detections)
                    {
                        if (detection.BatchNumber != null)
                        {
                            int batch = detection.BatchNumber.Value;
                            // Scale boxes back to original image size   
                            detection.OffsetAndScale(
                                images[batch].OriginalX, 
                                images[batch].OriginalY,   
                                images[batch].OriginalHeight / images[batch].Image.Height);
                        }
                    }
                    tensorDetections.Predictions = detections;
                }
                else
                {
                    throw new Exception("No valid output received from Tensor API.");
                }

                return tensorDetections;
            }
        }
        public override async Task<APIResponse?> CallAPI(Image<Rgba32> image)
        {
            return await CallAPI(new List<ImageBatchItem> { new ImageBatchItem(image) });   
        }
    }
}
