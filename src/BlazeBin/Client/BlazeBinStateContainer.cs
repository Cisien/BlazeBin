using BlazeBin.Client.Services;
using BlazeBin.Shared;
using BlazeBin.Shared.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

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

    public FileBundle? AdHocBundle { get; private set; }
    public int ActiveUploadIndex { get; private set; } = -1;
    public int ActiveFileIndex { get; private set; } = -1;

    public FileData? ActiveFile
    {
        get
        {
            var bundle = ActiveUpload;

            if (bundle == null)
            {
                return null;
            }

            if (ActiveFileIndex >= 0 && ActiveFileIndex < bundle.Files.Count)
            {
                return bundle.Files[ActiveFileIndex];
            }
            return null;
        }
    }

    public FileBundle? ActiveUpload
    {
        get
        {
            if (AdHocBundle != null)
            {
                return AdHocBundle;
            }

            var isOor = ActiveUploadIndex < 0 || ActiveUploadIndex > Uploads.Count - 1;
            if (isOor)
            {
                return null;
            }

            return Uploads[ActiveUploadIndex];
        }
    }

    public Error? Error { get; private set; }

    [MemberNotNullWhen(true, nameof(Error))]
    public bool DisplayError { get; private set; }

    public bool IsServerSideRender { get; set; }

    public event Func<Task>? OnChange;

    public BlazeBinStateContainer(ILogger<BlazeBinStateContainer> logger, IUploadService uploadSvc, IKeyGeneratorService keygen, IClientStorageService storage, NavigationManager nav, BlazeBinConfiguration? appConfig = null)
    {
        _uploadSvc = uploadSvc;
        _keygen = keygen;
        _storage = storage;
        _logger = logger;
        _nav = nav;
        History = new();
        Favorites = new();
        Uploads = new();

        if(appConfig != null)
        {
            ClientConfig = appConfig.Client;
        }
    }

    public bool StoreAntiforgeryToken(string? token)
    {
        _uploadSvc.SetAntiforgeryToken(token);
        return false;
    }

    public bool SetClientConfig(BlazeBinClient clientConfig)
    {
        ClientConfig = clientConfig;
        return true;
    }

    public async Task<bool> InitializeUploadLists()
    {
        Uploads = await _storage.Get<FileBundle>(UploadListKey);

        if (Uploads.Count < 0)
        {
            SelectUpload(0);
        }

        Favorites = await _storage.Get<string>(FavoritesListKey);
        History = await _storage.Get<string>(HistoryListKey);

        return true;
    }

    #region uploads
    public async Task<bool> CreateUpload(bool setActive)
    {
        var upload = FileBundle.New(_keygen.GenerateKey(GeneratedIdLength).ToString(), _keygen.GenerateKey(GeneratedIdLength).ToString());
        await InsertUpload(upload, setActive);
        return true;
    }

    public async Task<bool> InsertUpload(FileBundle upload, bool setActive)
    {
        var existingIndex = Uploads.FindIndex(a => a.Id == upload.Id);

        if (existingIndex != -1)
        {
            var newid = _keygen.GenerateKey(GeneratedIdLength).ToString();
            upload = upload with { Id = newid, LastServerId = null };
        }

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
        return true;
    }

    public async Task<bool> ReadUpload(string serverId)
    {
        var fromApi = await _uploadSvc.Get(serverId);
        if (!fromApi.Successful)
        {
            return ShowError($"Unable to load {serverId}", fromApi.Error);
        }

        SelectUpload(-1);
        AdHocBundle = fromApi.Value;

        SetActiveFile(0);

        if (!Uploads.Any(a => a.Id == ActiveUpload?.Id))
        {
            await CreateHistory(serverId);
        }
        return true;
    }

    public async Task<bool> DeleteUpload(string id)
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
            AdHocBundle = null;
            bool changed = SetActiveFile(-1);
            changed = changed || SelectUpload(-1);
            return changed;
        }

        if (uploadIndex < 0)
        {
            uploadIndex = 0;
        }

        if (uploadIndex > Uploads.Count - 1)
        {
            uploadIndex = Uploads.Count - 1;
        }

        return SelectUpload(uploadIndex);
    }

    public bool SelectUpload(int index)
    {
        if (ActiveUploadIndex == index)
        {
            return false;
        }

        ActiveUploadIndex = index;
        if (index == -1)
        {
            SetActiveFile(-1);
            return true;
        }

        AdHocBundle = null;
        if (ActiveUpload!.Files.Count > 0)
        {
            SetActiveFile(0);
        }
        return true;
    }

    public async Task<bool> SaveActiveUpload()
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
            return false;
        }

        if (ActiveUpload.Files.SelectMany(a => a.Data).Count() > 390_000)
        {
            return ShowError("File length limit exceeded", "The number characters contained in the files in this set is larger than what the server will accept.\nReduce the size of the files or split this into multiple sets.");
        }

        var result = await _uploadSvc.Set(ActiveUpload);

        if (!result.Successful)
        {
            return ShowError($"Failed to save {ActiveUpload.Id}", result.Error);
        }

        ActiveUpload.LastServerId = result.Value;

        if (!Uploads.Any(a => a.Id == ActiveUpload.Id))
        {
            await InsertUpload(ActiveUpload, true);
            if (AdHocBundle != null)
            {
                ActiveFileIndex = -1;
                AdHocBundle = null;
            }
        }

        await _storage.Set(UploadListKey, Uploads);
        return true;
    }

    public async Task<bool> SetActiveUploadDirty()
    {
        if (ActiveUpload == null)
        {
            throw new InvalidOperationException("Attempting to set dirty state on an active upload that doesn't exist");
        }

        if (ActiveUpload.LastServerId == null)
        {
            return false;
        }

        if (ActiveUpload == AdHocBundle)
        {
            await PromoteAdHocBundle();
        }

        ActiveUpload.LastServerId = null;
        return true;
    }
    #endregion

    #region files
    public async Task<bool> CreateFile(string filename, bool setActive)
    {
        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        if (ActiveUpload.Files.Any(a => a.Filename == filename))
        {
            return ShowError("Name Conflict", $"The current set already contains file with the name of {filename}");
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

        return true;
    }

    public async Task<bool> UpdateFile(string id, string contents) // maybe do filename also later?
    {
        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        var file = ActiveUpload.Files.SingleOrDefault(a => a.Id == id);
        if (file == null)
        {
            file = AdHocBundle?.Files.SingleOrDefault(a => a.Id == id);
        }

        if (file == null)
        {
            return false;
        }

        if (file.Data == contents)
        {
            return false;
        }

        await PromoteAdHocBundle();

        var index = ActiveUpload.Files.IndexOf(file);
        ActiveUpload.Files[index] = file with { Data = contents };
        ActiveUpload.LastServerId = null;

        await _storage.Set(UploadListKey, Uploads);

        return true;
    }

    public async Task<bool> DeleteFile(string id)
    {
        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        var fileIndex = ActiveUpload.Files.FindIndex(a => a.Id == id);
        if (fileIndex == -1)
        {
            return false;
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
        return true;
    }

    public bool SetActiveFile(int index)
    {
        if (ActiveFileIndex == index)
        {
            return false;
        }

        if (index == -1)
        {
            ActiveFileIndex = index;
            return true;
        }

        if (ActiveUpload == null)
        {
            throw new ArgumentException(nameof(ActiveUpload));
        }

        var isOutOfRange = index >= ActiveUpload.Files.Count;
        if (isOutOfRange)
        {
            return false;
        }

        ActiveFileIndex = index;
        return true;
    }

    private async Task<bool> PromoteAdHocBundle()
    {
        if (AdHocBundle == null)
        {
            return false;
        }

        var newid = _keygen.GenerateKey(GeneratedIdLength).ToString();
        var newBundle = AdHocBundle with { LastServerId = null, Id = newid };
        await InsertUpload(newBundle, true);
        AdHocBundle = null;
        return true;
    }
    #endregion

    #region history
    public async Task<bool> CreateHistory(string serverId)
    {
        if (History.Any(a => a == serverId))
        {
            return false;
        }

        History.Insert(0, serverId);
        if (History.Count >= 10)
        {
            History.RemoveRange(10, History.Count - 10);
        }

        await _storage.Set(HistoryListKey, History);
        return true;
    }

    public async Task<bool> DeleteHistory(string serverId)
    {
        var historyItem = History.SingleOrDefault(a => a == serverId);
        if (historyItem == null)
        {
            return false;
        }

        History.Remove(historyItem);
        await _storage.Set(HistoryListKey, History);
        return true;
    }

    #endregion

    #region favorites
    public async Task<bool> CreateFavorite(string serverId)
    {
        if (Favorites.Any(a => a == serverId))
        {
            return false;
        }

        Favorites.Insert(0, serverId);
        if (Favorites.Count >= 10)
        {
            Favorites.RemoveRange(10, Favorites.Count - 10);
        }

        await _storage.Set(FavoritesListKey, Favorites);
        return true;
    }

    public async Task<bool> DeleteFavorite(string serverId)
    {
        var favoriteItem = Favorites.SingleOrDefault(a => a == serverId);
        if (favoriteItem == null)
        {
            return false;
        }

        Favorites.Remove(favoriteItem);
        await _storage.Set(FavoritesListKey, Favorites);
        return true;
    }
    #endregion

    #region ErrorDialog

    public bool ResetMessage()
    {
        Error = null;
        DisplayError = false;
        return true;
    }

    public bool ShowError(string title, string message)
    {
        Error = new Error(title, message);
        DisplayError = true;

        return true;
    }
    #endregion

    #region Dispatch
#if DEBUG
    public async Task Dispatch(Expression<Func<Task<bool>>> work, bool doNavUpdate = true, [CallerMemberName] string method = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = -1)
    {
        bool stateChanged = false;
        try
        {
            stateChanged = await work.Compile()();
        }
        catch (Exception ex)
        {
            stateChanged = stateChanged || ShowError("Unhandled Exception", ex.ToString());
        }

        DoDebugLogging(work, doNavUpdate, method, filePath, lineNumber, stateChanged);
        if (stateChanged)
        {
            await StateHasChanged(doNavUpdate);
        }
    }

    public async Task Dispatch(Expression<Func<bool>> work, bool doNavUpdate = true, [CallerMemberName] string method = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = -1)
    {
        bool stateChanged = false;
        try
        {
            stateChanged = work.Compile()();
        }
        catch (Exception ex)
        {
            stateChanged = stateChanged || ShowError("Unhandled Exception", ex.ToString());
        }
        DoDebugLogging(work, doNavUpdate, method, filePath, lineNumber, stateChanged);

        if (stateChanged)
        {
            await StateHasChanged(doNavUpdate);
        }
    }

    private void DoDebugLogging<T>(Expression<T> work, bool doNavUpdate, string method, string filePath, int lineNumber, bool stateChanged)
    {
        var d = new Dictionary<string, object?>();
        var dispatchedName = "[null]";
        if (work.Body is MethodCallExpression methodBody)
        {
            dispatchedName = methodBody.Method.Name;
            var parameters = methodBody.Method.GetParameters().Select(a => a.Name!).ToList() ?? new List<string>();
            var argTypes = methodBody.Arguments.Select(a => a.NodeType);
            var values = methodBody.Arguments.Select(a =>
            {
                if (a is ConstantExpression c)
                {
                    return c.Value;
                }
                else
                {
                    return Expression.Lambda(a).Compile().DynamicInvoke();
                }
            }).ToList() ?? new List<object?>();

            for (var i = 0; i < parameters.Count; i++)
            {
                d.Add(parameters[i], values[i]);
            }
        }
        else if (work.Body is MemberExpression memberBody)
        {
            dispatchedName = "() => ";
            var value = Expression.Lambda(memberBody).Compile().DynamicInvoke();
            var name = memberBody.Member.Name;
            d.Add(name, value);
        }
        else if (work.Body is ConstantExpression constBody)
        {
            dispatchedName = "() => ";
            var value = constBody.Value;
            d.Add($"", value);
        }

        _logger.LogInformation("State change: {stateChanged} with nav update: {doNavUpdate}; from {method} {filePath}: {lineNumber}. {dispatchedMethod}({params})", stateChanged, doNavUpdate, method, filePath, lineNumber, dispatchedName, string.Join(", ", d.Select(a => $"{a.Key} = {a.Value ?? "null"}")));
        if (Uploads.SelectMany(a => a.Files).SelectMany(a => a.Data).Count() + (AdHocBundle?.Files.SelectMany(a => a.Data).Count() ?? 0) > 2000)
        {
            _logger.LogInformation("State: <too large to serialize>");
        }
        else
        {
            _logger.LogInformation("State: {state}", JsonSerializer.Serialize(this, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, IgnoreReadOnlyFields = false }));
        }
    }
#else
    public async Task Dispatch(Func<Task<bool>> work, bool doNavUpdate = true)
    {
        bool stateChanged = false;
        try
        {
            stateChanged = await work();
        }
        catch (Exception ex)
        {
            stateChanged = stateChanged || ShowError("Unhandled Exception", ex.Message);
        }

        if(stateChanged) 
        {
            await StateHasChanged(doNavUpdate);
        }
    }
    public async Task Dispatch(Func<bool> work, bool doNavUpdate = true)
    {
        bool stateChanged = false;
        try
        {
            stateChanged = work();
        }
        catch (Exception ex)
        {
            stateChanged = stateChanged || ShowError("Unhandled Exception", ex.Message);
        }

        if(stateChanged) 
        {
            await StateHasChanged(doNavUpdate);
        }
    }
#endif

    private async Task StateHasChanged(bool doNavUpdate = false)
    {
        if (!IsServerSideRender && doNavUpdate)
        {
            var currentUri = new Uri(_nav.Uri);
            if (ActiveUpload == null)
            {
                _nav.NavigateTo($"/", false);
            }
            else if (ActiveUpload.LastServerId == null && currentUri.Segments.Length != 1)
            {
                _nav.NavigateTo($"/", false);
            }
            else if (ActiveUpload.LastServerId == null && currentUri.Segments.Length == 1)
            {
                // url is already /, noop
            }
            else if (ActiveUpload.LastServerId != null)
            {
                if (currentUri.Segments.Length == 1)
                {
                    _nav.NavigateTo($"/{ActiveUpload!.LastServerId}/{ActiveFileIndex}", false);
                }
                else if (currentUri.Segments.Length == 2)
                {
                    _nav.NavigateTo($"/{ActiveUpload!.LastServerId}/0", false);
                }
                else if (currentUri.Segments.Length == 3 && (currentUri.Segments[1] != ActiveUpload?.LastServerId
                       || currentUri.Segments[2] != ActiveFileIndex.ToString()))
                {
                    _nav.NavigateTo($"/{ActiveUpload!.LastServerId}/{ActiveFileIndex}", false);
                }
                else
                {
                    throw new InvalidOperationException("Unexpected state");
                }
            }
            else
            {
                throw new InvalidOperationException("Unexpected state");
            }
        }

        if (OnChange != null)
        {
            await OnChange();
        }
    }

    #endregion
}
