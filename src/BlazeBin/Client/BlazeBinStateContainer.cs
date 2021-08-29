
using BlazeBin.Client.Services;
using BlazeBin.Shared;
using BlazeBin.Shared.Services;
using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace BlazeBin.Client;
public class BlazeBinStateContainer
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly IKeyGeneratorService _keygen;
    private readonly LocalStorageService _storage;

    private const string HistoryPushState = "window.history.pushState";
    private const string UploadListKey = "upload-list";
    private const string HistoryListKey = "history-list";
    private const string FavoritesListKey = "favorites-list";

    public const int GeneratedIdLength = 12;

    public List<string>? History { get; private set; }
    public List<string>? Favorites { get; private set; }
    public List<FileBundle>? Uploads { get; private set; }

    public FileBundle? _adHocBundle;
    public int _activeUploadIndex = -1;
    public int _activeFileIndex = -1;


    public FileData? ActiveFile
    {
        get
        {
            var bundle = ActiveUpload;

            if (bundle == null)
            {
                return null;
            }

            if (_activeFileIndex >= 0 && _activeFileIndex < bundle.Files.Count)
            {
                return bundle.Files[_activeFileIndex];
            }
            return null;
        }
    }

    public FileBundle? ActiveUpload
    {
        get
        {
            if (_adHocBundle != null)
            {
                return _adHocBundle;
            }

            if (Uploads == null)
            {
                return null;
            }

            var isOor = _activeUploadIndex < 0 || _activeUploadIndex > Uploads.Count -1;
            if (isOor)
            {
                return null;
            }

            return Uploads[_activeUploadIndex];
        }
    }

    public bool IsInitialized { get; private set; }

    public Error? Error { get; private set; }
    public bool DisplayError { get; set; }

    public event Func<Task>? OnChange;

    public BlazeBinStateContainer(IJSRuntime jsRuntime, HttpClient http, IKeyGeneratorService keygen, LocalStorageService storage)
    {
        _js = jsRuntime;
        _http = http;
        _keygen = keygen;
        _storage = storage;
    }

    public async Task InitializeUploadLists()
    {
        Uploads = await _storage.Get<FileBundle>(UploadListKey);
        if (Uploads.Count < 0)
        {
            await SelectUpload(0);
        }

        Favorites = await _storage.Get<string>(FavoritesListKey);
        History = await _storage.Get<string>(HistoryListKey);
        Favorites = new List<string> { "future" };
    }

    #region uploads
    public async Task CreateUpload(bool setActive)
    {
        var upload = FileBundle.New(_keygen.GenerateKey(GeneratedIdLength).ToString(), _keygen.GenerateKey(GeneratedIdLength).ToString());
        await InsertUpload(upload, setActive);
    }

    public async Task InsertUpload(FileBundle upload, bool setActive)
    {
        _ = Uploads ?? throw new ArgumentException(nameof(Uploads));

        var existingIndex = Uploads.FindIndex(a => a.Id == upload.Id);
        var index = Math.Max(existingIndex, 0);

        Uploads.Insert(index, upload);

        if (Uploads.Count > 10)
        {
            Uploads.RemoveRange(10, Uploads.Count - 10);
        }

        await _storage.Set(UploadListKey, Uploads);
        if (setActive)
        {
            await SelectUpload(index);
        }
    }

    public async Task ReadUpload(string serverId)
    {
        _ = Uploads ?? throw new ArgumentException(nameof(Uploads));

        var response = await _http.GetAsync($"raw/{serverId}");
        if (!response.IsSuccessStatusCode)
        {
            ShowError($"Unable to load {serverId}", $"Server responded with {response.StatusCode}");
            return;
        }

        try
        {
            var fromApi = await response.Content.ReadFromJsonAsync<FileBundle>();
            if (fromApi == null)
            {
                ShowError($"Unable to load {serverId}", $"Server returned unexpected data");
                return;
            }
            fromApi.LastServerId = serverId;
            var existingIndex = Uploads.FindIndex(a => a.Id == fromApi.Id);
            if (existingIndex != -1)
            {
                await SelectUpload(existingIndex);
            }
            else
            {
                _adHocBundle = fromApi;
            }
            _ = ActiveUpload ?? throw new InvalidOperationException("ActiveUpload returns null after loading an upload");
        }
        catch (JsonException)
        {
            ShowError($"Unable to load {serverId}", $"Server returned unexpected data");
            return;
        }

        if (ActiveUpload.Files.Count == 0)
        {
            ShowError($"Unable to load {serverId}", "Server data contained no files");
            return;
        }

        SetActiveFile(0);

        if (!Uploads.Any(a => a.Id == ActiveUpload?.Id))
        {
            await CreateHistory(serverId);
        }
    }

    public async Task DeleteUpload(string id)
    {
        _ = Uploads ?? throw new ArgumentException(nameof(Uploads));

        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        var uploadIndex = Uploads.FindIndex(a => a.Id == id);
        if (uploadIndex == -1)
        {
            throw new InvalidOperationException("Attempted to remove an upload that wasn't in the list");
        }

        Uploads.RemoveAt(uploadIndex);

        await _storage.Set(UploadListKey, Uploads);

        if (Uploads.Count == 0)
        {
            SetActiveFile(-1);
            await SelectUpload(-1);
            return;
        }

        if (uploadIndex < 0)
        {
            uploadIndex = 0;
        }

        if (uploadIndex > Uploads.Count - 1)
        {
            uploadIndex = Uploads.Count - 1;
        }

        await SelectUpload(uploadIndex);
    }

    public async Task SelectUpload(int index)
    {
        if (_activeUploadIndex == index)
        {
            return;
        }

        _activeUploadIndex = index;

        if (index == -1)
        {
            SetActiveFile(index);
            return;
        }

        _adHocBundle = null;
        if (ActiveUpload!.Files.Count > 0)
        {
            SetActiveFile(0);
        }

        if (ActiveUpload.LastServerId != null)
        {
            await _js.InvokeVoidAsync(HistoryPushState, "", "", $"/{ActiveUpload.LastServerId}");
        }
    }

    public async Task SaveActiveUpload()
    {
        _ = Uploads ?? throw new ArgumentException(nameof(Uploads));

        if (ActiveUpload == null)
        {
            throw new InvalidOperationException("Attempting to save a null active upload");
        }

        if (ActiveUpload!.Id == null)
        {
            throw new InvalidOperationException("Attempting to save an active upload that doesn't have an id!");
        }

        if (ActiveUpload.LastServerId != null)
        {
            return;
        }

        var body = new MultipartFormDataContent();
        var contentJson = JsonSerializer.Serialize(ActiveUpload);

        if (string.IsNullOrWhiteSpace(contentJson))
        {
            ShowError($"Failed to save {ActiveUpload.Id}", "No data to upload");
            return;
        }

        body.Add(new StringContent(contentJson, Encoding.UTF8, "application/json"), "file", ActiveUpload.Id);

        var response = await _http.PostAsync("submit", body);
        if (!response.IsSuccessStatusCode)
        {
            ShowError($"Failed to save {ActiveUpload.Id}", $"Server responded with { response.StatusCode }");
            return;
        }

        var content = await response.Content.ReadFromJsonAsync<FileData>();
        if (content == null)
        {
            ShowError($"Failed to save {ActiveUpload.Id}", $"Server response was unexpected");
            return;
        }

        ActiveUpload.LastServerId = content.Id;

        if (!Uploads.Any(a => a.Id == ActiveUpload.Id))
        {
            await InsertUpload(ActiveUpload, true);
            if (_adHocBundle != null)
            {
                _activeFileIndex = -1;
                _adHocBundle = null;
            }
        }

        await _js.InvokeVoidAsync(HistoryPushState, "", "", $"/{ActiveUpload.LastServerId}");
        await _storage.Set(UploadListKey, Uploads);
    }

    public async Task SetActiveUploadDirty()
    {
        if (ActiveUpload == null)
        {
            throw new InvalidOperationException("Attempting to set dirty state on an active upload that doesn't exist");
        }

        if (ActiveUpload.LastServerId == null)
        {
            return;
        }

        ActiveUpload.LastServerId = null;
        await _js.InvokeVoidAsync(HistoryPushState, "", "", $"/");
    }
    #endregion

    #region files
    public void CreateFile(string filename, bool setActive)
    {
        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        if (ActiveUpload.Files.Any(a => a.Filename == filename))
        {
            ShowError("Name Conflict", $"The current set already contains file with the name of {filename}");
            return;
        }

        var id = _keygen.GenerateKey(GeneratedIdLength).ToString();
        var newFile = FileData.Empty with { Id = id, Filename = filename.ToString() };
        ActiveUpload.Files.Add(newFile);
        ActiveUpload.LastServerId = null;
        if (setActive)
        {
            SetActiveFile(ActiveUpload.Files.IndexOf(newFile));
        }

    }

    public void UpdateFile(string id, string contents) // maybe do filename also later?
    {
        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        var file = ActiveUpload.Files.SingleOrDefault(a => a.Id == id);
        if (file == null)
        {
            return;
        }

        if (file.Data == contents)
        {
            return;
        }

        var index = ActiveUpload.Files.IndexOf(file);
        ActiveUpload.Files[index] = file with { Data = contents };
        ActiveUpload.LastServerId = null;
    }

    public void DeleteFile(string id)
    {
        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        var fileIndex = ActiveUpload.Files.FindIndex(a => a.Id == id);
        if (fileIndex == -1)
        {
            return;
        }
        ActiveUpload.Files.RemoveAt(fileIndex);
        ActiveUpload.LastServerId = null;
        fileIndex--;

        if (fileIndex >= ActiveUpload.Files.Count)
        {
            fileIndex = ActiveUpload.Files.Count - 1;
        }

        if (fileIndex > -1)
        {
            SetActiveFile(fileIndex);
        }
    }

    public void SetActiveFile(int index)
    {
        if(index == -1)
        {
            _activeFileIndex = index;
            return;
        }

        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        if (index == -1)
        {
            _activeFileIndex = -1;
            return;
        }

        if (ActiveFile?.Id == ActiveUpload.Files[index].Id)
        {
            return;
        }

        var isOutOfRange = index >= ActiveUpload.Files.Count;
        if (isOutOfRange)
        {
            return;
        }

        _activeFileIndex = index;
    }
    #endregion

    #region history
    public async Task CreateHistory(string serverId)
    {
        _ = History ?? throw new ArgumentException(nameof(History));

        if (History.Any(a => a == serverId))
        {
            return;
        }

        History.Insert(0, serverId);
        if (History.Count >= 10)
        {
            History.RemoveRange(10, History.Count - 10);
        }

        await _storage.Set(HistoryListKey, History);
    }

    public async Task DeleteHistory(string serverId)
    {
        _ = History ?? throw new ArgumentException(nameof(History));

        var historyItem = History.SingleOrDefault(a => a == serverId);
        if (historyItem == null)
        {
            return;
        }

        History.Remove(historyItem);
        await _storage.Set(HistoryListKey, History);
    }

    #endregion

    #region favorites
    public Task CreateFavorite(string serverId)
    {
        // NYI

        return Task.CompletedTask;
        //if (Favorites.Any(a => a == serverId))
        //{
        //    return;
        //}

        //Favorites.Insert(0, serverId);
        //if (Favorites.Count >= 10)
        //{
        //    Favorites.RemoveRange(10, Favorites.Count - 10);
        //}

        //await _storage.Set(FavoritesListKey, Favorites);
    }

    public Task DeleteFavorite(string serverId)
    {
        // NYI
        return Task.CompletedTask;
        //var favoriteItem = Favorites.SingleOrDefault(a => a == serverId);
        //if (favoriteItem == null)
        //{
        //    return;
        //}

        //Favorites.Remove(favoriteItem);
        //await _storage.Set(FavoritesListKey, Favorites);
    }
    #endregion

    #region ErrorDialog

    public void ResetMessage()
    {
        Error = null;
        DisplayError = false;
    }

    public void ShowError(string title, string message)
    {
        Error = new Error(title, message);
        DisplayError = true;
    }
    #endregion

    #region Dispatch
