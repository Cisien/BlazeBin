using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                        .ConfigureAppConfiguration((ctx,bldr) => {
                            bldr.AddEnvironmentVariables("blazebin_")
                            .AddApplicationInsightsSettings(developerMode: ctx.HostingEnvironment.IsDevelopment());
                        })
                        .ConfigureLogging((ctx, bldr) =>
                        {
                            var isDev = ctx.HostingEnvironment.IsDevelopment();
                            bldr.SetMinimumLevel(LogLevel.Trace);
                            bldr.ClearProviders();
                            bldr.AddSimpleConsole(o =>
                            {
                                o.ColorBehavior = isDev ? LoggerColorBehavior.Enabled : LoggerColorBehavior.Disabled;
                                o.SingleLine = !isDev;
                                o.TimestampFormat = "o";
                                o.UseUtcTimestamp = !isDev;
                            });
                            bldr.AddApplicationInsights(o => {
                                o.FlushOnDispose = true;
                                o.IncludeScopes = true;
                                o.TrackExceptionsAsExceptionTelemetry = true;
                            });
                        })
                        .UseStartup<Startup>();
                });
    }
}
