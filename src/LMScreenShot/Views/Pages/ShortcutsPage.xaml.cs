using System.Windows;
using System.Windows.Controls;
using LMScreenShot.Core.Config;

namespace LMScreenShot.Views.Pages;

public partial class ShortcutsPage : Page
{
    public ShortcutsPage()
    {
        InitializeComponent();
        Loaded += ShortcutsPage_Loaded;
    }

    private void ShortcutsPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshShortcuts();
    }

    public void RefreshShortcuts()
    {
        var config = AppConfig.Instance;
        TxtShortcutOverlay.Text = config.CaptureHotkeyText;
        TxtShortcutIdentify.Text = config.IdentifyHotkeyText;
        TxtShortcutInstant.Text = config.InstantHotkeyText;
    }

    private void BtnChangeShortcuts_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current.MainWindow as MainWindow)?.ShowShortcutsDialog();
    }
}
