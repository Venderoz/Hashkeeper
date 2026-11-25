using System.IO;
using Hashkeeper.Tray.Services;
using Hashkeeper.Tray.Services.Storage;
using Hashkeeper.Tray.Services.Vault;
using Hashkeeper.Tray.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hashkeeper.Tray.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTrayServices_ResolvesTrayIconViewModel()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<TrayIconViewModel>());
    }

    [Fact]
    public void AddTrayServices_RegistersWpfShutdownService()
    {
        using var provider = BuildProvider();

        Assert.IsType<WpfShutdownService>(provider.GetRequiredService<IShutdownService>());
    }

    [Fact]
    public void AddTrayServices_TrayIconViewModelIsSingleton()
    {
        using var provider = BuildProvider();

        Assert.Same(
            provider.GetRequiredService<TrayIconViewModel>(),
            provider.GetRequiredService<TrayIconViewModel>());
    }

    [Fact]
    public void AddTrayServices_UsesDefaultVaultPath()
    {
        using var provider = BuildProvider();

        Assert.Equal(LocalVaultStore.DefaultFilePath, provider.GetRequiredService<ILocalVaultStore>().FilePath);
    }

    [Fact]
    public void AddTrayServices_CanOverrideVaultPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "hashkeeper-tests", "override.db");
        using var provider = new ServiceCollection().AddTrayServices(path).BuildServiceProvider();

        Assert.Equal(path, provider.GetRequiredService<ILocalVaultStore>().FilePath);
    }

    [Fact]
    public void AddTrayServices_VaultSessionIsSingleton_WindowViewModelsAreTransient()
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IVaultSession>(), provider.GetRequiredService<IVaultSession>());
        Assert.NotSame(provider.GetRequiredService<UnlockViewModel>(), provider.GetRequiredService<UnlockViewModel>());
        Assert.NotSame(provider.GetRequiredService<CreateVaultViewModel>(), provider.GetRequiredService<CreateVaultViewModel>());
    }

    // ValidateOnBuild catches missing registrations for every service, not only the ones resolved here.
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddTrayServices()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
