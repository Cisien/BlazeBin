using System;
using System.Text.Json;
using System.Threading.Tasks;

using BlazeBin.Client.Services;
using BlazeBin.Shared;

namespace BlazeBin.Server.Services;
public class ServerSideUploadService : IUploadService
{
    private readonly IStorageService _storage;

    public ServerSideUploadService(IStorageService storage)
    {
        _storage = storage;
    }

    public async Task<Result<FileBundle>> Get(string serverId)
    {
        var data = await _storage.ReadDataAsync(serverId);
        if (data == null)
        {
            return Result<FileBundle>.FromError("File not found");
        }

        try
        {
            var bundle = JsonSerializer.Deserialize<FileBundle>(data.Data);

            if (bundle == null)
            {
                return Result<FileBundle>.FromError("File was not a bundle");
            }

            return Result<FileBundle>.FromSuccess(bundle);
        }
        catch (JsonException)
        {
            return Result<FileBundle>.FromError("File was not a bundle");
        }
    }

    public Task<Result<string>> Set(FileBundle item)
    {
        throw new NotSupportedException("Writing files is not supported server-side");
    }

    public void SetAntiforgeryToken(string? token)
    {
    }
}
