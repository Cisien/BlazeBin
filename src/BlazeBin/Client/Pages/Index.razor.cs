using Microsoft.AspNetCore.Components;

namespace BlazeBin.Client.Pages;

public partial class Index : IDisposable
{
    [Inject] private BlazeBinStateContainer? State { get; set; }
    [Inject] private ILogger<Editor>? Logger { get; set; }

    [Parameter] public string? UploadName { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State!.OnChange += HandleStateChange;
        await State!.Dispatch(() => Task.CompletedTask); // force logging the State!

        Logger!.LogInformation("Loading upload {upload}", UploadName);
        if (UploadName != null)
        {
            var existing = State!.Uploads?.FindIndex(a => a.LastServerId == UploadName);
            if (existing.HasValue && existing.Value != -1)
            {
                // upload is set, matches an existing upload     action: load existing
                await State!.Dispatch(() => State!.SelectUpload(existing.Value));

                Logger!.LogInformation("{upload} requested, and we already have a local copy of it.", UploadName);
            }
            else
            {
                // upload is set, existing uploads               action: always load
                // upload is set, no existing uploads            action: always load
                await State!.Dispatch(() => State!.ReadUpload(UploadName));
                Logger!.LogInformation("{upload} requested. Loaded from server.", UploadName);
            }
        }
        else if((State!.Uploads?.Count ?? 0) > 0)
        {
            // upload is not set, existing uploads           action: set first existing upload, first file as active
            await State!.Dispatch(() => State!.SelectUpload(0));
            Logger!.LogInformation("The first upload was set active");
        }
        else if ((State!.Uploads?.Count ?? 0) == 0)
        {
            // upload is not set, no uploads exist           action: create empty upload, set active
            await State!.Dispatch(() => State!.CreateUpload(true));

            Logger!.LogInformation("An upload was created, and set as active");
        }
        else
        {
            Logger!.LogCritical("An initial state wasn't accounted for!");
        }

        Logger!.LogInformation("State object is good to go!");

        await State!.Dispatch(() => Task.CompletedTask); // force logging the State!
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
