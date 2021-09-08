using Microsoft.AspNetCore.Components;

namespace BlazeBin.Client.Pages;

public partial class Index : IDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }

    private string? _uploadName;
    [Parameter]
    public string? UploadName
    {
        get
        {
            return _uploadName;
        }
        set
        {
            _uploadName = value;
            _ = ProcessUploadNameChange();
        }
    }
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
            _activeIndex = int.Parse(value ?? "0");
            _ = ProcessUploadNameChange();
        }
    }

    protected override void OnInitialized()
    {
        State!.OnChange += HandleStateChange;
    }

    private async Task ProcessUploadNameChange()
    {
        if (_uploadName != null)
        {
            var existing = State!.Uploads?.FindIndex(a => a.LastServerId == _uploadName);
            if (existing.HasValue && existing.Value != -1)
            {
                await State!.Dispatch(() => State!.SelectUpload(existing.Value));
            }
            else
            {
                await State!.Dispatch(() => State!.ReadUpload(_uploadName));
            }
        }
        else if ((State!.Uploads?.Count ?? 0) > 0)
        {
            await State!.Dispatch(() => State!.SelectUpload(0));
        }
        else if ((State!.Uploads?.Count ?? 0) == 0)
        {
            await State!.Dispatch(() => State!.CreateUpload(true));
        }
        else
        {
            throw new InvalidOperationException("An initial state wasn't accounted for!");
        }

        var index = Math.Max(State!._activeFileIndex, _activeIndex);

        await State!.Dispatch(() => State!.SetActiveFile(index));
    }

    private Task HandleStateChange()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        State!.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
