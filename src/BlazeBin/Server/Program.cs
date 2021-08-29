using Microsoft.Extensions.Logging.Console;

namespace BlazeBin.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureLogging((ctx, bldr) =>
                        {
                            var isProd = ctx.HostingEnvironment.IsProduction();
                            bldr.SetMinimumLevel(LogLevel.Trace);
                            bldr.ClearProviders();
                            bldr.AddSimpleConsole(o =>
                            {
                                o.ColorBehavior = isProd ? LoggerColorBehavior.Disabled : LoggerColorBehavior.Enabled;
                                o.SingleLine = isProd;
                                o.TimestampFormat = "o";
                                o.UseUtcTimestamp = isProd;
                            });
                        })
                        .UseStartup<Startup>();
                });
    }
}
