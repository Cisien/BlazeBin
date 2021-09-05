using BlazeBin.Shared;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazeBin.Server.HealthChecks;
public class FilesystemAvailableCheck : IHealthCheck
{
    private readonly string _basePath;
    private ILogger<FilesystemAvailableCheck> _logger;

    public FilesystemAvailableCheck(ILogger<FilesystemAvailableCheck> logger, BlazeBinConfiguration config)
    {
        _basePath = config.BaseDirectory;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_basePath))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Filesystem Unavailable"));
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Filesystem Unavailable");
            return Task.FromResult(HealthCheckResult.Unhealthy("Filesystem Unavailable", ex));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
