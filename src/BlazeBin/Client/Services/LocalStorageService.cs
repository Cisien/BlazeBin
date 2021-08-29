using BlazeBin.Shared;
using Microsoft.JSInterop;
using System.Text.Json;

namespace BlazeBin.Client.Services;

public class LocalStorageService
{
    private const string LocalStorageGetItem = "window.localStorage.getItem";
    private const string LocalStorageSetItem = "window.localStorage.setItem";

    private readonly IJSRuntime _js;

    public LocalStorageService(IJSRuntime js)
    {
        _js = js;
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
        var storageItem = JsonSerializer.Serialize(item);
        await _js.InvokeVoidAsync(LocalStorageSetItem, key, storageItem);
    }

}
