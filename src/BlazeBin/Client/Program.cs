using BlazeBin.Client.Services;
using BlazeBin.Shared.Services;
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
            await builder.Build().RunAsync();
        }
    }
}
