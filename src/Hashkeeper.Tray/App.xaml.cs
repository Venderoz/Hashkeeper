using System.Windows;
using H.NotifyIcon;
using Hashkeeper.Tray.Services.Vault;
using Hashkeeper.Tray.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hashkeeper.Tray;

/// <summary>
/// Starts the app in the system tray: there is no main window, only the tray icon.
/// The app runs until it is shut down explicitly (e.g. "Exit" in the context menu).
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddTrayServices(builder.Configuration["VaultPath"]);
        _host = builder.Build();
        _host.Start();

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.DataContext = _host.Services.GetRequiredService<TrayIconViewModel>();

        // The icon is defined as a resource, not in a window, so it has to be created explicitly.
        // Efficiency mode stays off so the app reacts immediately to the tray menu and, later, the hotkey.
        _trayIcon.ForceCreate(false);

        // First run: create the vault. Otherwise: ask for the Master Password right away.
        _host.Services.GetRequiredService<IVaultWindowService>().ShowCreateOrUnlock();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Detach the view model first, so nothing can update the icon after it is released.
        if (_trayIcon is not null)
        {
            _trayIcon.DataContext = null;
            _trayIcon.Dispose();
        }

        // Disposing the host locks the vault, which clears the Vault Key from memory.
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
