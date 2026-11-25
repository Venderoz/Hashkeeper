using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hashkeeper.Tray.Services.Vault;
using Microsoft.Data.Sqlite;

namespace Hashkeeper.Tray.ViewModels;

/// <summary>First run: choose a Master Password and create the vault (HK-28).</summary>
public sealed partial class CreateVaultViewModel(IVaultSession session) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _password = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _confirmation = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Raised on the UI thread once the vault has been created (it is then unlocked).</summary>
    public event EventHandler? Completed;

    public int MinimumPasswordLength => IVaultSession.MinimumPasswordLength;

    /// <summary>What is still wrong with the input, or <see langword="null"/> when it can be submitted.</summary>
    public string? ValidationMessage
    {
        get
        {
            if (Password.Length < MinimumPasswordLength)
            {
                return $"Use at least {MinimumPasswordLength} characters.";
            }

            if (Confirmation.Length > 0 && Confirmation != Password)
            {
                return "The passwords do not match.";
            }

            return null;
        }
    }

    private bool CanCreate() => !IsBusy && ValidationMessage is null && Confirmation == Password;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        var password = Password;
        try
        {
            // Argon2id takes a moment; keep the window responsive.
            await Task.Run(() => session.CreateVault(password));
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or SqliteException)
        {
            ErrorMessage = $"The vault could not be created: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
