using System.Windows;
using Hashkeeper.Tray.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Hashkeeper.Tray.Services.Vault;

/// <summary>Shows <see cref="CreateVaultWindow"/> or <see cref="UnlockWindow"/>, one at a time.</summary>
public sealed class WpfVaultWindowService(IServiceProvider services, IVaultSession session) : IVaultWindowService
{
    private Window? _openWindow;

    public void ShowCreateOrUnlock()
    {
        if (_openWindow is not null)
        {
            _openWindow.Activate();
            return;
        }

        if (session.IsUnlocked)
        {
            return;
        }

        Window window = session.VaultExists
            ? services.GetRequiredService<UnlockWindow>()
            : services.GetRequiredService<CreateVaultWindow>();

        window.Closed += (_, _) => _openWindow = null;
        _openWindow = window;
        window.Show();
        window.Activate();
    }
}
