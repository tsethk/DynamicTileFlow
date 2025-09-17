namespace DynamicTileFlow.Classes.Servers
{
    public class AIServerConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int TimeoutInSeconds { get; set; } = 10;
        public int MaxBatchSize { get; set; } = 1;
        public string[] Labels { get; set; } = Array.Empty<string>();
        public bool IsSSL { get; set; } = false;
        public float MovingAverageAlpha { get; set; } = 0.1f;
    }
}
