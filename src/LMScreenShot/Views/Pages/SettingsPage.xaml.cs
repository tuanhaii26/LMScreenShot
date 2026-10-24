using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LMScreenShot.Core.Config;
using LMScreenShot.Core.Localization;
using LMScreenShot.Core.Utils;
using Wpf.Ui.Appearance;

namespace LMScreenShot.Views.Pages;

public partial class SettingsPage : Page
{
    private bool _isInitializing = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        var config = AppConfig.Instance;

        TxtFolderPath.Text = config.ResolvedSavePath;
        ToggleMinimizeToTray.IsChecked = config.MinimizeToTray == 1;
        ToggleMinimizeOnClose.IsChecked = config.MinimizeOnClose == 1;
        ToggleDarkMode.IsChecked = config.IsDarkMode == 1;

        ComboStartup.SelectedIndex = Math.Clamp(config.Startup, 0, 2);
        ComboLanguage.SelectedIndex = config.Lang == "en" ? 1 : 0;

        _isInitializing = false;
    }

    private void ComboStartup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        int selected = ComboStartup.SelectedIndex;
        if (selected >= 0 && selected <= 2)
        {
            AppConfig.Instance.Startup = selected;
            AppConfig.Instance.Save();
            StartupManager.ApplyStartup(selected);
        }
    }

    private void ComboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (ComboLanguage.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
        {
            string shortLang = (langCode == "en-US" || langCode == "en") ? "en" : "vi";
            AppConfig.Instance.Lang = shortLang;
            AppConfig.Instance.Save();

            LocalizationManager.SetLanguage(shortLang == "en" ? "en-US" : "vi-VN");
        }
    }

    private void BtnChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = LocalizationManager.GetString("Loc_Settings_DefaultFolder"),
            InitialDirectory = AppConfig.Instance.ResolvedSavePath
        };

        if (dialog.ShowDialog() == true)
        {
            AppConfig.Instance.SavePath = dialog.FolderName;
            AppConfig.Instance.Save();
            TxtFolderPath.Text = dialog.FolderName;
        }
    }

    private void ToggleMinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        AppConfig.Instance.MinimizeToTray = ToggleMinimizeToTray.IsChecked == true ? 1 : 0;
        AppConfig.Instance.Save();
    }

    private void ToggleMinimizeOnClose_Click(object sender, RoutedEventArgs e)
    {
        AppConfig.Instance.MinimizeOnClose = ToggleMinimizeOnClose.IsChecked == true ? 1 : 0;
        AppConfig.Instance.Save();
    }

    private void ToggleDarkMode_Click(object sender, RoutedEventArgs e)
    {
        bool isDark = ToggleDarkMode.IsChecked == true;
        AppConfig.Instance.IsDarkMode = isDark ? 1 : 0;
        AppConfig.Instance.Save();

        var theme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme);
        LMScreenShot.Core.Theme.AccentColorHelper.Apply(theme);
    }
}
