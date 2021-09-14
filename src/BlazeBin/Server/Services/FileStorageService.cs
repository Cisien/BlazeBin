using BlazeBin.Shared;

using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlazeBin.Server.Services;
public class FileStorageService : IStorageService
{
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _baseDirectory;

    public FileStorageService(ILogger<FileStorageService> logger, BlazeBinConfiguration config)
    {
        _logger = logger;
        _baseDirectory = Path.TrimEndingDirectorySeparator(config.BaseDirectory);
    }

    public async Task<FileData> WriteDataAsync(FileData data)
    {
        var hash = GetShaFromEncodedKey(data.Id).ToString();
        var path = Path.Combine(_baseDirectory, hash);
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _logger.LogDebug("Receiving file {File}", data with { Data = string.Empty });

        var json = JsonSerializer.SerializeToUtf8Bytes(data);
        await File.WriteAllBytesAsync(path, json);
        return data;

    }

    public async Task<FileData?> ReadDataAsync(string encodedName)
    {
        var hash = GetShaFromEncodedKey(encodedName).ToString();
        var path = Path.Combine(_baseDirectory, hash);
        if (!File.Exists(path))
        {
            return null;
        }

        using var file = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<FileData>(file);

        return data;
    }

    public Task DeleteOlderThan(TimeSpan duration, CancellationToken ct = default)
    {
        var files = Directory.GetFiles(_baseDirectory);
        _logger.LogDebug("Found {amount} files in {baseDirectory}.", files.Length, _baseDirectory);

        var amountRemoved = 0;
        foreach (var file in files)
        {
            if(ct.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            var created = File.GetCreationTimeUtc(file);
            if (created < DateTimeOffset.UtcNow.Subtract(duration))
            {
                _logger.LogDebug("Deleting {file}", file);
                File.Delete(file);
                amountRemoved++;
            }
        }

        _logger.LogDebug("Removed {amountRemoved} files older created before {date}", amountRemoved, DateTimeOffset.UtcNow.Subtract(duration));

        return Task.CompletedTask;
    }

    private static ReadOnlySpan<char> GetShaFromEncodedKey(ReadOnlySpan<char> fileKey)
    {
        var encoded = new Span<byte>(new byte[fileKey.Length]);
        Encoding.UTF8.GetBytes(fileKey, encoded);

        var hashed = new Span<byte>(new byte[32]);
        SHA256.HashData(encoded, hashed);

        var lowerHexKey = new Span<char>(new char[64]);
        Convert.ToHexString(hashed).AsSpan().ToLower(lowerHexKey, null);

        return lowerHexKey;
    }
}
