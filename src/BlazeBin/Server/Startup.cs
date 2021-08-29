using BlazeBin.Server.Services;
using BlazeBin.Shared.Services;
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
            services.AddHostedService<FileGroomingWorker>(); 
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

            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("/{param?}", "index.html");
            });
        }
    }
}
