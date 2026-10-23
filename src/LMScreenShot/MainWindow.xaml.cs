using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using LMScreenShot.Core.Capture;
using LMScreenShot.Core.Config;
using LMScreenShot.Core.Hotkey;
using LMScreenShot.Core.Localization;
using LMScreenShot.Core.Native;
using LMScreenShot.Core.Tray;
using LMScreenShot.Views.Pages;
using LMScreenShot.Views.Windows;
using Wpf.Ui.Appearance;
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
    private readonly TrayIconManager _trayManager = new();
    private readonly List<ScreenOverlayWindow> _activeOverlays = new();

    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowDimensions();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void RestoreWindowDimensions()
    {
        var config = AppConfig.Instance;
        if (config.WindowWidth >= MinWidth)
            Width = config.WindowWidth;
        if (config.WindowHeight >= MinHeight)
            Height = config.WindowHeight;

        if (config.IsMaximized == 1)
            WindowState = WindowState.Maximized;
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            if (ActualWidth >= MinWidth) AppConfig.Instance.WindowWidth = ActualWidth;
            if (ActualHeight >= MinHeight) AppConfig.Instance.WindowHeight = ActualHeight;
            AppConfig.Instance.IsMaximized = 0;
            AppConfig.Instance.Save();
        }
        else if (WindowState == WindowState.Maximized)
        {
            AppConfig.Instance.IsMaximized = 1;
            AppConfig.Instance.Save();
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. Áp dụng cài đặt đã lưu (Theme, System Accent Color & Ngôn ngữ)
        var config = AppConfig.Instance;
        var theme = config.IsDarkMode == 1 ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme);
        LMScreenShot.Core.Theme.AccentColorHelper.Apply(theme);
        LocalizationManager.SetLanguage(config.Lang == "en" ? "en-US" : "vi-VN");

        // 2. Điều hướng ban đầu đến Trang chủ
        RootFrame.Navigate(_homePage);

        // 3. Khởi tạo khay hệ thống (System Tray)
        _trayManager.Initialize(this);

        // 4. Đăng ký phím tắt toàn hệ thống
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
        _trayManager.Dispose();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            if (ActualWidth >= MinWidth) AppConfig.Instance.WindowWidth = ActualWidth;
            if (ActualHeight >= MinHeight) AppConfig.Instance.WindowHeight = ActualHeight;
            AppConfig.Instance.IsMaximized = 0;
            AppConfig.Instance.Save();
        }
        else if (WindowState == WindowState.Maximized)
        {
            AppConfig.Instance.IsMaximized = 1;
            AppConfig.Instance.Save();
        }

        if (!_isExplicitExit && AppConfig.Instance.MinimizeToTray == 1)
        {
            e.Cancel = true;
            this.Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && AppConfig.Instance.MinimizeToTray == 1)
        {
            this.Hide();
        }
        else if (WindowState == WindowState.Maximized)
        {
            AppConfig.Instance.IsMaximized = 1;
            AppConfig.Instance.Save();
        }
        else if (WindowState == WindowState.Normal)
        {
            AppConfig.Instance.IsMaximized = 0;
            AppConfig.Instance.Save();
        }

        base.OnStateChanged(e);
    }

    public void ShowAndRestore()
    {
        this.Show();
        if (this.WindowState == WindowState.Minimized)
        {
            this.WindowState = WindowState.Normal;
        }
        this.Activate();
        this.Focus();
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        _trayManager.Dispose();
        CloseAllOverlays();
        _hotkeyManager.Dispose();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Mở giao diện Overlay Chụp tương tác (Ctrl + Shift + X hoặc bấm chuột trái vào Tray Icon hoặc nút Ảnh chụp mới)
    /// </summary>
    public void OpenCaptureOverlay()
    {
        CloseAllOverlays();

        bool wasVisible = this.Visibility == Visibility.Visible && this.WindowState != WindowState.Minimized;

        // Ẩn tạm thời cửa sổ chính nếu đang mở
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
                if (wasVisible)
                {
                    ShowAndRestore();
                }
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
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
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
