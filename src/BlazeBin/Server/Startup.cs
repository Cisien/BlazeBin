using System.Net;
using System.Text.Json;

using Azure.Identity;

using BlazeBin.Client.Services;
using BlazeBin.Server.HealthChecks;
using BlazeBin.Server.Services;
using BlazeBin.Shared.Services;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace BlazeBin.Server
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;
        private readonly BlazeBinConfiguration _config;

        public Startup(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            var blazebinConfig = new BlazeBinConfiguration();
            config.GetRequiredSection("BlazeBin").Bind(blazebinConfig, o => o.BindNonPublicProperties = true);
            _config = blazebinConfig;
            Console.WriteLine($"Configuration: {JsonSerializer.Serialize(blazebinConfig)}");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_config);
            services.AddApplicationInsightsTelemetry();
            var dataProtection = services.AddDataProtection();

            if (_config.DataProtection.Enabled)
            {
                dataProtection.PersistKeysToFileSystem(new DirectoryInfo(_config.DataProtection.KeyLocation));
                if (!_env.IsDevelopment())
                {
                    dataProtection.ProtectKeysWithAzureKeyVault(new Uri(_config.DataProtection.KeyIdentifier),
                        new DefaultAzureCredential());
                }
            }

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

            if (_config.Hosting.UseForwardedHeaders)
            {
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    // These three subnets encapsulate the applicable Azure subnets. At the moment, it's not possible to narrow it down further.
                    foreach (var network in _config.Hosting.KnownNetworks)
                    {
                        options.KnownNetworks.Add(new(IPAddress.Parse(network[0]), int.Parse(network[1])));
                    }
                    foreach(var proxy in _config.Hosting.KnownProxies)
                    {
                        options.KnownProxies.Add(IPAddress.Parse(proxy));
                    }

                    if(_config.Hosting.ProtoHeadername != null)
                    {
                        options.ForwardedProtoHeaderName = _config.Hosting.ProtoHeadername;
                    }
                    if(_config.Hosting.ForwardedForHeaderName != null)
                    {
                        options.ForwardedForHeaderName = _config.Hosting.ForwardedForHeaderName;
                    }
                });
            }
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
                if (_config.Hosting.UseForwardedHeaders)
                {
                    app.Use((ctx, next) =>
                    {
                        logger.LogInformation("before-forwarded-headers scheme: {scheme}; all headers: {headers}", ctx.Request.Scheme, JsonSerializer.Serialize(ctx.Request.Headers));
                        return next();
                    });
                    app.UseForwardedHeaders();
                    app.Use((ctx, next) =>
                    {
                        logger.LogInformation("after-forwarded-headers scheme: {scheme}; all headers: {headers}", ctx.Request.Scheme, JsonSerializer.Serialize(ctx.Request.Headers));
                        return next();
                    });
                }
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
