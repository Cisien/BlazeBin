
using Microsoft.ApplicationInsights;
using System.Diagnostics;

namespace BlazeBin.Server.Services;
public class StatsCollectionService : IHostedService
{
    private readonly Timer _timer;
    private readonly BlazeBinConfiguration _config;
    private readonly IServiceProvider _provider;
    private readonly ILogger<StatsCollectionService> _logger;
    private readonly CancellationTokenSource _cts;

    public StatsCollectionService(IServiceProvider provider, ILogger<StatsCollectionService> logger, BlazeBinConfiguration config)
    {
        _cts = new CancellationTokenSource();
        _timer = new(async (_) => await CollectStats());

        _config = config;
        _provider = provider;
        _logger = logger;
    }

    private async Task CollectStats()
    {
        var ctsToken = _cts.Token;
        if (ctsToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var collectionTimer = Stopwatch.StartNew();
        
            await using var scope = _provider.CreateAsyncScope();
            var telemetryClient = scope.ServiceProvider.GetService<TelemetryClient>();

            if (telemetryClient == null)
            {
                return;
            }

            var entries = Directory.GetFiles(_config.BaseDirectory).Select(a => new FileInfo(a)).ToList();
            var count = entries.Count;
            var size = 0L;
            var oldest = DateTime.UtcNow;

            var largest = 0L;
            var largestFilename = "";

            foreach (var entry in entries)
            {
                if(ctsToken.IsCancellationRequested)
                {
                    return;
                }

                size += entry.Length;
                oldest = oldest > entry.CreationTimeUtc ? entry.CreationTimeUtc : oldest;

                if (entry.Length > largest)
                {
                    largest = entry.Length;
                    largestFilename = entry.Name;
                }
            }

            collectionTimer.Stop();
            var oldestDays = (DateTime.UtcNow - oldest).TotalDays;

            if (_config.Stats.LogToAppInsights)
            {
                telemetryClient.GetMetric("stats-count").TrackValue(count);
                telemetryClient.GetMetric("stats-size").TrackValue(size);
                telemetryClient.GetMetric("stats-oldest").TrackValue(oldestDays);
                telemetryClient.GetMetric("stats-largest-file-size", "stats-largest-file-name").TrackValue(largest, largestFilename);
                telemetryClient.GetMetric("stats-collection-time").TrackValue(collectionTimer.Elapsed.TotalSeconds);
            }
            if (_config.Stats.LogToMSEL)
            {
                _logger.LogInformation("stats-count: {count}", count);
                _logger.LogInformation("stats-size: {size}", size);
                _logger.LogInformation("stats-oldest: {oldest} ({date})", oldestDays, oldest);
                _logger.LogInformation("stats-largest-file-size: {filesize}; stats-largest-file-name: {filename}", largest, largestFilename);
                _logger.LogInformation("stats-collection-time: {collectionTime}", collectionTimer.Elapsed.TotalSeconds);
            }
        }
        catch(Exception ex)
        {
            _logger.LogError("Exception while collecting statistics", ex);
        }
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if(!_config.Stats.Enabled)
        {
            return Task.CompletedTask;
        }

        _timer.Change(TimeSpan.Zero, _config.Stats.CollectionInterval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        _cts.Dispose();
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }
}
