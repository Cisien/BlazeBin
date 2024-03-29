using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;

using Azure.Identity;

using BlazeBin.Client.Services;
using BlazeBin.Server.HealthChecks;
using BlazeBin.Server.Services;
using BlazeBin.Shared;
using BlazeBin.Shared.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

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

            services.AddResponseCompression(o =>
            {
                o.EnableForHttps = true;
                o.MimeTypes = new List<string>{
                    "application/json",
                    "application/javascript",
                    "text/css"
                };
            });

            services.AddControllers();
            services.AddRazorPages();
            services.AddScoped<IKeyGeneratorService, AlphaKeyGeneratorService>();
            services.AddScoped<IStorageService, FileStorageService>();
            services.AddScoped<IClientStorageService, ServerSideClientStorageService>();
            services.AddScoped<IUploadService, ServerSideUploadService>();
            services.AddScoped(provider =>
            {
                var state = ActivatorUtilities.CreateInstance<Client.BlazeBinStateContainer>(provider);
                state.IsServerSideRender = true;
                state.CreateUpload(true).GetAwaiter().GetResult();
                return state;
            });
            services.AddHealthChecks()
                .AddCheck<FilesystemAvailableCheck>("filesystem_availability")
                .AddCheck<FilesystemWritableCheck>("filesystem_writable");

            services.AddHostedService<FileGroomingWorker>();

            if (!_env.IsDevelopment())
            {
                services.AddHttpsRedirection(o =>
                {
                    o.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
                    o.HttpsPort = 443;
                });
            }

            if (_config.Hosting.UseForwardedHeaders)
            {
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    foreach (var network in _config.Hosting.KnownNetworks)
                    {
                        options.KnownNetworks.Add(new(IPAddress.Parse(network[0]), int.Parse(network[1])));
                    }
                    if (_config.Hosting.KnownProxies.Count == 0)
                    {
                        // hack hack hack hack hack
                        for (var i = 0; i < 256; i++)
                        {
                            for (var j = 0; j < 256; j++)
                            {
                                options.KnownProxies.Add(IPAddress.Parse($"[::ffff:169.254.{i}.{j}]"));
                            }
                        }
                    }

                    foreach (var proxy in _config.Hosting.KnownProxies)
                    {
                        options.KnownProxies.Add(IPAddress.Parse(proxy));
                    }

                    if (_config.Hosting.ProtoHeadername != null)
                    {
                        options.ForwardedProtoHeaderName = _config.Hosting.ProtoHeadername;
                    }
                    if (_config.Hosting.ForwardedForHeaderName != null)
                    {
                        options.ForwardedForHeaderName = _config.Hosting.ForwardedForHeaderName;
                    }
                });
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            app.UseHealthChecks("/health");
            app.UseHealthChecks("/robots933456.txt");

            if (_config.Hosting.RedirecFromWww)
            {
                app.UseRewriter(new RewriteOptions()
                    .AddRedirectToNonWwwPermanent());
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                if (_config.Hosting.UseForwardedHeaders)
                {
                    app.UseForwardedHeaders();
                }

                app.UseHttpsRedirection();
                app.UseHsts();

                app.Use((context, next) =>
                {
                    context.Response.Headers.TryAdd("X-Frame-Options", "deny");
                    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
                    context.Response.Headers.TryAdd("Referrer-Policy", "same-origin");
                    return next();
                });

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

            app.UseResponseCompression();

            app.UseBlazorFrameworkFiles();
            if (_env.IsDevelopment())
            {
                app.UseStaticFiles();
            }
            else
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    OnPrepareResponse = ctx =>
                    {
                        ctx.Context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                        {
                            MaxAge = TimeSpan.FromDays(1),
                            Public = true
                        };
                    }
                });
            }

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
