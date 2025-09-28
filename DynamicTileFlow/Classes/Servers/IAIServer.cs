using DynamicTileFlow.Classes.JSON;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DynamicTileFlow.Classes.Servers
{
    public interface IAIServer
    {
        public int ServerTimeout { get; }   
        public string Endpoint { get; }
        public string ServerName { get; }
        public int Port { get; }
        public string Name { get; }
        public DateTime? LastKnownActive { get; set; }
        public bool IsSSL { get; set; }
        public string ServiceUrl => (IsSSL ? "https" : "http") + "://" + ServerName + ":" + Port.ToString() + Endpoint;
        public string WebTestURL => (IsSSL ? "https" : "http") + "://" + ServerName + ":" + Port.ToString();
        public int AvgRoundTrip { get; }
        public float MovingAverageAlpha { get; set; }
        public int TotalCalls { get; }
        public int ActiveCalls { get; }
        public int ActiveChecks { get; }
        public bool IsActive { get; }
        public int? MaxBatchSize { get; set; }
        public Task<APIResponse?> SendRequest(Image<Rgba32> Image);
        public Task<APIResponse?> SendRequest(List<ImageBatchItem> Images);
        Task<APIResponse?> CallAPI(Image<Rgba32> Image);
        Task<APIResponse?> CallAPI(List<ImageBatchItem> Images);
        public void IncrementActiveCalls();
        public void DecrementActiveCalls();
        public void IncrementActiveChecks();
        public void DecrementActiveChecks();
        public void Deactivate();
        public void Activate();
        public void CheckStatus();
        public void AddRoundTripStat(int RoundTripMs);
    }
}