#if DEBUG
    public async Task Dispatch(Expression<Func<Task>> work, [CallerMemberName] string method = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = -1)
    {
        try
        {
            await work.Compile()();
        }
        catch (Exception ex)
        {
            ShowError("Unhandled Exception", ex.ToString());
        }
        Console.WriteLine($"State change call initiated from {method} {filePath}: {lineNumber}. work body: {work.Body}");
        Console.WriteLine("State: " + JsonSerializer.Serialize(this, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, IgnoreReadOnlyFields = false }));

        await StateHasChanged();
    }
#else
    public async Task Dispatch(Func<Task> work)

        {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            ShowError("Unhandled Exception", ex.Message);
        }
        await StateHasChanged();
    }
#endif

#if DEBUG
    public async Task Dispatch(Expression<Action> work, [CallerMemberName] string method = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = -1)
    {
        try
        {
            work.Compile()();
        }
        catch (Exception ex)
        {
            ShowError("Unhandled Exception", ex.ToString());
        }
        Console.WriteLine($"State change call initiated from {method} {filePath}: {lineNumber}. work body: {work.Body}");
        Console.WriteLine("State: " + JsonSerializer.Serialize(this, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, IgnoreReadOnlyFields = false }));

        await StateHasChanged();
    }
#else
    public async Task Dispatch(Action work)

        {
        try
        {
            work();
        }
        catch (Exception ex)
        {
            ShowError("Unhandled Exception", ex.Message);
        }
        await StateHasChanged();
    }
#endif

    private async Task StateHasChanged()
    {
        if (OnChange != null)
        {
            await OnChange();
        }
    }

    #endregion
}
