using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazeBin.Server.HealthChecks
{
    public class FilesystemAvailableCheck : IHealthCheck
    {
        private readonly string _basePath;

        public FilesystemAvailableCheck(IConfiguration config)
        {
            _basePath = string.IsNullOrWhiteSpace(config["BaseDirectory"]) ? "/app/data" : config["BaseDirectory"];
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if(!Directory.Exists(_basePath))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Filesystem Unavailable"));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}