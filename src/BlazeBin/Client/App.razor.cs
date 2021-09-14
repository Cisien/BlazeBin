using BlazeBin.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace BlazeBin.Client;
public partial class App : IDisposable
{
    [Inject] public BlazeBinStateContainer State { get; set; } = null!;
    [Inject] public PersistentComponentState AppState { get; set; } = null!;
    [Inject] public NavigationManager Nav { get; set; } = null!;
    [Inject] public ILogger<App> Logger { get; set; } = null!;

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await State.InitializeUploadLists();

        if (!State.IsServerSideRender)
        {
            Nav.LocationChanged += LocationChanged;

            if (AppState.TryTakeFromJson<Error>("server-side-error", out var serverError) && serverError != null)
            {
                State.ShowError(serverError.Title, serverError.Text);
            }

            if (AppState.TryTakeFromJson<string>("af-token", out var afToken))
            {
                State.StoreAntiforgeryToken(afToken);
            }
            else
            {
                throw new InvalidOperationException("Unable to get af-token from server state");
            }

            if (AppState.TryTakeFromJson<BlazeBinClient>("client-config", out var clientConfig) && clientConfig != null)
            {
                State.SetClientConfig(clientConfig);
            }
            else
            {
                throw new InvalidOperationException("Unable to get client-config from server state");
            }
        }
        else
        {
            AppState.RegisterOnPersisting(PersistingServerState);
        }
        await base.SetParametersAsync(parameters);
    }

    private async void LocationChanged(object? sender, LocationChangedEventArgs e)
    {
        Console.WriteLine("Location Changed");
        Logger.LogInformation("nav link changed to {nav}; wasIntercepted: {intercepted}", e.Location, e.IsNavigationIntercepted);
        if (State == null)
        {
            return;
        }
        await State.Dispatch(() => true, false);
    }

    public Task PersistingServerState()
    {
        AppState.PersistAsJson("server-side-error", State.Error);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Nav.LocationChanged -= LocationChanged;

        GC.SuppressFinalize(this);
    }
}
