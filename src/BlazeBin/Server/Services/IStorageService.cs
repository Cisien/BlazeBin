using System;
using System.Threading;
using System.Threading.Tasks;

using BlazeBin.Shared;

namespace BlazeBin.Server.Services;
public interface IStorageService
{
    Task<FileData?> ReadDataAsync(string encodedName);
    Task<FileData> WriteDataAsync(FileData data);
    Task DeleteOlderThan(TimeSpan timeSpan, CancellationToken ct = default);
}
