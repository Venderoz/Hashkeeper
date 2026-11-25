using System.IO;
using Hashkeeper.Tray.ViewModels;

namespace Hashkeeper.Tray.Tests;

public class UnlockViewModelTests
{
    private const string Password = "correct horse battery";

    [Fact]
    public void EmptyPassword_CannotUnlock()
    {
        var viewModel = new UnlockViewModel(FakeVaultSession.WithVault(Password));

        Assert.False(viewModel.UnlockCommand.CanExecute(null));
    }

    [Fact]
    public async Task CorrectPassword_UnlocksAndCompletes()
    {
        var session = FakeVaultSession.WithVault(Password);
        var viewModel = new UnlockViewModel(session) { Password = Password };
        var completed = 0;
        viewModel.Completed += (_, _) => completed++;

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.True(session.IsUnlocked);
        Assert.Equal(1, completed);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task WrongPassword_ShowsGenericErrorClearsInputAndStaysLocked()
    {
        var session = FakeVaultSession.WithVault(Password);
        var viewModel = new UnlockViewModel(session) { Password = "not the password" };
        var completed = 0;
        var rejected = 0;
        viewModel.Completed += (_, _) => completed++;
        viewModel.PasswordRejected += (_, _) => rejected++;

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.False(session.IsUnlocked);
        Assert.Equal(0, completed);
        Assert.Equal(1, rejected);
        Assert.Equal(UnlockViewModel.WrongPasswordMessage, viewModel.ErrorMessage);
        Assert.Equal("", viewModel.Password);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RetryAfterWrongPassword_Unlocks()
    {
        var session = FakeVaultSession.WithVault(Password);
        var viewModel = new UnlockViewModel(session) { Password = "wrong" };
        await viewModel.UnlockCommand.ExecuteAsync(null);

        viewModel.Password = Password;
        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.True(session.IsUnlocked);
        Assert.Equal(["wrong", Password], session.UnlockAttempts);
    }

    [Fact]
    public async Task UnreadableVaultFile_ShowsErrorWithoutClaimingWrongPassword()
    {
        var session = FakeVaultSession.WithVault(Password);
        session.FailWith = new IOException("The file is in use.");
        var viewModel = new UnlockViewModel(session) { Password = Password };

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.Equal("The vault could not be opened: The file is in use.", viewModel.ErrorMessage);
        Assert.Equal(Password, viewModel.Password);
        Assert.False(viewModel.IsBusy);
    }
}
