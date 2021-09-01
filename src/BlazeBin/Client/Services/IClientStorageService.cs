
namespace BlazeBin.Client.Services;
public interface IClientStorageService
{
    Task<List<T>> Get<T>(string key) where T : class;
    Task Set<T>(string key, T item) where T : class;
}
