using Hashkeeper.Tray.Services;
using Hashkeeper.Tray.ViewModels;

namespace Hashkeeper.Tray.Tests;

public class TrayIconViewModelTests
{
    [Fact]
    public void ExitCommand_ShutsTheAppDown()
    {
        var shutdownService = new FakeShutdownService();
        var viewModel = new TrayIconViewModel(shutdownService);

        viewModel.ExitCommand.Execute(null);

        Assert.Equal(1, shutdownService.ShutdownCalls);
    }

    [Fact]
    public void ExitCommand_CanAlwaysExecute()
    {
        var viewModel = new TrayIconViewModel(new FakeShutdownService());

        Assert.True(viewModel.ExitCommand.CanExecute(null));
    }

    [Fact]
    public void ToolTipText_IsAppName()
    {
        var viewModel = new TrayIconViewModel(new FakeShutdownService());

        Assert.Equal("Hashkeeper", viewModel.ToolTipText);
    }

    private sealed class FakeShutdownService : IShutdownService
    {
        public int ShutdownCalls { get; private set; }

        public void Shutdown() => ShutdownCalls++;
    }
}
