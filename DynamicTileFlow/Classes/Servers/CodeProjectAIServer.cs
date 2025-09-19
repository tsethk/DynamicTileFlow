using DynamicTileFlow.Classes.JSON;
using Microsoft.AspNetCore.Hosting.Server;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using static System.Net.Mime.MediaTypeNames;

namespace DynamicTileFlow.Classes.Servers
{

    public class CodeProjectAIServer : AIServer
    {
        public CodeProjectAIServer(string serverName, int port, string endpoint, bool isSSL, string name, int serverTimeout, float movingAverageAlpha)
            : base(serverName, port, endpoint, isSSL, name, serverTimeout, movingAverageAlpha) // Pass required arguments to base constructor
        {
        }
        public override Task<APIResponse?> CallAPI(List<ImageBatchItem> images)
        {
            throw new NotImplementedException();
        }
        public override async Task<APIResponse?> CallAPI(Image<Rgba32> image)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(ServerTimeout);       
                using var imageStream = new MemoryStream();
                await image.SaveAsJpegAsync(imageStream);
                imageStream.Seek(0, SeekOrigin.Begin);

                var content = new MultipartFormDataContent();
                content.Add(new StreamContent(imageStream), "image", "tile.jpg");

                HttpResponseMessage? response;

                var startCall = new Stopwatch();
                startCall.Start();
                response = await client.PostAsync(ServiceUrl, content);
                AddRoundTripStat((int)startCall.Elapsed.TotalMilliseconds);

                if (response == null || !response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var parsed = JsonConvert.DeserializeObject<CodeProjectAIResponse>(json);

                return parsed;
            }
        }
    }
}
