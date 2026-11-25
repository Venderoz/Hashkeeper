using System.Windows;

namespace Hashkeeper.Tray.Services;

/// <summary>Shuts down the running WPF application.</summary>
public sealed class WpfShutdownService : IShutdownService
{
    public void Shutdown() => Application.Current.Shutdown();
}
