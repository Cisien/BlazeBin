using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazeBin.Client.Pages;

public partial class PasteList : IDisposable
{
    [Inject] private BlazeBinStateContainer State { get; set; } = null!;

    protected override void OnInitialized()
    {
        State.OnChange += HandleStateChange;
    }

    private Task HandleStateChange()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task SelectUpload(MouseEventArgs e, int index)
    {
        if (e.Detail > 1)
        {
            return;
        }

        await State.Dispatch(() => State.SelectUpload(index));
    }

    private async Task RemoveUpload(MouseEventArgs e, string id)
    {
        if (e.Detail > 1)
        {
            return;
        }

        // prompt that upload is not posted, confirm
        await State.Dispatch(() => State.DeleteUpload(id));
    }

    private async Task ToggleFavorite(MouseEventArgs e, string favorite)
    {
        if (e.Detail > 1)
        {
            return;
        }

        if(State.Favorites.Any(a => a == favorite))
        {
            await State.Dispatch(() => State.DeleteFavorite(favorite));
        }
        else
        {
            await State.Dispatch(() => State.CreateFavorite(favorite));
        }
    }

    private async Task RemoveHistory(MouseEventArgs e, string history)
    {

        if (e.Detail > 1)
        {
            return;
        }

        await State.Dispatch(() => State.DeleteHistory(history));
    }

    private async Task SelectNonFileBundle(MouseEventArgs e, string serverId)
    {
        if (e.Detail > 1)
        {
            return;
        }

        await State.Dispatch(() => State.ReadUpload(serverId));
    }

    public void Dispose()
    {
        State.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
