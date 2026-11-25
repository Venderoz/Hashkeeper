using Hashkeeper.Tray.ViewModels;

namespace Hashkeeper.Tray.Tests;

public class TrayIconViewModelTests
{
    private readonly FakeShutdownService _shutdown = new();
    private readonly FakeVaultWindowService _windows = new();

    [Fact]
    public void ExitCommand_ShutsTheAppDown()
    {
        var viewModel = NewViewModel(new FakeVaultSession());

        viewModel.ExitCommand.Execute(null);

        Assert.Equal(1, _shutdown.ShutdownCalls);
    }

    [Fact]
    public void OpenVaultCommand_ShowsTheCreateOrUnlockWindow()
    {
        var viewModel = NewViewModel(new FakeVaultSession());

        viewModel.OpenVaultCommand.Execute(null);

        Assert.Equal(1, _windows.ShowCalls);
    }

    [Fact]
    public void LockVaultCommand_LocksTheSession()
    {
        var session = FakeVaultSession.WithVault("password1234");
        session.TryUnlock("password1234");
        var viewModel = NewViewModel(session);

        viewModel.LockVaultCommand.Execute(null);

        Assert.False(session.IsUnlocked);
        Assert.True(viewModel.IsLocked);
    }

    [Fact]
    public void OpenVaultHeader_DependsOnWhetherAVaultExists()
    {
        Assert.Equal("Create vault…", NewViewModel(new FakeVaultSession()).OpenVaultHeader);
        Assert.Equal("Unlock vault…", NewViewModel(FakeVaultSession.WithVault("password1234")).OpenVaultHeader);
    }

    [Fact]
    public void LockStateChange_UpdatesStateAndToolTip()
    {
        var session = FakeVaultSession.WithVault("password1234");
        var viewModel = NewViewModel(session);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal("Hashkeeper – locked", viewModel.ToolTipText);

        session.TryUnlock("password1234");

        Assert.True(viewModel.IsUnlocked);
        Assert.False(viewModel.IsLocked);
        Assert.Equal("Hashkeeper – unlocked", viewModel.ToolTipText);
        Assert.Contains(nameof(TrayIconViewModel.IsUnlocked), changed);
        Assert.Contains(nameof(TrayIconViewModel.IsLocked), changed);
        Assert.Contains(nameof(TrayIconViewModel.ToolTipText), changed);
    }

    private TrayIconViewModel NewViewModel(FakeVaultSession session) => new(_shutdown, session, _windows);
}
