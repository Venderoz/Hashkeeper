namespace Hashkeeper.Tray.Services;

/// <summary>Shuts the application down.</summary>
/// <remarks>
/// Keeps view models free of WPF types (<c>Application.Current</c>), so they can be unit-tested.
/// </remarks>
public interface IShutdownService
{
    void Shutdown();
}
