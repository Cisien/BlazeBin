using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazeBin.Client.Pages;

public partial class PasteList : IDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }
    [Inject] private ILogger<Editor>? Logger { get; set; }

    protected override void OnInitialized()
    {
        State!.OnChange += HandleStateChange;
    }

    private Task HandleStateChange()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task SelectUpload(MouseEventArgs e, string id)
    {
        if (e.Detail > 1)
        {
            return;
        }
        _ = State!.Uploads ?? throw new ArgumentException(nameof(State.Uploads));
        var index = State!.Uploads.FindIndex(a => a.Id == id);
        await State!.Dispatch(() => State!.SelectUpload(index));
    }

    private async Task RemoveUpload(MouseEventArgs e, string id)
    {
        if (e.Detail > 1)
        {
            return;
        }

        // prompt that upload is not posted, confirm
        await State!.Dispatch(() => State!.DeleteUpload(id));
    }

    private async Task RemoveFavorite(MouseEventArgs e, string favorite)
    {
        if (e.Detail > 1)
        {
            return;
        }

        await State!.Dispatch(() => State!.DeleteFavorite(favorite));
    }

    private async Task RemoveHistory(MouseEventArgs e, string history)
    {

        if (e.Detail > 1)
        {
            return;
        }

        await State!.Dispatch(() => State!.DeleteHistory(history));
    }

    private async Task SelectNonFileBundle(MouseEventArgs e, string serverId)
    {

        if (e.Detail > 1)
        {
            return;
        }

        await State!.Dispatch(() => State!.ReadUpload(serverId));
    }

    public void Dispose()
    {
        State!.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
