using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using LMScreenShot.Core.Capture;
using LMScreenShot.Core.Hotkey;
using LMScreenShot.Core.Native;
using LMScreenShot.Views.Pages;
using LMScreenShot.Views.Windows;
using Wpf.Ui.Controls;

namespace LMScreenShot;

public partial class MainWindow : FluentWindow
{
    private readonly HomePage _homePage = new();
    private readonly GalleryPage _galleryPage = new();
    private readonly ShortcutsPage _shortcutsPage = new();
    private readonly SettingsPage _settingsPage = new();
    private readonly AboutPage _aboutPage = new();

    private readonly GlobalHotkeyManager _hotkeyManager = new();
    private readonly List<ScreenOverlayWindow> _activeOverlays = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Điều hướng ban đầu đến Trang chủ
        RootFrame.Navigate(_homePage);

        // Đăng ký phím tắt toàn hệ thống
        var helper = new WindowInteropHelper(this);
        _hotkeyManager.Register(helper.Handle, ScreenCapturer.GetMonitors().Count);
        _hotkeyManager.CaptureOverlayRequested += OpenCaptureOverlay;
        _hotkeyManager.IdentifyOverlayRequested += OpenIdentifyOverlay;
        _hotkeyManager.InstantCaptureRequested += HandleInstantCapture;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        CloseAllOverlays();
        _hotkeyManager.Dispose();
    }

    /// <summary>
    /// Mở giao diện Overlay Chụp tương tác (Ctrl + Shift + X hoặc bấm nút Ảnh chụp mới)
    /// </summary>
    public void OpenCaptureOverlay()
    {
        CloseAllOverlays();

        // Ẩn hoàn toàn cửa sổ chính
        this.Hide();

        var monitors = ScreenCapturer.GetMonitors();
        foreach (var monitor in monitors)
        {
            var overlay = new ScreenOverlayWindow(monitor, OverlayMode.Capture);

            overlay.CaptureTriggered += async (targetMon) =>
            {
                CloseAllOverlays();
                await _homePage.ExecuteCaptureAsync(targetMon);
            };

            overlay.NumberKeyTriggered += async (num) =>
            {
                var targetMon = monitors.FirstOrDefault(m => m.DisplayNumber == num);
                if (targetMon != null)
                {
                    CloseAllOverlays();
                    await _homePage.ExecuteCaptureAsync(targetMon);
                }
            };

            overlay.CancelTriggered += () =>
            {
                CloseAllOverlays();
                this.Show();
                this.Activate();
            };

            _activeOverlays.Add(overlay);
            overlay.Show();
        }
    }

    /// <summary>
    /// Mở giao diện Nhận diện màn hình (Identify) không làm phiền (Ctrl + Shift + `)
    /// </summary>
    public void OpenIdentifyOverlay()
    {
        CloseAllOverlays();

        var monitors = ScreenCapturer.GetMonitors();
        foreach (var monitor in monitors)
        {
            var overlay = new ScreenOverlayWindow(monitor, OverlayMode.Identify);
            _activeOverlays.Add(overlay);
            overlay.Show();
        }
    }

    private async void HandleInstantCapture(int monitorNumber)
    {
        var monitors = ScreenCapturer.GetMonitors();
        var targetMon = monitors.FirstOrDefault(m => m.DisplayNumber == monitorNumber);
        if (targetMon != null)
        {
            await _homePage.ExecuteCaptureAsync(targetMon);
        }
    }

    private void CloseAllOverlays()
    {
        foreach (var overlay in _activeOverlays.ToList())
        {
            try { overlay.Close(); } catch { }
        }
        _activeOverlays.Clear();
    }

    /// <summary>
    /// Hiệu ứng Fluent Entrance (Slide-Up & Fade-In) chuẩn Windows 11 mỗi khi chuyển tab.
    /// </summary>
    private void RootFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is FrameworkElement pageContent)
        {
            var transform = new TranslateTransform(0, 16);
            pageContent.RenderTransform = transform;

            var slideAnim = new DoubleAnimation
            {
                From = 16,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            pageContent.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }
    }

    private void NavHome_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.Content != _homePage)
        {
            RootFrame.Navigate(_homePage);
        }
        _homePage.RefreshMonitors();
    }

    private void NavGallery_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.Content != _galleryPage)
        {
            RootFrame.Navigate(_galleryPage);
        }
    }

    private void NavShortcuts_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.Content != _shortcutsPage)
        {
            RootFrame.Navigate(_shortcutsPage);
        }
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.Content != _settingsPage)
        {
            RootFrame.Navigate(_settingsPage);
        }
    }

    private void NavAbout_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.Content != _aboutPage)
        {
            RootFrame.Navigate(_aboutPage);
        }
    }
}
