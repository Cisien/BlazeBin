using System.Text.Json;

using BlazeBin.Client.Services;
using BlazeBin.Server.HealthChecks;
using BlazeBin.Server.Services;
using BlazeBin.Shared.Services;


namespace BlazeBin.Server
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;

        public Startup(IWebHostEnvironment env)
        {
            _env = env;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddAntiforgery(o =>
            {
                o.HeaderName = "X-XSRF-TOKEN";
                o.Cookie.Name = "X-XSRF-TOKEN";
                o.FormFieldName = "X-XSRF-TOKEN";

                o.Cookie.SecurePolicy = _env.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            });

            services.AddControllers();
            services.AddRazorPages();
            services.AddScoped<IKeyGeneratorService, AlphaKeyGeneratorService>();
            services.AddScoped<IStorageService, FileStorageService>();
            services.AddScoped<IClientStorageService, ServerSideClientStorageService>();
            services.AddScoped<IUploadService, ServerSideUploadService>();
            services.AddScoped<Client.BlazeBinStateContainer>();
            services.AddHealthChecks()
                .AddCheck<FilesystemAvailableCheck>("filesystem_availability")
                .AddCheck<FilesystemWritableCheck>("filesystem_writable");

            services.AddHostedService<FileGroomingWorker>();
            services.AddHostedService<StatsCollectionService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseForwardedHeaders();
                app.UseHttpsRedirection();
                app.UseHsts();

                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    AllowStatusCode404Response = false,
                    ExceptionHandler = async (context) =>
                    {
                        if (!context.Response.HasStarted)
                        {
                            context.Response.StatusCode = 500;
                            await context.Response.WriteAsJsonAsync(new { Error = "An unknown error has occurred." });
                        }
                    }
                });
            }

            app.UseHealthChecks("/health");
            app.UseHealthChecks("/robots933456.txt");

            app.Use((context, next) =>
            {
                if (context.Response.HasStarted)
                {
                    return next();
                }
                logger.LogCritical(JsonSerializer.Serialize(context.Request.Headers));
                context.Response.Headers.TryAdd("X-Frame-Options", "deny");
                context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                context.Response.Headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
                context.Response.Headers.TryAdd("Referrer-Policy", "same-origin");
                context.Response.Headers.TryAdd("Cross-Origin-Embedder-Policy", "require-corp");
                context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
                context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");
                return next();
            });

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
