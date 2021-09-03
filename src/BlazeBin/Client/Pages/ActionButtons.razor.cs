using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazeBin.Client.Pages;

public partial class ActionButtons : IDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }
    private bool _saving;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            State!.OnChange += HandleStateChange;
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
        _saving = true;
        StateHasChanged();
        await State!.Dispatch(() => State!.SaveActiveUpload());
        _saving = false;
    }

    private bool IsSavingDisabled()
    {
        if(_saving)
        {
            return true;
        }
        
        if(State!.ActiveUpload != null && State.ActiveUpload.LastServerId != null)
        {
            return true;
        }

        if(State!.ActiveFile == null)
        {
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        State!.OnChange -= HandleStateChange;
        GC.SuppressFinalize(this);
    }
}
