
using DynamicTileFlow.Classes.Servers;
using Microsoft.AspNetCore.Hosting.Server;


namespace ImageTilerProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            var ServerConfigs = builder.Configuration
                .GetSection("AIServers")
                .Get<List<AIServerConfig>>();

            var Servers = new List<AIServer>();

            if (ServerConfigs != null)
            {
                foreach (var cfg in ServerConfigs)
                {
                    switch (cfg.Type)
                    {
                        case "CodeProjectAI":
                            Servers.Add(new YoloServer(
                                cfg.ServerName,
                                cfg.Port,
                                cfg.Endpoint,
                                isSSL: false,                 // or cfg.IsSSL if you add it
                                cfg.Name,
                                cfg.TimeoutInSeconds,
                                rollingAverageWindow: 10
                            ));
                            break;

                        case "Tensor":
                            Servers.Add(new TensorServer(
                                cfg.ServerName,
                                cfg.Port,
                                cfg.Endpoint,
                                isSSL: false,                 // or cfg.IsSSL if you add it
                                cfg.Name,
                                cfg.TimeoutInSeconds,
                                rollingAverageWindow: 10,
                                cfg.Labels
                            )
                            { MaxBatchSize = cfg.MaxBatchSize });
                            break;

                        // case "TensorServer": servers.Add(new TensorServer(...)); break;

                        default:
                            throw new InvalidOperationException($"Unknown server type: {cfg.Type}");
                    }
                }
            }

            builder.Services.AddSingleton<AIServerList>(new AIServerList(Servers));

            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
