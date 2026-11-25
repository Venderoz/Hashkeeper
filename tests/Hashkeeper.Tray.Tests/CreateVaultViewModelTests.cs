using System.IO;
using Hashkeeper.Tray.ViewModels;

namespace Hashkeeper.Tray.Tests;

public class CreateVaultViewModelTests
{
    private const string GoodPassword = "correct horse battery";

    [Fact]
    public void EmptyInput_CannotCreateAndAsksForLength()
    {
        var viewModel = new CreateVaultViewModel(new FakeVaultSession());

        Assert.False(viewModel.CreateCommand.CanExecute(null));
        Assert.Equal("Use at least 12 characters.", viewModel.ValidationMessage);
    }

    [Fact]
    public void ShortPassword_CannotCreate()
    {
        var viewModel = new CreateVaultViewModel(new FakeVaultSession())
        {
            Password = "elevenchars",
            Confirmation = "elevenchars",
        };

        Assert.False(viewModel.CreateCommand.CanExecute(null));
        Assert.Equal("Use at least 12 characters.", viewModel.ValidationMessage);
    }

    [Fact]
    public void MismatchedConfirmation_CannotCreate()
    {
        var viewModel = new CreateVaultViewModel(new FakeVaultSession())
        {
            Password = GoodPassword,
            Confirmation = GoodPassword + "!",
        };

        Assert.False(viewModel.CreateCommand.CanExecute(null));
        Assert.Equal("The passwords do not match.", viewModel.ValidationMessage);
    }

    [Fact]
    public void MissingConfirmation_CannotCreateButShowsNoMismatchYet()
    {
        var viewModel = new CreateVaultViewModel(new FakeVaultSession()) { Password = GoodPassword };

        Assert.False(viewModel.CreateCommand.CanExecute(null));
        Assert.Null(viewModel.ValidationMessage);
    }

    [Fact]
    public void ValidInput_CanCreate()
    {
        var viewModel = NewValidViewModel(new FakeVaultSession());

        Assert.Null(viewModel.ValidationMessage);
        Assert.True(viewModel.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void ValidationMessage_UpdatesWhenTyping()
    {
        var viewModel = new CreateVaultViewModel(new FakeVaultSession());
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Password = GoodPassword;

        Assert.Contains(nameof(CreateVaultViewModel.ValidationMessage), changed);
    }

    [Fact]
    public async Task Create_CreatesTheVaultAndCompletes()
    {
        var session = new FakeVaultSession();
        var viewModel = NewValidViewModel(session);
        var completed = 0;
        viewModel.Completed += (_, _) => completed++;

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.True(session.VaultExists);
        Assert.True(session.IsUnlocked);
        Assert.Equal(1, completed);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Create_WhenStorageFails_ShowsErrorAndDoesNotComplete()
    {
        var session = new FakeVaultSession { FailWith = new IOException("Disk full.") };
        var viewModel = NewValidViewModel(session);
        var completed = 0;
        viewModel.Completed += (_, _) => completed++;

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(0, completed);
        Assert.Equal("The vault could not be created: Disk full.", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.CreateCommand.CanExecute(null));
    }

    private static CreateVaultViewModel NewValidViewModel(FakeVaultSession session) =>
        new(session) { Password = GoodPassword, Confirmation = GoodPassword };
}
