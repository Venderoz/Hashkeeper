using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hashkeeper.Tray.Services;

namespace Hashkeeper.Tray.ViewModels;

/// <summary>State and commands of the tray icon and its context menu.</summary>
public sealed partial class TrayIconViewModel(IShutdownService shutdownService) : ObservableObject
{
    public string ToolTipText => "Hashkeeper";

    [RelayCommand]
    private void Exit() => shutdownService.Shutdown();
}
