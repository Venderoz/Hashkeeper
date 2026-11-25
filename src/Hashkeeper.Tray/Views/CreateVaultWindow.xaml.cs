using System.Windows;
using Hashkeeper.Tray.ViewModels;

namespace Hashkeeper.Tray.Views;

/// <summary>First-run window that creates the vault (HK-28).</summary>
public partial class CreateVaultWindow : Window
{
    private readonly CreateVaultViewModel _viewModel;

    public CreateVaultWindow(CreateVaultViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Completed += (_, _) => Close();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    // PasswordBox.Password cannot be data-bound in WPF, so the view copies it into the view model.
    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.Password = PasswordInput.Password;

    private void OnConfirmationChanged(object sender, RoutedEventArgs e) => _viewModel.Confirmation = ConfirmationInput.Password;
}
