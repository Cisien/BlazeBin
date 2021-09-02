using Microsoft.AspNetCore.Components;

namespace BlazeBin.Client.Pages;

public partial class ErrorDialog: IDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }

    protected override void OnInitialized()
    {
        State!.OnChange += HandleStateChange;
    }

    private async Task CloseDialog()
    {
        await State!.Dispatch(() => State!.ResetMessage());
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
