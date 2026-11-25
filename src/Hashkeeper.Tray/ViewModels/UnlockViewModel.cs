using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hashkeeper.Tray.Services.Vault;
using Microsoft.Data.Sqlite;

namespace Hashkeeper.Tray.ViewModels;

/// <summary>Unlocks an existing vault with the Master Password (HK-29).</summary>
public sealed partial class UnlockViewModel(IVaultSession session) : ObservableObject
{
    internal const string WrongPasswordMessage = "Wrong Master Password.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private string _password = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Raised on the UI thread once the vault is unlocked.</summary>
    public event EventHandler? Completed;

    /// <summary>Raised when the password was rejected, so the view can clear and refocus its input.</summary>
    public event EventHandler? PasswordRejected;

    private bool CanUnlock() => !IsBusy && Password.Length > 0;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        var password = Password;
        try
        {
            // Argon2id takes a moment; keep the window responsive.
            if (await Task.Run(() => session.TryUnlock(password)))
            {
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Same message for a wrong password and for corrupted vault data (docs/crypto.md §6.2).
            ErrorMessage = WrongPasswordMessage;
            Password = "";
            PasswordRejected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or SqliteException)
        {
            ErrorMessage = $"The vault could not be opened: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
