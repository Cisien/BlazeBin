namespace BlazeBin.Server.Services;
public class FileGroomingWorker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileGroomingWorker> _logger;
    private readonly Timer _timer;

    private const int days = 30;

    public FileGroomingWorker(IServiceProvider serviceProvider, ILogger<FileGroomingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timer = new(async (o) => await Cleanup());
    }

    private async Task Cleanup()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        await storage.DeleteOlderThan(TimeSpan.FromDays(days));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var checkIntervalHours = 1;
        _logger.LogDebug("Starting file grooming for files older than {days} days using a check interval of {checkIntervalHours} hour", days, checkIntervalHours);
        _timer.Change(TimeSpan.FromHours(checkIntervalHours), TimeSpan.FromHours(checkIntervalHours));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _timer.Dispose();
        return Task.CompletedTask;
    }
}
