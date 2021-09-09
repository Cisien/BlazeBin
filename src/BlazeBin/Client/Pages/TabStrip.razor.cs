
using BlazeBin.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazeBin.Client.Pages;

public partial class TabStrip : IDisposable
{
    [Inject] private BlazeBinStateContainer State { get; set; } = null!;

    private string? _newFilename;

    protected override void OnInitialized()
    {
        State.OnChange += HandleStateChange;
    }

    private Task HandleStateChange()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task TabClicked(MouseEventArgs e, int index)
    {
        if (e.Detail > 1)
        {
            return;
        }
        _ = State.ActiveUpload ?? throw new ArgumentException("Attempt to change set active file on an upload that doesn't exist");

        await State.Dispatch(() => State.SetActiveFile(index));
    }

    private async Task CloseTab(MouseEventArgs e, FileData data)
    {
        if (e.Detail > 1)
        {
            return;
        }

        await State.Dispatch(() => State.DeleteFile(data.Id));
    }

    private void CreateTab(MouseEventArgs e)
    {
        if (e.Detail > 1)
        {
            return;
        }

        _newFilename = "";
    }

    private async Task CreateNewFile(KeyboardEventArgs e)
    {
        if (e.Code != "Enter" && e.Code != "NumpadEnter")
        {
            return;
        }

        if (_newFilename == null)
        {
            return;
        }

        await State.Dispatch(() => State.CreateFile(_newFilename, true));
        _newFilename = null;
    }


    public void Dispose()
    {
        State.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
