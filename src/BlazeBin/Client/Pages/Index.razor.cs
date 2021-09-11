using Microsoft.AspNetCore.Components;

namespace BlazeBin.Client.Pages;

public partial class Index : IDisposable
{
    [Inject] private BlazeBinStateContainer State { get; set; } = null!;

    [Parameter] public string? UploadName { get; set; }
    [Parameter] public string? ActiveIndex { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Console.WriteLine("OnInit");
        State.OnChange += HandleStateChange;
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

        _ = int.TryParse(ActiveIndex, out var index);
        if (ActiveIndex != null && State.ActiveFileIndex != index)
        {
            index = Math.Max(State.ActiveFileIndex, index);
            initialStateChanged = initialStateChanged || State.SetActiveFile(index);
        }

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
