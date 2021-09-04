using System.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazeBin.Server.HealthChecks;
public class FilesystemWritableCheck : IHealthCheck
{
    private readonly ILogger<FilesystemWritableCheck> _logger;
    private readonly string _basePath;

    public FilesystemWritableCheck(ILogger<FilesystemWritableCheck> logger, BlazeBinConfiguration config)
    {
        _logger = logger;
        _basePath = config.BaseDirectory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var testFilename = Path.Combine(_basePath, "test.txt");
        try
        {
            await File.WriteAllTextAsync(testFilename, "test", cancellationToken);
        }
        catch (Exception ex)
        {
            var psi = new ProcessStartInfo("du", "-h")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var p = Process.Start(psi);
            p.WaitForExit();
            var output = p.StandardOutput.ReadToEnd();
            _logger.LogError("mounts: {mounts}", output);

            var root = Directory.GetDirectories("/");
            _logger.LogError("rootDirs: {dirs}", string.Join(", ", root));
            var app = Directory.GetDirectories("/app");
            _logger.LogError("appDirs: {appDirs}", string.Join(", ", app));

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
