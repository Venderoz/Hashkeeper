using Hashkeeper.Tray.Services;
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

    // ValidateOnBuild catches missing registrations for every service, not only the ones resolved here.
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddTrayServices()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
