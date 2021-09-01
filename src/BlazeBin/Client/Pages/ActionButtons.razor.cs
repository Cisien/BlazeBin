using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazeBin.Client.Pages;

public partial class ActionButtons : IDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }

    protected override void OnAfterRender(bool firstRender)
    {
        State!.OnChange += HandleStateChange;
        if (firstRender)
        {
            StateHasChanged();
        }
        base.OnAfterRender(firstRender);
    }

    private Task HandleStateChange()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task CreateNewBundle(MouseEventArgs e)
    {
        await State!.Dispatch(() => State!.CreateUpload(true));
    }

    private async Task SaveFileBundle(MouseEventArgs e)
    {
        await State!.Dispatch(() => State!.SaveActiveUpload());
    }

    public void Dispose()
    {
        State!.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
