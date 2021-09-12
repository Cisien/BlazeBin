using BlazeBin.Shared;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazeBin.Server.HealthChecks;
public class FilesystemWritableCheck : IHealthCheck
{
    private static ILogger<FilesystemWritableCheck>? _logger;
    private static string? _basePath;
    private static HealthCheckResult _lastResult = HealthCheckResult.Degraded("This check has not run yet");

    private static readonly Lazy<Timer> CheckTimer = new(new Timer(async (_) => await DoCheck(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1)));

    public FilesystemWritableCheck(ILogger<FilesystemWritableCheck> logger, BlazeBinConfiguration config, IWebHostEnvironment env)
    {
        _logger ??= logger;
        _basePath ??= Path.Combine(config.BaseDirectory, env.EnvironmentName);
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }

        if (!CheckTimer.IsValueCreated)
        {
            _ = CheckTimer.Value;
        }
    }

    private static async Task DoCheck()
    {
        if (_basePath == null)
        {
            return;
        }

        var testFilename = Path.Combine(_basePath, "test.txt");
        try
        {
            await File.WriteAllTextAsync(testFilename, "test");
        }
        catch (Exception ex)
        {
            _lastResult = HealthCheckResult.Unhealthy("Filesystem unwritable", ex);
            return;
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
                    _logger?.LogError("Unable to clean-up test write file", ex);
                }
            }
        }

        _lastResult = HealthCheckResult.Healthy();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_lastResult);
    }
}
