using BlazeBin.Client.Services;
using BlazeBin.Shared.Services;
using BlazorApplicationInsights;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace BlazeBin.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
                        
            builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddSingleton<IKeyGeneratorService, AlphaKeyGeneratorService>();

            builder.Services.AddSingleton<BlazeBinStateContainer>();
            builder.Services.AddSingleton<LocalStorageService>();

            builder.Services.AddBlazorApplicationInsights(async applicationInsights =>
            {
                var telemetryItem = new TelemetryItem()
                {
                    Tags = new Dictionary<string, object>()
                    {
                        { "ai.cloud.role", "SPA" },
                        { "ai.cloud.roleInstance", "Blazebin WASM" },
                    }
                };

                await applicationInsights.SetInstrumentationKey("140cb61d-0bfd-43c6-aa2a-61bb74b24f46");
                await applicationInsights.LoadAppInsights();

                await applicationInsights.AddTelemetryInitializer(telemetryItem);
            });

            await builder.Build().RunAsync();
        }
    }
}
