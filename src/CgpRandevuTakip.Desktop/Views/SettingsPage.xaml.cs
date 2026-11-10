using System.Windows;
using System.Windows.Controls;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Views;

public partial class SettingsPage : UserControl
{
    private bool _syncingPassword;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.LoadCommand.ExecuteAsync(null);

            _syncingPassword = true;
            try
            {
                BackupPasswordBox.Password = viewModel.BackupPassword;
                BackupPasswordConfirmBox.Password = viewModel.BackupPasswordConfirm;
                GoogleClientSecretBox.Password = viewModel.GoogleClientSecret;
            }
            finally
            {
                _syncingPassword = false;
            }
        }
    }

    private void OnBackupPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword || DataContext is not SettingsViewModel viewModel)
            return;

        viewModel.BackupPassword = BackupPasswordBox.Password;
    }

    private void OnBackupPasswordConfirmChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword || DataContext is not SettingsViewModel viewModel)
            return;

        viewModel.BackupPasswordConfirm = BackupPasswordConfirmBox.Password;
    }

    private void OnGoogleClientSecretChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword || DataContext is not SettingsViewModel viewModel)
            return;

        viewModel.GoogleClientSecret = GoogleClientSecretBox.Password;
    }
}
