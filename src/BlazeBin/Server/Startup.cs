using BlazeBin.Client.Services;
using BlazeBin.Server.HealthChecks;
using BlazeBin.Server.Services;
using BlazeBin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace BlazeBin.Server
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IKeyGeneratorService, AlphaKeyGeneratorService>();
            services.AddScoped<IStorageService, FileStorageService>();
            services.AddControllers();
            services.AddRazorPages();
            services.AddHostedService<FileGroomingWorker>();
            services.AddScoped<IClientStorageService, ServerSideClientStorageService>();
            services.AddScoped<IUploadService, ServerSideUploadService>();
            services.AddScoped<Client.BlazeBinStateContainer>();
            services.AddHealthChecks()
                .AddCheck<FilesystemAvailableCheck>("filesystem_availability")
                .AddCheck<FilesystemWritableCheck>("filesystem_writable");

            services.AddApplicationInsightsTelemetry();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    AllowStatusCode404Response = false,
                    ExceptionHandler = async (context) =>
                    {
                        if (!context.Response.HasStarted)
                        {
                            context.Response.ContentType = "application/json";
                            var response = @"{""error"": ""An unknown error has occurred.""}";
                            var bytes = Encoding.UTF8.GetBytes(response);
                            await context.Response.BodyWriter.WriteAsync(bytes);
                        }
                    }
                });
            }
            app.UseHealthChecks("/health");
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
                endpoints.MapFallbackToFile("/_Host");
            });
        }
    }
}
