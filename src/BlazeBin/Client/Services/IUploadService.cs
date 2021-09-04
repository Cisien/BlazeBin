
using BlazeBin.Shared;

namespace BlazeBin.Client.Services;
public interface IUploadService
{
    Task<Result<FileBundle>> Get(string serverId);
    Task<Result<string>> Set(FileBundle item);
    void SetAntiforgeryToken(string? token);
}
