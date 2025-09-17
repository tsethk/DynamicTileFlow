
namespace DynamicTileFlow.Classes.Servers
{
    public class AIServerList
    {
        public AIServerList(IEnumerable<AIServer> servers)
        {
            Servers = servers;
        }       
        public IEnumerable<AIServer> Servers { get; set; }
        public AIServer? GetAIEndpoint()
        {
            // Check if inactive servers are not active in another thread
            _ = Task.Run(() =>
            {
                foreach (var server in Servers.Where(s => s.IsActive == false && (s.LastKnownActive == null || (DateTime.Now - s.LastKnownActive!).Value.TotalSeconds > 30)))
                {
                    server.CheckStatus();
                }
            });

            AIServer? NextServer = null;

            NextServer = Servers
                .Where(s => s.IsActive == true)
                .OrderBy(s => ((float)s.AvgRoundTrip) * s.ActiveCalls).ThenBy(s => s.ActiveCalls).FirstOrDefault((AIServer?)null);

            return NextServer;
        }
    }
}
