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

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Completed += (_, _) => Close();
        viewModel.PasswordRejected += (_, _) =>
        {
            PasswordInput.Clear();
            PasswordInput.Focus();
        };
        Loaded += (_, _) => PasswordInput.Focus();
    }

    // PasswordBox.Password cannot be data-bound in WPF, so the view copies it into the view model.
    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.Password = PasswordInput.Password;
}
