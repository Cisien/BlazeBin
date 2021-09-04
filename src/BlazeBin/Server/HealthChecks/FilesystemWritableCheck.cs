using System.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazeBin.Server.HealthChecks;
public class FilesystemWritableCheck : IHealthCheck
{
    private readonly ILogger<FilesystemWritableCheck> _logger;
    private readonly string _basePath;

    public FilesystemWritableCheck(ILogger<FilesystemWritableCheck> logger, BlazeBinConfiguration config, IWebHostEnvironment env)
    {
        _logger = logger;
        _basePath = Path.Combine(config.BaseDirectory, env.EnvironmentName);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var testFilename = Path.Combine(_basePath,  "test.txt");
        try
        {
            await File.WriteAllTextAsync(testFilename, "test", cancellationToken);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Filesystem unwritable", ex);
        }
        finally
        {
            if (File.Exists(testFilename))
            {
                try
                {
                    File.Delete(testFilename);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unable to clean-up test write file", ex);
                }
            }
        }

        return HealthCheckResult.Healthy();
    }
}
