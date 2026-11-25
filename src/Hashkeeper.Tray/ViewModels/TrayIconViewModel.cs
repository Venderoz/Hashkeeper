using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hashkeeper.Tray.Services;
using Hashkeeper.Tray.Services.Vault;

namespace Hashkeeper.Tray.ViewModels;

/// <summary>State and commands of the tray icon and its context menu.</summary>
public sealed partial class TrayIconViewModel : ObservableObject
{
    private readonly IShutdownService _shutdownService;
    private readonly IVaultSession _session;
    private readonly IVaultWindowService _windows;

    public TrayIconViewModel(IShutdownService shutdownService, IVaultSession session, IVaultWindowService windows)
    {
        _shutdownService = shutdownService;
        _session = session;
        _windows = windows;

        // May be raised on a background thread; WPF marshals property-change notifications for bindings.
        _session.LockStateChanged += (_, _) => RefreshLockState();
    }

    public bool IsUnlocked => _session.IsUnlocked;

    public bool IsLocked => !_session.IsUnlocked;

    public string ToolTipText => IsUnlocked ? "Hashkeeper – unlocked" : "Hashkeeper – locked";

    /// <summary>Menu text for opening the vault: create it on first run, otherwise unlock it.</summary>
    public string OpenVaultHeader => _session.VaultExists ? "Unlock vault…" : "Create vault…";

    /// <summary>Shows the create or unlock window (also bound to a left click on the icon).</summary>
    [RelayCommand]
    private void OpenVault() => _windows.ShowCreateOrUnlock();

    [RelayCommand]
    private void LockVault() => _session.Lock();

    [RelayCommand]
    private void Exit() => _shutdownService.Shutdown();

    private void RefreshLockState()
    {
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(ToolTipText));
        OnPropertyChanged(nameof(OpenVaultHeader));
    }
}
