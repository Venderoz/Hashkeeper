using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Hashkeeper.Tray.Views.Controls;

/// <summary>A password box with an eye button that shows the typed password as plain text.</summary>
/// <remarks>
/// The <see cref="PasswordBox"/> always holds the current password. While the password is shown,
/// a <see cref="TextBox"/> is displayed instead and every edit is copied back; when it is hidden
/// again the text box is cleared, so the plain text is only in a visible control while the user wants it.
/// </remarks>
public partial class RevealablePasswordBox : UserControl
{
    private bool _syncing;

    public RevealablePasswordBox()
    {
        InitializeComponent();
    }

    /// <summary>Raised whenever the password changes, whether it is hidden or shown.</summary>
    public event RoutedEventHandler? PasswordChanged;

    public string Password => HiddenInput.Password;

    public bool IsRevealed => RevealToggle.IsChecked == true;

    /// <summary>Clears the password, hides it again and raises <see cref="PasswordChanged"/>.</summary>
    public void Clear()
    {
        RevealToggle.IsChecked = false;
        RunSynced(() =>
        {
            HiddenInput.Clear();
            VisibleInput.Clear();
        });
        PasswordChanged?.Invoke(this, new RoutedEventArgs());
    }

    /// <summary>Moves keyboard focus to whichever input is currently shown.</summary>
    public void FocusInput()
    {
        if (IsRevealed)
        {
            VisibleInput.Focus();
            VisibleInput.CaretIndex = VisibleInput.Text.Length;
        }
        else
        {
            HiddenInput.Focus();
        }
    }

    // A Label's access key (e.g. Alt+M) focuses the control itself; pass the focus on to the input.
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (ReferenceEquals(e.NewFocus, this))
        {
            FocusInput();
        }
    }

    private void OnHiddenInputChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            PasswordChanged?.Invoke(this, new RoutedEventArgs());
        }
    }

    private void OnVisibleInputChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        RunSynced(() => HiddenInput.Password = VisibleInput.Text);
        PasswordChanged?.Invoke(this, new RoutedEventArgs());
    }

    private void OnRevealChanged(object sender, RoutedEventArgs e)
    {
        RunSynced(() =>
        {
            if (IsRevealed)
            {
                VisibleInput.Text = HiddenInput.Password;
                VisibleInput.Visibility = Visibility.Visible;
                HiddenInput.Visibility = Visibility.Collapsed;
            }
            else
            {
                VisibleInput.Clear();
                VisibleInput.Visibility = Visibility.Collapsed;
                HiddenInput.Visibility = Visibility.Visible;
            }
        });

        var label = IsRevealed ? "Hide password" : "Show password";
        RevealToggle.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(RevealToggle, label);
        FocusInput();
    }

    private void RunSynced(Action action)
    {
        _syncing = true;
        try
        {
            action();
        }
        finally
        {
            _syncing = false;
        }
    }
}
