using BlazeBin.Client.Services;
using BlazeBin.Shared;

namespace BlazeBin.Server.Services;
public class ServerSideUploadService : IUploadService
{
    public Task<Result<FileBundle>> Get(string serverId)
    {
        var bundle = FileBundle.New(serverId, "nothing");
        bundle.LastServerId = serverId;
        return Task.FromResult(Result<FileBundle>.FromSuccess(bundle));
    }

    public Task<Result<string>> Set(FileBundle item)
    {
        throw new NotSupportedException("Writing files is not supported server-side");
    }

    public void SetAntiforgeryToken(string? token)
    {
    }
}
