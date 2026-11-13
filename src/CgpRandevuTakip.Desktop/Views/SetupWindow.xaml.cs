using System.ComponentModel;
using System.Windows;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class SetupWindow : Window
{
    private readonly SetupViewModel _viewModel;
    private bool _allowClose;
    private bool _closeConfirmed;

    public SetupWindow(SetupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.SetupSucceeded += OnSetupSucceeded;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
        PasswordBox.Focus();
    }

    private void OnSetupSucceeded(object? sender, EventArgs e)
    {
        _allowClose = true;
        DialogResult = true;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose && !_closeConfirmed)
        {
            var message = _viewModel.IsUpdateMode
                ? "Değişiklikler kaydedilmedi. Kurulum sihirbazından çıkılsın mı?"
                : "Kurulum tamamlanmadı. Çıkarsanız yönetici hesabı oluşturulmayacak. Çıkılsın mı?";

            var result = MessageBox.Show(
                this,
                message,
                "Kurulum sihirbazı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _closeConfirmed = true;
        }

        if (!_allowClose && _closeConfirmed)
            _viewModel.RestoreThemeOnCancel();

        _viewModel.SetupSucceeded -= OnSetupSucceeded;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.Password = PasswordBox.Password;

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.ConfirmPassword = ConfirmPasswordBox.Password;

    private void OnCurrentPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.CurrentPassword = CurrentPasswordBox.Password;

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.NewPassword = NewPasswordBox.Password;

    private void OnNewConfirmPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.NewConfirmPassword = NewConfirmPasswordBox.Password;

    private void OnSmtpPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.SmtpPassword = SmtpPasswordBox.Password;

    private void OnBackupPasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.BackupPassword = BackupPasswordBox.Password;

    private void OnBackupPasswordConfirmChanged(object sender, RoutedEventArgs e)
        => _viewModel.BackupPasswordConfirm = BackupPasswordConfirmBox.Password;
}
