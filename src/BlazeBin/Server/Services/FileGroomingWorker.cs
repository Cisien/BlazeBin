using System;
using System.Threading;
using System.Threading.Tasks;

using BlazeBin.Shared;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazeBin.Server.Services;
public class FileGroomingWorker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileGroomingWorker> _logger;
    private readonly BlazeBinConfiguration _config;
    private readonly Timer _timer;
    private readonly CancellationTokenSource _cts;

    public FileGroomingWorker(IServiceProvider serviceProvider, ILogger<FileGroomingWorker> logger, BlazeBinConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config;
        _timer = new(async (o) => await Cleanup());
        _cts = new CancellationTokenSource();
    }

    private async Task Cleanup()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        await storage.DeleteOlderThan(_config.Grooming.MaxAge, _cts.Token);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if(!_config.Grooming.Enabled)
        {
            return Task.CompletedTask;
        }

        _logger.LogDebug("Starting file grooming for files older than {days} days using a check interval of {checkIntervalHours}", _config.Grooming.MaxAge, _config.Grooming.Interval);
        _timer.Change(TimeSpan.Zero, _config.Grooming.Interval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _cts.Cancel();
        _timer.Dispose();
        _cts.Dispose();
        return Task.CompletedTask;
    }
}
