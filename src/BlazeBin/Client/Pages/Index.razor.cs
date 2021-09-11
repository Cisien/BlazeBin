using Microsoft.AspNetCore.Components;

namespace BlazeBin.Client.Pages;

public partial class Index : IDisposable
{
    private BlazeBinStateContainer? _state;
    [Inject]
    private BlazeBinStateContainer State
    {
        get { return _state!; }
        set
        {
            if (_state != null)
            {
                _state.OnChange -= HandleStateChange;
            }
            _state = value;
            _state.OnChange += HandleStateChange;
        }
    }

    [Parameter]
    public string? UploadName { get; set; }

    private int _activeIndex;
    [Parameter]
    public string? ActiveIndex
    {
        get
        {
            return _activeIndex.ToString();
        }
        set
        {
            var val = int.Parse(value ?? "-1");
            _activeIndex = val;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await ProcessUploadNameChange();
    }

    private async Task ProcessUploadNameChange()
    {
        var initialStateChanged = false;
        if (UploadName != null)
        {
            var existing = State.Uploads?.FindIndex(a => a.LastServerId == UploadName);
            if (existing.HasValue && existing.Value != -1)
            {
                initialStateChanged = initialStateChanged || State.SelectUpload(existing.Value);
            }
            else
            {
                initialStateChanged = initialStateChanged || await State.ReadUpload(UploadName);
            }
        }
        else if ((State.Uploads?.Count ?? 0) > 0)
        {
            initialStateChanged = initialStateChanged || State.SelectUpload(0);
        }
        else if ((State.Uploads?.Count ?? 0) == 0)
        {
            initialStateChanged = initialStateChanged || await State.CreateUpload(true);
        }
        else
        {
            throw new InvalidOperationException("An initial state wasn't accounted for!");
        }

        if (State.ActiveFileIndex != _activeIndex)
        {
            var index = Math.Max(State.ActiveFileIndex, _activeIndex);
            initialStateChanged = initialStateChanged || State.SetActiveFile(index);
        }

        // this is likely the first state change triggered in the rendering pipeline.
        await State.Dispatch(() => initialStateChanged);
    }

    private Task HandleStateChange()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        State.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
