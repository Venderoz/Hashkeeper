using Hashkeeper.Tray.Services;
using Hashkeeper.Tray.Services.Storage;
using Hashkeeper.Tray.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hashkeeper.Tray;

/// <summary>Registers the Tray app's services and view models.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrayServices(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IShutdownService, WpfShutdownService>();
        services.AddSingleton<ILocalVaultStore>(_ => new LocalVaultStore(LocalVaultStore.DefaultFilePath));

        // View models
        services.AddSingleton<TrayIconViewModel>();

        return services;
    }
}
