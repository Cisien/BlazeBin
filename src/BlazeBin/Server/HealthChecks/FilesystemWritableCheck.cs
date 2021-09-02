using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazeBin.Server.HealthChecks
{
    public class FilesystemWritableCheck : IHealthCheck
    {
        private readonly string _basePath;

        public FilesystemWritableCheck(IConfiguration config)
        {
            _basePath = string.IsNullOrWhiteSpace(config["BaseDirectory"]) ? "/app/data" : config["BaseDirectory"];
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
                return HealthCheckResult.Unhealthy("Filesystem unwritable", ex);
            }
            finally
            {
                if (File.Exists(testFilename))
                {
                    File.Delete(testFilename);
                }
            }

            return HealthCheckResult.Healthy();
        }
    }
}