namespace BlazeBin.Server.Services;
public class ServerSideClientStorageService : Client.Services.IClientStorageService
{
    private readonly Dictionary<string, object> _memStorage = new();
    public Task<List<T>> Get<T>(string key) where T : class
    {
        if(!_memStorage.TryGetValue(key, out var value))
        {
            value = new List<T>();
            _memStorage.Add(key, value);
        }

        return Task.FromResult((List<T>)value);
    }

    public Task Set<T>(string key, T item) where T : class
    {
        _memStorage.TryAdd(key, item);
        return Task.CompletedTask;
    }
}
