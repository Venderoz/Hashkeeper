namespace Hashkeeper.Tray.Services.Vault;

/// <summary>Opens the window that gets the user into the vault.</summary>
/// <remarks>Keeps view models free of WPF window types, so they can be unit-tested.</remarks>
public interface IVaultWindowService
{
    /// <summary>
    /// Shows the create-vault window on first run, otherwise the unlock window.
    /// Does nothing if the vault is already unlocked; brings an already open window to the front.
    /// </summary>
    void ShowCreateOrUnlock();
}
