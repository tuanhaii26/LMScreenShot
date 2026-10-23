using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace LMScreenShot.Views.Pages;

public partial class SettingsPage : Page
{
    private string _screenshotDirectory = string.Empty;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _screenshotDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Screenshots");

        TxtFolderPath.Text = _screenshotDirectory;
    }

    private void BtnChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Chọn thư mục lưu ảnh chụp màn hình",
            InitialDirectory = _screenshotDirectory
        };

        if (dialog.ShowDialog() == true)
        {
            _screenshotDirectory = dialog.FolderName;
            TxtFolderPath.Text = _screenshotDirectory;
        }
    }

    private void ToggleDarkMode_Click(object sender, RoutedEventArgs e)
    {
        if (ToggleDarkMode.IsChecked == true)
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }
        else
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Light);
        }
    }
}
