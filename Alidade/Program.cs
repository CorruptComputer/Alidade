using Alidade.Osm.TaggingSchemas;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Alidade;

/// <summary>
///   Application entry point
/// </summary>
public static class Program
{
    /// <summary>
    ///   Everything starts from here.
    /// </summary>
    public static async Task Main(string[] args)
    {
        await WebAssemblyHostBuilder.CreateDefault(args).BuildHost().RunHostAsync();
    }

    private static WebAssemblyHost BuildHost(this WebAssemblyHostBuilder builder)
    {
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.ConfigureContainer(new AutofacServiceProviderFactory(), containerBuilder =>
        {
            containerBuilder.RegisterModule(new AlidadeModule(builder.HostEnvironment.BaseAddress));
        });

        builder.Logging
#if DEBUG
            .SetMinimumLevel(LogLevel.Debug);
#else
            .SetMinimumLevel(LogLevel.Warning);
#endif

        return builder.Build();
    }

    private static async Task RunHostAsync(this WebAssemblyHost host)
    {
        // Eagerly instantiate event-driven services that subscribe to state.
        // Resolving EditBufferService here keeps it alive for the application lifetime
        // and activates its state-change subscriptions.
        host.Services.GetRequiredService<EditBufferService>();

        // Warm up compiled-in static dictionaries and the preset search index.
        _ = TaggingData.Presets.Count;
        _ = TaggingData.Fields.Count;
        _ = TaggingData.DeprecationRules.Count;
        _ = TaggingData.Categories.Count;
        host.Services.GetRequiredService<PresetService>().WarmUp();

        // Build NTS coverage geometries for imagery in the background.
        IMediator mediator = host.Services.GetRequiredService<IMediator>();
        _ = mediator.Publish(new Handlers.Map.ImageryLoadRequested.Notification());

        // Load keybinding overrides and active endpoint from IndexedDB before restoring auth
        // so that TryRestoreSessionAsync reads the persisted endpoint.
        SettingsService settingsService = host.Services.GetRequiredService<SettingsService>();
        await settingsService.LoadFromStorageAsync();

        // Restore OAuth session if a token was previously stored.
        AuthService authService = host.Services.GetRequiredService<AuthService>();
        await authService.TryRestoreSessionAsync();

        // Check IndexedDB for unsaved edits from a previous session.
        EditBufferService editBufferService = host.Services.GetRequiredService<EditBufferService>();
        EditBufferDraft? draft = await editBufferService.TryLoadDraftAsync();
        if (draft is not null)
        {
            await mediator.Publish(new Handlers.EditBuffer.DraftFound.Notification(draft.DirtyCount, draft.SavedAt));
        }

        // If authenticated, fetch settings from the active endpoint and surface any conflicts.
        await settingsService.FetchPrefsFromOsmAsync();

        await host.RunAsync();
    }
}
