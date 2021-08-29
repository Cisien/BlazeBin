using BlazeBin.Shared;
using System.Threading.Tasks;

namespace BlazeBin.Server.Services
{
    public interface IStorageService
    {
        Task<FileData?> ReadDataAsync(string encodedName);
        Task<FileData> WriteDataAsync(FileData data);
        Task DeleteOlderThan(TimeSpan timeSpan);
    }
}