
using BlazeBin.Client.Services;
using BlazeBin.Shared;
using System.Text.Json;

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
        try
        {
            var contents = data?.Data;

            if(contents == null)
            {
                return Result<FileBundle>.FromError("File Bundle doesn't exist or had no content.");
            }

            var bundle = JsonSerializer.Deserialize<FileBundle>(contents);
            if(bundle == null)
            {
                return Result<FileBundle>.FromError("File Bundle doesn't exist or had no content.");
            }
            bundle.LastServerId = serverId;
            return Result<FileBundle>.FromSuccess(bundle);
        }
        catch(JsonException)
        {
            return Result<FileBundle>.FromError("File Bundle doesn't exist or had no content."); ;
        }
    }

    public Task<Result<string>> Set(FileBundle item)
    {
        throw new NotSupportedException("Writing files is not supported server-side");
    }
}
