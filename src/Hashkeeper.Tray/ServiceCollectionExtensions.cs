using Hashkeeper.Tray.Services;
using Hashkeeper.Tray.Services.Storage;
using Hashkeeper.Tray.Services.Vault;
using Hashkeeper.Tray.ViewModels;
using Hashkeeper.Tray.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Hashkeeper.Tray;

/// <summary>Registers the Tray app's services, view models and windows.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    /// <param name="vaultFilePath">
    /// Vault file to use instead of <see cref="LocalVaultStore.DefaultFilePath"/>, e.g. for manual testing
    /// (<c>--VaultPath=C:\temp\test.db</c> on the command line).
    /// </param>
    public static IServiceCollection AddTrayServices(this IServiceCollection services, string? vaultFilePath = null)
    {
        var vaultPath = string.IsNullOrWhiteSpace(vaultFilePath) ? LocalVaultStore.DefaultFilePath : vaultFilePath;

        // Services
        services.AddSingleton<IShutdownService, WpfShutdownService>();
        services.AddSingleton<ILocalVaultStore>(_ => new LocalVaultStore(vaultPath));

        // Holds the Vault Key while unlocked; the container disposes it on shutdown, which clears the key.
        services.AddSingleton<IVaultSession, VaultSession>();
        services.AddSingleton<IVaultWindowService, WpfVaultWindowService>();

        // View models
        services.AddSingleton<TrayIconViewModel>();
        services.AddTransient<CreateVaultViewModel>();
        services.AddTransient<UnlockViewModel>();

        // Windows: a new instance every time one is opened
        services.AddTransient<CreateVaultWindow>();
        services.AddTransient<UnlockWindow>();

        return services;
    }
}
