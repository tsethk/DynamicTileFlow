using DynamicTileFlow.Classes.JSON;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;

namespace DynamicTileFlow.Classes.Servers
{
    public abstract class AIServer : IAIServer
    {
        private int _activeCalls = 0;
        public int ServerTimeout { get; private set; } = 10;
        public string Endpoint { get; set; }
        public string ServerName { get; set; }
        public int Port { get; set; }
        public string Name { get; set; }
        public DateTime? LastKnownActive { get; set; }
        public bool IsSSL { get; set; }
        public string ServiceUrl => (IsSSL ? "https" : "http") + "://" + ServerName + ":" + Port.ToString() + Endpoint;
        public string WebTestURL => (IsSSL ? "https" : "http") + "://" + ServerName + ":" + Port.ToString();
        public int AvgRoundTrip { get; private set; }
        public int TotalCalls { get; private set; }
        public int ActiveCalls => _activeCalls;
        public bool IsActive { get; private set; } = true;
        public int? MaxBatchSize { get; set; } = null;
        public float MovingAverageAlpha { get; set; }
        public AIServer(
            string serverName, 
            int port, 
            string endpoint, 
            bool isSSL, 
            string name, 
            int serverTimeout, 
            float movingAverageAlpha)
        {
            _activeCalls = 0;
            ServerName = serverName;
            Port = port;
            Endpoint = endpoint;
            IsSSL = isSSL;
            Name = name;
            ServerTimeout = serverTimeout;
            MovingAverageAlpha = movingAverageAlpha;
            IsActive = true;
        }
        public async Task<APIResponse?> SendRequest(Image<Rgba32> image)
        {
            IncrementActiveCalls();

            APIResponse? response = default;

            try
            {
                var startCall = new Stopwatch();
                startCall.Start();
                response = await CallAPI(image);
                AddRoundTripStat((int)startCall.Elapsed.TotalMilliseconds);
            }
            catch (Exception)
            {
                Deactivate();
            }
            finally
            {
                DecrementActiveCalls();
            }

            if (response != null)
            {
                LastKnownActive = DateTime.Now;
            }

            return response;
        }
        public async Task<APIResponse?> SendRequest(List<ImageBatchItem> images)
        {
            IncrementActiveCalls();

            APIResponse? response = default;

            try
            {
                var startCall = new Stopwatch();
                startCall.Start();
                response = await CallAPI(images);
                AddRoundTripStat((int)(startCall.Elapsed.TotalMilliseconds / images.Count));
            }
            catch (Exception)
            {
                Deactivate();
            }
            finally
            {
                DecrementActiveCalls();
            }

            if (response != null)
            {
                LastKnownActive = DateTime.Now;
            }

            return response;
        }
        public abstract Task<APIResponse?> CallAPI(Image<Rgba32> Image);
        public abstract Task<APIResponse?> CallAPI(List<ImageBatchItem> Images);
        public void IncrementActiveCalls() => Interlocked.Increment(ref _activeCalls);
        public void DecrementActiveCalls() => Interlocked.Decrement(ref _activeCalls);
        public void Activate()
        {
            IsActive = true;
            LastKnownActive = DateTime.Now;
        }
        public void Deactivate()
        {
            IsActive = false;
            AvgRoundTrip = 0;
        }
        public void AddRoundTripStat(int milliseconds)
        {
            if (AvgRoundTrip == 0)
            {
                AvgRoundTrip = milliseconds;
            }
            else
            {
                AvgRoundTrip = (int)(((1 - MovingAverageAlpha) * AvgRoundTrip) + (MovingAverageAlpha * milliseconds));
            }
            TotalCalls++;
        }
        public void CheckStatus()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(ServerTimeout);
                    var response = client.GetAsync(WebTestURL).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        Activate();
                    }
                    else
                    {
                        Deactivate();
                    }
                }
            }
            catch
            {
                Deactivate();
            }
        }
    }
}
