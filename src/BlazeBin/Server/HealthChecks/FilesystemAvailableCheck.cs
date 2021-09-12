﻿using BlazeBin.Shared;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlazeBin.Server.HealthChecks;
public class FilesystemAvailableCheck : IHealthCheck
{
    private static string? _basePath;
    private static ILogger<FilesystemAvailableCheck>? _logger;
    private static readonly Lazy<Timer> CheckTimer = new(new Timer(DoCheck, null, TimeSpan.Zero, TimeSpan.FromMinutes(1)));

    private static HealthCheckResult _lastResult;

    public FilesystemAvailableCheck(ILogger<FilesystemAvailableCheck> logger, BlazeBinConfiguration config)
    {
        _basePath ??= config.BaseDirectory;
        _logger ??= logger;

        if(!CheckTimer.IsValueCreated)
        {
            _ = CheckTimer.Value;
        }
    }

    private static void DoCheck(object? state)
    {
        if(_basePath == null)
        {
            return;
        }

        try
        {
            if (!Directory.Exists(_basePath))
            {
                _lastResult = HealthCheckResult.Unhealthy("Filesystem Unavailable");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Filesystem Unavailable");
            _lastResult = HealthCheckResult.Unhealthy("Filesystem Unavailable", ex);
            return;
        }

        _lastResult = HealthCheckResult.Healthy();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_lastResult);
    }
}
