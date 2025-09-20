
using DynamicTileFlow.Classes.DynamicTiler;
using DynamicTileFlow.Classes.Servers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;


namespace ImageTilerProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Using Roboto-Regular.ttf (Apache 2.0 License)");
            
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https:// aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            var DynamicTilePlans = builder.Configuration
                .GetSection("DynamicTilePlans")
                .Get<List<DynamicTilePlan>>() ?? new List<DynamicTilePlan>();

            foreach(var TilePlan in DynamicTilePlans)
            { 
                if(TilePlan.ImageWidthExpected <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(TilePlan.ImageWidthExpected), 
                        TilePlan.ImageWidthExpected, 
                        $"Dynamic tile plan '{TilePlan.TilePlanName}' must have a positive ImageWidthExpected.");      
                }
                if(TilePlan.ImageHeightExpected <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(TilePlan.ImageHeightExpected), 
                        TilePlan.ImageHeightExpected, 
                        $"Dynamic tile plan '{TilePlan.TilePlanName}' must have a positive ImageHeightExpected.");   
                }
                foreach(var plan in TilePlan.TilePlans)
                {
                    if(plan.XEndPercent != null && (plan.XEndPercent > 1 || plan.XEndPercent <= 0))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(plan.XEndPercent), 
                            plan.XEndPercent, 
                            $"Dynamic tile plan '{TilePlan.TilePlanName}' has an X End Percent greater than one or zero or negative.");
                    }
                    if(plan.XStartPercent != null && (plan.XStartPercent >= 1 || plan.XStartPercent < 0))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(plan.XStartPercent), 
                            plan.XStartPercent, 
                            $"Dynamic tile plan '{TilePlan.TilePlanName}' has an X Start Percent greater than or equal to one or negative.");
                    }  
                    if(plan.XStartPercent != null && plan.XEndPercent != null && plan.XStartPercent >= plan.XEndPercent)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(plan.XStartPercent), 
                            plan.XStartPercent, 
                            $"Dynamic tile plan '{TilePlan.TilePlanName}' has an X Start Percent greater than or equal to the X End Percent.");
                    }
                    if(plan.Height <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(plan.Height), 
                            plan.Height, 
                            $"Dynamic tile plan '{TilePlan.TilePlanName}' has a tile plan with non-positive Height.");  
                    }
                    if(plan.Width <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(plan.Width), 
                            plan.Width, 
                            $"Dynamic tile plan '{TilePlan.TilePlanName}' has a tile plan with non-positive Width."); 
                    }
                    if(plan.OverlapFactor < 0 || plan.OverlapFactor >= 0.5)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(plan.OverlapFactor), 
                            plan.OverlapFactor, 
                            $"Dynamic tile plan '{TilePlan.TilePlanName}' has a tile plan with OverlapFactor out of range (must be >= 0 and < 0.5).");        
                    }
                    if(plan.ScaleWidth <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(plan.ScaleWidth), 
                            plan.ScaleWidth, 
                            $"Dynamic tile plan '{TilePlan.TilePlanName}' has a tile plan with non-positive ScaleWidth.");      
                    }
                }
            }   

            builder.Services.AddSingleton<List<DynamicTilePlan>>(DynamicTilePlans);


            var ServerConfigs = builder.Configuration
                .GetSection("AIServers")
                .Get<List<AIServerConfig>>();

            var Servers = new List<AIServer>();

            if (ServerConfigs != null)
            {
                foreach (var cfg in ServerConfigs)
                {
                    if (cfg.MovingAverageAlpha is <= 0 or > 1)
                        throw new ArgumentOutOfRangeException(
                            nameof(cfg.MovingAverageAlpha),
                            cfg.MovingAverageAlpha,
                            $"Value must be > 0 and <= 1 for server '{cfg.Name}'.");

                    if(cfg.TimeoutInSeconds <= 0)
                        throw new ArgumentOutOfRangeException(
                            nameof(cfg.TimeoutInSeconds),
                            cfg.TimeoutInSeconds,
                            $"Value must be > 0 for server '{cfg.Name}'.");     

                    if(cfg.Port < 0 || cfg.Port > 65535)
                        throw new ArgumentOutOfRangeException(
                            nameof(cfg.Port),
                            cfg.Port,
                            $"Value must be between 0 and 65535 for server '{cfg.Name}'.");     

                    

                    switch (cfg.Type)
                    {
                        case "CodeProjectAI":
                            Servers.Add(new CodeProjectAIServer(
                                cfg.ServerName,
                                cfg.Port,
                                cfg.Endpoint,
                                cfg.IsSSL,              
                                cfg.Name,
                                cfg.TimeoutInSeconds,
                                cfg.MovingAverageAlpha
                            ));
                            break;

                        case "Tensor":
                            Servers.Add(new TensorServer(
                                cfg.ServerName,
                                cfg.Port,
                                cfg.Endpoint,
                                cfg.IsSSL,                 
                                cfg.Name,
                                cfg.TimeoutInSeconds,
                                cfg.MovingAverageAlpha,
                                cfg.Labels,
                                cfg.MaxBatchSize
                            ));
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(cfg.Type),
                                cfg.Type,
                                $"Unknown server type for server '{cfg.Name}'.");
                    }
                }
            }
            int TimeoutCheck = builder.Configuration.GetValue<int>("InactiveServerCheckTimeoutSeconds");
            builder.Services.AddSingleton<AIServerList>(new AIServerList(Servers, TimeoutCheck));

            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
