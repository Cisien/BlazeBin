using BlazeBin.Client.Services;
using BlazeBin.Shared;
using BlazeBin.Shared.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BlazeBin.Client;
public class BlazeBinStateContainer
{
    private readonly IUploadService _uploadSvc;
    private readonly IKeyGeneratorService _keygen;
    private readonly IClientStorageService _storage;
    private readonly ILogger<BlazeBinStateContainer> _logger;
    private readonly NavigationManager _nav;

    private const string UploadListKey = "upload-list";
    private const string HistoryListKey = "history-list";
    private const string FavoritesListKey = "favorites-list";

    public const int GeneratedIdLength = 12;

    public List<string> History { get; private set; }
    public List<string> Favorites { get; private set; }
    public List<FileBundle> Uploads { get; private set; }

    public BlazeBinClient ClientConfig { get; private set; } = new();

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

            var isOor = _activeUploadIndex < 0 || _activeUploadIndex > Uploads.Count - 1;
            if (isOor)
            {
                return null;
            }

            return Uploads[_activeUploadIndex];
        }
    }

    public Error? Error { get; private set; }

    [MemberNotNullWhen(true, nameof(Error))]
    public bool DisplayError { get; private set; }

    public event Func<Task>? OnChange;

    public BlazeBinStateContainer(ILogger<BlazeBinStateContainer> logger, IUploadService uploadSvc, IKeyGeneratorService keygen, IClientStorageService storage, NavigationManager nav)
    {
        _uploadSvc = uploadSvc;
        _keygen = keygen;
        _storage = storage;
        _logger = logger;
        _nav = nav;
        History = new();
        Favorites = new();
        Uploads = new();
    }

    public void StoreAntiforgeryToken(string? token)
    {
        _uploadSvc.SetAntiforgeryToken(token);
    }

    public void SetClientConfig(BlazeBinClient clientConfig)
    {
        ClientConfig = clientConfig;
    }

    public async Task InitializeUploadLists()
    {
        Uploads = await _storage.Get<FileBundle>(UploadListKey);

        if (Uploads.Count < 0)
        {
            SelectUpload(0);
        }

        Favorites = await _storage.Get<string>(FavoritesListKey);
        History = await _storage.Get<string>(HistoryListKey);
    }

    #region uploads
    public async Task CreateUpload(bool setActive)
    {
        var upload = FileBundle.New(_keygen.GenerateKey(GeneratedIdLength).ToString(), _keygen.GenerateKey(GeneratedIdLength).ToString());
        await InsertUpload(upload, setActive);
    }

    public async Task InsertUpload(FileBundle upload, bool setActive)
    {
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
            SelectUpload(index);
        }
    }

    [RequiresUnreferencedCode("Requried by System.Text.Json.JsonSerializer")]
    public async Task ReadUpload(string serverId)
    {
        var fromApi = await _uploadSvc.Get(serverId);
        if (!fromApi.Successful)
        {
            ShowError($"Unable to load {serverId}", fromApi.Error);
            return;
        }

        var existingIndex = Uploads.FindIndex(a => a.Id == fromApi.Value.Id);
        if (existingIndex != -1)
        {
            SelectUpload(existingIndex);
        }
        else
        {
            SelectUpload(-1);
            _adHocBundle = fromApi.Value;
        }
        _ = ActiveUpload ?? throw new InvalidOperationException("ActiveUpload returns null after loading an upload");

        SetActiveFile(0);

        if (!Uploads.Any(a => a.Id == ActiveUpload?.Id))
        {
            await CreateHistory(serverId);
        }
    }

    public async Task DeleteUpload(string id)
    {
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
            SelectUpload(-1);
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

        SelectUpload(uploadIndex);
    }

    public void SelectUpload(int index)
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
    }

    public async Task SaveActiveUpload()
    {
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

        if (ActiveUpload.Files.SelectMany(a => a.Data).Count() > 390_000)
        {
            ShowError("File length limit exceeded", "The number characters contained in the files in this set is larger than what the server will accept.\nReduce the size of the files or split this into multiple sets.");
            return;
        }

        var result = await _uploadSvc.Set(ActiveUpload);

        if (!result.Successful)
        {
            ShowError($"Failed to save {ActiveUpload.Id}", result.Error);
            return;
        }

        ActiveUpload.LastServerId = result.Value;

        if (!Uploads.Any(a => a.Id == ActiveUpload.Id))
        {
            await InsertUpload(ActiveUpload, true);
            if (_adHocBundle != null)
            {
                _activeFileIndex = -1;
                _adHocBundle = null;
            }
        }

        await _storage.Set(UploadListKey, Uploads);
    }

    public void SetActiveUploadDirty()
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
    }
    #endregion

    #region files
    public async Task CreateFile(string filename, bool setActive)
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

        await PromoteAdHocBundle();

        var id = _keygen.GenerateKey(GeneratedIdLength).ToString();
        var newFile = FileData.Empty with { Id = id, Filename = filename.ToString() };
        ActiveUpload.Files.Add(newFile);
        ActiveUpload.LastServerId = null;
        if (setActive)
        {
            SetActiveFile(ActiveUpload.Files.IndexOf(newFile));
        }
        await _storage.Set(UploadListKey, Uploads);
    }

    public async Task UpdateFile(string id, string contents) // maybe do filename also later?
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
        await PromoteAdHocBundle();

        var index = ActiveUpload.Files.IndexOf(file);
        ActiveUpload.Files[index] = file with { Data = contents };
        ActiveUpload.LastServerId = null;

        await _storage.Set(UploadListKey, Uploads);
    }

    public async Task DeleteFile(string id)
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
        await PromoteAdHocBundle();

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
        await _storage.Set(UploadListKey, Uploads);
    }

    public void SetActiveFile(int index)
    {
        if (index == -1)
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

        var isOutOfRange = index >= ActiveUpload.Files.Count;
        if (isOutOfRange)
        {
            return;
        }

        if (ActiveFile?.Id == ActiveUpload.Files[index].Id)
        {
            return;
        }

        _activeFileIndex = index;
    }

    private async Task PromoteAdHocBundle()
    {
        if (_adHocBundle == null)
        {
            return;
        }

        await InsertUpload(_adHocBundle, true);
        _adHocBundle = null;
    }
    #endregion

    #region history
    public async Task CreateHistory(string serverId)
    {
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
    public async Task CreateFavorite(string serverId)
    {
        if (Favorites.Any(a => a == serverId))
        {
            return;
        }

        Favorites.Insert(0, serverId);
        if (Favorites.Count >= 10)
        {
            Favorites.RemoveRange(10, Favorites.Count - 10);
        }

        await _storage.Set(FavoritesListKey, Favorites);
    }

    public async Task DeleteFavorite(string serverId)
    {
        var favoriteItem = Favorites.SingleOrDefault(a => a == serverId);
        if (favoriteItem == null)
        {
            return;
        }

        Favorites.Remove(favoriteItem);
        await _storage.Set(FavoritesListKey, Favorites);
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
        _logger.LogInformation("State change call initiated from {method} {filePath}: {lineNumber}. {dispatchedMethod}", method, filePath, lineNumber, work.Body);

        if (Uploads.SelectMany(a => a.Files).SelectMany(a => a.Data).Count() + (_adHocBundle?.Files.SelectMany(a => a.Data).Count() ?? 0) > 2000)
        {
            _logger.LogInformation("State: <too large to serialize>");
        }
        else
        {
            _logger.LogInformation("State: {state}", JsonSerializer.Serialize(this, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, IgnoreReadOnlyFields = false }));
        }

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
        _logger.LogInformation("State change call initiated from {method} {filePath}: {lineNumber}. {dispatchedMethod}", method, filePath, lineNumber, work.Body);

        if (Uploads.SelectMany(a => a.Files).SelectMany(a => a.Data).Count() + (_adHocBundle?.Files.SelectMany(a => a.Data).Count() ?? 0) > 2000)
        {
            _logger.LogInformation("State: <too large to serialize>");
        }
        else
        {
            _logger.LogInformation("State: {state}", JsonSerializer.Serialize(this, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, IgnoreReadOnlyFields = false }));
        }

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
        if (ActiveUpload?.LastServerId != null && !_nav.Uri.EndsWith(ActiveUpload.LastServerId))
        {
            _nav.NavigateTo($"/{ActiveUpload.LastServerId}", false);
        }

        if (OnChange != null)
        {
            await OnChange();
        }
    }

    #endregion
}
