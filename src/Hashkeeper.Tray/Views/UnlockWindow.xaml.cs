using System.Windows;
using Hashkeeper.Tray.ViewModels;

namespace Hashkeeper.Tray.Views;

/// <summary>Asks for the Master Password to unlock the vault (HK-29).</summary>
public partial class UnlockWindow : Window
{
    private readonly UnlockViewModel _viewModel;

    public UnlockWindow(UnlockViewModel viewModel)
    {
        InitializeComponent();
        WindowTheme.Apply(this);

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Completed += (_, _) => Close();
        viewModel.PasswordRejected += (_, _) =>
        {
            PasswordInput.Clear();
            PasswordInput.FocusInput();
        };
        Loaded += (_, _) => PasswordInput.FocusInput();
    }

    // IsCancel only closes dialogs shown with ShowDialog; this window is shown with Show().
    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    // A password cannot be data-bound in WPF, so the view copies it into the view model.
    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.Password = PasswordInput.Password;
}
