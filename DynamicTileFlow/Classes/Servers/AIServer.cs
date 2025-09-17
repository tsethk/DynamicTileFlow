using DynamicTileFlow.Classes.JSON;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DynamicTileFlow.Classes.Servers
{
    public abstract class AIServer : IAIServer
    {
        private int _ActiveCalls = 0;
        public int ServerTimeout { get; private set; } = 10;
        public string Endpoint { get; set; }
        public string ServerName { get; set; }
        public int Port { get; set; }
        public string Name { get; set; }
        public DateTime? LastKnownActive { get; set; }
        public bool IsSSL { get; set; }
        public string ServiceUrl => (IsSSL ? "https" : "http") + "://" + ServerName + ":" + Port.ToString() + Endpoint;
        public string WebTestURL => (IsSSL ? "https" : "http") + "://" + ServerName + ":" + Port.ToString();
        public int AvgRoundTripTotalCalls { get; private set; }
        public int AvgRoundTrip { get; private set; }
        public int RollingAverageWindow { get; set; }
        public int TotalCalls { get; private set; }
        public int ActiveCalls => _ActiveCalls;
        public bool IsActive { get; private set; } = true;
        public int? MaxBatchSize { get; set; } = null;
        public float MovingAverageAlpha { get; set; }
        public AIServer(string ServerName, int Port, string Endpoint, bool IsSSL, string Name, int ServerTimeout, float MovingAverageAlpha)
        {
            this.ServerName = ServerName;
            this.Port = Port;
            this.Endpoint = Endpoint;
            this.IsSSL = IsSSL;
            this.Name = Name;
            this.ServerTimeout = ServerTimeout;
            this.MovingAverageAlpha = MovingAverageAlpha;
            _ActiveCalls = 0;
            IsActive = true;
        }
        public async Task<APIResponse?> SendRequest(Image<Rgba32> Image)
        {
            IncrementActiveCalls();

            APIResponse? response = default;

            try
            {
                // Await ensures exceptions bubble into the catch block
                var startCall = DateTime.Now;
                response = await CallAPI(Image);
                AddRoundTripStat((int)(DateTime.Now - startCall).TotalMilliseconds);
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
        public async Task<APIResponse?> SendRequest(List<ImageBatchItem> Images)
        {
            IncrementActiveCalls();

            APIResponse? response = default;

            try
            {
                // Await ensures exceptions bubble into the catch block
                var startCall = DateTime.Now;
                response = await CallAPI(Images);
                AddRoundTripStat((int)((DateTime.Now - startCall).TotalMilliseconds / Images.Count));
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
        public void IncrementActiveCalls() => Interlocked.Increment(ref _ActiveCalls);
        public void DecrementActiveCalls() => Interlocked.Decrement(ref _ActiveCalls);
        public void Activate()
        {
            IsActive = true;
            LastKnownActive = DateTime.Now;
        }
        public void Deactivate()
        {
            IsActive = false;
            AvgRoundTrip = 0;
            AvgRoundTripTotalCalls = 0;
        }
        public void AddRoundTripStat(int Milliseconds)
        {
            if (AvgRoundTrip == 0)
            {
                AvgRoundTrip = Milliseconds;
            }
            else
            {
                AvgRoundTrip = (int)(((1 - MovingAverageAlpha) * AvgRoundTrip) + (MovingAverageAlpha * Milliseconds));
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
