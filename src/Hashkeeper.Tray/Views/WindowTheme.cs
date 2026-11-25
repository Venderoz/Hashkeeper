using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Hashkeeper.Tray.Views;

/// <summary>Colours the window's title bar and border to match the app palette (Windows 11).</summary>
/// <remarks>On older Windows versions the attributes are not supported and the call is ignored.</remarks>
internal static class WindowTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            SetInt(handle, DwmwaUseImmersiveDarkMode, 1);
            SetColor(handle, DwmwaCaptionColor, (Color)window.FindResource("NavyColor"));
            SetColor(handle, DwmwaBorderColor, (Color)window.FindResource("CyanColor"));
            SetColor(handle, DwmwaTextColor, Color.FromRgb(0xE8, 0xF6, 0xF8));
        };
    }

    // COLORREF is 0x00BBGGRR.
    private static void SetColor(IntPtr handle, int attribute, Color color) =>
        SetInt(handle, attribute, color.R | (color.G << 8) | (color.B << 16));

    private static void SetInt(IntPtr handle, int attribute, int value) =>
        _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
