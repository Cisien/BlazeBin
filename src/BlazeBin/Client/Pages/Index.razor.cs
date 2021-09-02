using Microsoft.AspNetCore.Components;

namespace BlazeBin.Client.Pages;

public partial class Index : IDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }
    [Parameter] public string? UploadName { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State!.OnChange += HandleStateChange;
        if (UploadName != null)
        {
            var existing = State!.Uploads?.FindIndex(a => a.LastServerId == UploadName);
            if (existing.HasValue && existing.Value != -1)
            {
                await State!.Dispatch(() => State!.SelectUpload(existing.Value));
            }
            else
            {
                await State!.Dispatch(() => State!.ReadUpload(UploadName));
            }
        }
        else if((State!.Uploads?.Count ?? 0) > 0)
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
