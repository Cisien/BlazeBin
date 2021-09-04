using BlazeBin.Client.Services;
using BlazeBin.Shared.Services;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace BlazeBin.Client;
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
        builder.Services.AddSingleton<IKeyGeneratorService, AlphaKeyGeneratorService>();

        builder.Services.AddSingleton<IClientStorageService, ClientSideLocalStorageService>();
        builder.Services.AddSingleton<IUploadService, ClientSideUploadService>();
        builder.Services.AddSingleton<BlazeBinStateContainer>();

        await builder.Build().RunAsync();
    }
}
