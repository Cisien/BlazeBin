using BlazeBin.Shared;
using BlazeBin.Shared.Extensions;
using BlazorMonaco;
using Microsoft.AspNetCore.Components;

namespace BlazeBin.Client.Pages;

public partial class Editor : IAsyncDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }

    private const string ModelUriFormat = "https://bin.mod.gg/{0}/{1}";
    private MonacoEditor? _editor;

    public async Task EditorInitialized(MonacoEditorBase editor)
    {
        State!.OnChange += HandleStateChange;

        await HandleStateChange();
    }

    private async Task HandleStateChange()
    {
        if (_editor != null)
        {
            await BindState();
            return;
        }
        StateHasChanged();
    }

    private async Task ModelContentChanged(ModelContentChangedEvent changed)
    {
        _ = _editor ?? throw new ArgumentException(nameof(_editor));

        if (State!.ActiveUpload == null)
        {
            await State!.Dispatch(() => State!.CreateUpload(true));
        }

        await State!.Dispatch(() => State!.SetActiveUploadDirty());
    }

    // save and raise events to notify everything else (for saving, etc)
    private async Task ModelChanged(ModelChangedEvent changed)
    {
        if (State!.ActiveUpload == null)
        {
            return;
        }

        var model = await MonacoEditor.GetModel(changed.OldModelUri);
        if (model == null || await model.IsDisposed())
        {
            return;
        }
        var (bundleId, fileId) = GetIdsFromModelUri(model.Uri);

        if (State!.ActiveUpload.Id != bundleId)
        {
            return;
        }

        var data = await model.GetValue(EndOfLinePreference.LF, false);

        await State!.Dispatch(() => State!.UpdateFile(fileId, data));
    }

    private async Task EditorTextBlur(MonacoEditor editor)
    {
        var model = await editor.GetModel();
        await ModelChanged(new ModelChangedEvent { OldModelUri = model.Uri });
    }

    private StandaloneEditorConstructionOptions EditorConstructionOptions(MonacoEditor editor)
    {
        _editor ??= editor;
        var options = new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Theme = "vs-dark"
        };

        return options;
    }

    // syncs the monaco editor models with the files in the currently loaded bundle
    private async Task BindState()
    {
        _ = _editor ?? throw new ArgumentException(nameof(_editor));

        var models = await MonacoEditor.GetModels();

        // dispose models that aren't used anymore
        foreach (var model in models)
        {
            if (!DataExistsForModel(model))
            {
                await model.DisposeModel();
            }
        }

        if (State!.ActiveUpload == null)
        {
            if (_editor == null)
            {
                throw new ArgumentException(nameof(_editor));
            }

            var defuncTModel = await _editor.GetModel();
            if (defuncTModel != null)
            {
                await defuncTModel.DisposeModel();
            }
            return;
        }

        // create models that don't exist yet
        foreach (var file in State!.ActiveUpload.Files)
        {
            var modelUri = GetUriForFile(State!.ActiveUpload, file);
            TextModel? existingModel = null;
            foreach (var model in models)
            {
                var isModelDisposed = false;
                try
                {
                    isModelDisposed = await model.IsDisposed();
                }
                catch (Exception)
                {
                    isModelDisposed = true;
                }

                if (!isModelDisposed && model.Uri == modelUri)
                {
                    existingModel = model;
                    break;
                }
            }

            if (existingModel == null)
            {
                existingModel = await EnsureModelCreated(file);
            }

            if (file.Id == State!.ActiveFile?.Id)
            {
                var editorModel = await _editor.GetModel();
                if (editorModel?.Uri == existingModel.Uri)
                {
                    continue;
                }
                await _editor.SetModel(existingModel);
            }
        }
    }

    private bool DataExistsForModel(TextModel model)
    {
        if (State!.ActiveUpload == null)
        {
            return false;
        }

        if (State!.ActiveFile == null)
        {
            return false;
        }

        var (bundleId, fileId) = GetIdsFromModelUri(model.Uri);
        if (State!.ActiveUpload.Id != bundleId)
        {
            return false;
        }

        var existing = State!.ActiveUpload.Files.SingleOrDefault(a => a.Id == fileId);
        if (existing == null)
        {
            return false;
        }
        return true;
    }

    private async Task<TextModel> EnsureModelCreated(FileData file)
    {
        if (State!.ActiveUpload == null)
        {
            throw new ArgumentException(nameof(State.ActiveUpload));
        }

        if (State!.ActiveFile == null)
        {
            throw new ArgumentException(nameof(State.ActiveFile));
        }

        var modelUri = GetUriForFile(State!.ActiveUpload, file);
        var model = await MonacoEditorBase.GetModel(modelUri);

        if (model == null || await model.IsDisposed())
        {
            model = await MonacoEditorBase.CreateModel(file.Data, file.GetLanguage(), modelUri);
        }
        return model;
    }

    private static string GetUriForFile(FileBundle bundle, FileData file)
    {
        if (string.IsNullOrWhiteSpace(bundle.Id))
        {
            throw new ArgumentNullException(nameof(bundle), "Attempt to get uri for file bundle with empty id");
        }
        if (string.IsNullOrWhiteSpace(file.Id))
        {
            throw new ArgumentNullException(nameof(bundle), "Attempt to get uri file with empty id");
        }

        return string.Format(ModelUriFormat, bundle.Id, file.Id);
    }

    private static (string bundleId, string fileId) GetIdsFromModelUri(string modelUri)
    {
        var parts = modelUri.Split('/');
        var fileId = parts[^1];
        var bundleId = parts[^2];
        return (bundleId, fileId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_editor != null)
        {
            foreach (var model in await MonacoEditor.GetModels())
            {
                await model.DisposeModel();
            }

            await _editor.DisposeEditor();
        }
        State!.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }

}
