using Microsoft.JSInterop;

using System.Text.Encodings.Web;
using System.Text.Json;

namespace BlazeBin.Client.Services;

public class ClientSideLocalStorageService : IClientStorageService
{
    private const string LocalStorageGetItem = "window.localStorage.getItem";
    private const string LocalStorageSetItem = "window.localStorage.setItem";

    private readonly IJSRuntime _js;
    private readonly JsonSerializerOptions _serializerOpts;

    public ClientSideLocalStorageService(IJSRuntime js)
    {
        _js = js;
        _serializerOpts = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<List<T>> Get<T>(string key) where T : class
    {
        var localStorageItem = await _js.InvokeAsync<string>(LocalStorageGetItem, key);

        if (string.IsNullOrEmpty(localStorageItem))
        {
            var empty = new List<T>();
            await Set(key, empty);
            return empty;
        }

        try
        {
            var result = JsonSerializer.Deserialize<List<T>>(localStorageItem);
            if(result == null)
            {
                var empty = new List<T>();
                await Set(key, empty);
                return empty;
            }

            return result;
        }
        catch (JsonException)
        {
            var empty = new List<T>();
            await Set(key, empty);
            return empty;
        }
    }

    public async Task Set<T>(string key, T item) where T: class
    {
        var storageItem = JsonSerializer.Serialize(item, _serializerOpts);
        await _js.InvokeVoidAsync(LocalStorageSetItem, key, storageItem);
    }

}
