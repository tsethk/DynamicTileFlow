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
    public class TensorAPIRequest
    {
        public List<TensorAPIInput>? inputs { get; set; } = new List<TensorAPIInput>();
    }
    public class TensorAPIInput
    {
        public string? name { get; set; }
        public string? datatype { get; set; }
        public int[]? shape { get; set; }
        public float[]? data { get; set; }
    }
    public class TensorAPIResponse
    {
        public string? model_name { get; set; }
        public string? model_version { get; set; }
        public TensorAPIOutput[]? outputs { get; set; }
    }
    public class TensorAPIOutput
    {
        public string? name { get; set; }
        public string? datatype { get; set; }
        public int[]? shape { get; set; }
        public float[]? data { get; set; }

    }
    public class TensorServer : AIServer
    {
        public string[] Labels { get; set; }

        public TensorServer(string serverName, int port, string endpoint, bool isSSL, string name, int serverTimeout, float movingAverageAlpha, string[] Labels, int MaxBatchSize)
            : base(serverName, port, endpoint, isSSL, name, serverTimeout, movingAverageAlpha) // Pass required arguments to base constructor
        {
            this.Labels = Labels;
            this.MaxBatchSize = MaxBatchSize;
        }
        public override async Task<APIResponse?> CallAPI(List<ImageBatchItem> Images)
        {
            using (var client = new HttpClient())
            {
                var TensorDetections = new APIResponse();

                var ActualImages = Images.Select(i => i.Image).ToList();    

                //All tiles should be the same height and width coming in for tensor input
                var TensorInput = TensorProcessor.CreateTensorInput(ActualImages, 3, ActualImages[0].Height, ActualImages[0].Width);

                var content = new StringContent(JsonConvert.SerializeObject(TensorInput), System.Text.Encoding.UTF8, "application/json");

                // Replace with actual endpoint and adjust key names as needed
                var Response = await client.PostAsync(ServiceUrl, content);

                if (!Response.IsSuccessStatusCode) return null;

                var TensorResponse = await TensorProcessor.DecodeTensorApiResponse(Response);

                List<DetectionResult>? detections = new List<DetectionResult>();

                var Output = TensorResponse?.outputs?.FirstOrDefault();

                if (Output?.data != null && Output?.shape?.Length >= 3)
                {
                    detections = TensorProcessor.DecodeDetections(
                        Output.data,
                        Output.shape[0],
                        Output.shape[1],
                        Output.shape[2],
                        0.5f,
                        Labels,
                        0);


                    foreach (var detection in detections)
                    {
                        if (detection.BatchNumber != null)
                        {
                            int batch = detection.BatchNumber.Value;
                            //Scale boxes back to original image size   
                            detection.OffsetAndScale(
                                Images[batch].OriginalX, 
                                Images[batch].OriginalY,   
                                Images[batch].OriginalHeight / Images[batch].Image.Height);
                        }
                    }
                    TensorDetections.Predictions = detections;
                }
                else
                {
                    throw new Exception("No valid output received from Tensor API.");
                }

                return TensorDetections;
            }
        }
        public override async Task<APIResponse?> CallAPI(Image<Rgba32> Image)
        {
            return await CallAPI(new List<ImageBatchItem> { new ImageBatchItem(Image) });   
        }
    }
}
