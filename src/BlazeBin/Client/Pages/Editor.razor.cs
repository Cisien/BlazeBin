using BlazeBin.Shared;
using BlazeBin.Shared.Extensions;
using BlazorMonaco;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazeBin.Client.Pages;

public partial class Editor : IAsyncDisposable
{
    [Inject] private BlazeBinStateContainer State { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private const string ModelUriFormat = "https://bin.mod.gg/{0}/{1}";
    private MonacoEditor? _editor;
    private bool _hasMarkedDirty = false;

    public async Task EditorInitialized(MonacoEditorBase editor)
    {
        State.OnChange += HandleStateChange;
        var windowWidth = await JS.InvokeAsync<int>("blazebin.getWindowWidth");
        var isNarrow = windowWidth < 780;
        await _editor!.UpdateOptions(new GlobalEditorOptions
        {
            Minimap = new EditorMinimapOptions { Enabled = !isNarrow },
            LineNumbers = isNarrow ? "off" : "on"
        });
        
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
        if(changed.Changes.Count == 0)
        {
            return;
        }

        if (!_hasMarkedDirty)
        {
            await State.Dispatch(() => State.SetActiveUploadDirty());

            _hasMarkedDirty = true;
        }
    }

    // save and raise events to notify everything else (for saving, etc)
    private async Task ModelChanged(ModelChangedEvent changed)
    {
        if (State.ActiveUpload == null)
        {
            return;
        }

        var model = await MonacoEditorBase.GetModel(changed.OldModelUri);
        if (model == null || await model.IsDisposed())
        {
            return;
        }
        var (bundleId, fileId) = GetIdsFromModelUri(model.Uri);

        if (State.ActiveUpload.Id != bundleId)
        {
            return;
        }

        var data = await model.GetValue(EndOfLinePreference.LF, false);

        await State.UpdateFile(fileId, data);
        _hasMarkedDirty = false;
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

        var models = await MonacoEditorBase.GetModels();
        // dispose models that aren't used anymore
        foreach (var model in models)
        {
            if (!DataExistsForModel(model) && !await model.IsDisposed())
            {
                try
                {
                    await model.DisposeModel();
                }
                catch (Exception)
                {
                    // sometimes this throws an "Unhandled exception rendering component: Cannot read properties of null (reading 'dispose')" exception in jsland,
                    // even though everything is good on the C# side
                }
            }
        }

        if (State.ActiveUpload == null)
        {
            var defuncTModel = await _editor.GetModel();
            if (defuncTModel != null)
            {
                await defuncTModel.DisposeModel();
            }
            return;
        }

        // create models that don't exist yet
        foreach (var file in State.ActiveUpload.Files)
        {
            var modelUri = GetUriForFile(State.ActiveUpload, file);
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
            if(existingModel == null) // still
            {
                // couldn't create the model (likely due to a race condition when swapping models too quickly)
                continue;
            }

            if (file.Id == State.ActiveFile?.Id)
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
        if (State.ActiveUpload == null)
        {
            return false;
        }

        if (State.ActiveFile == null)
        {
            return false;
        }

        var (bundleId, fileId) = GetIdsFromModelUri(model.Uri);
        if (State.ActiveUpload.Id != bundleId)
        {
            return false;
        }

        var existing = State.ActiveUpload.Files.SingleOrDefault(a => a.Id == fileId);
        if (existing == null)
        {
            return false;
        }
        return true;
    }

    private async Task<TextModel> EnsureModelCreated(FileData file)
    {
        if (State.ActiveUpload == null)
        {
            throw new ArgumentException(nameof(State.ActiveUpload));
        }

        if (State.ActiveFile == null)
        {
            throw new ArgumentException(nameof(State.ActiveFile));
        }

        var modelUri = GetUriForFile(State.ActiveUpload, file);
        var model = await MonacoEditorBase.GetModel(modelUri);

        if (model == null || await model.IsDisposed())
        {
            try
            {
                model = await MonacoEditorBase.CreateModel(file.Data, file.GetLanguage(), modelUri);
            }
            catch
            {
                // another unavoidable race condition where jsland throws an exception even though we validated it in c#
                // Unhandled exception rendering component: ModelService: Cannot add model because it already exists!
            }
        }
        return model!;
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
            foreach (var model in await MonacoEditorBase.GetModels())
            {
                await model.DisposeModel();
            }

            await _editor.DisposeEditor();
        }
        State.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
