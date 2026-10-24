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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            WindowState = WindowState.Minimized;
            Hide();
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

        // 4. Đăng ký phím tắt toàn hệ thống & Hook nhận thông điệp Single Instance
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);

        _hotkeyManager.Register(helper.Handle, ScreenCapturer.GetMonitors().Count);
        _hotkeyManager.CaptureOverlayRequested += OpenCaptureOverlay;
        _hotkeyManager.IdentifyOverlayRequested += OpenIdentifyOverlay;
        _hotkeyManager.InstantCaptureRequested += HandleInstantCapture;

        // 5. Kiểm tra nếu khởi động thu nhỏ
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            WindowState = WindowState.Minimized;
            Hide();
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == App.WM_SHOWME)
        {
            ShowAndRestore();
            NativeMethods.SetForegroundWindow(hwnd);
            handled = true;
        }
        return nint.Zero;
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

        if (!_isExplicitExit)
        {
            if (AppConfig.Instance.MinimizeOnClose == 1)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            else
            {
                ExitApplication();
                return;
            }
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
        if (wasVisible)
        {
            this.Hide();
        }

        bool isHandled = false;
        var monitors = ScreenCapturer.GetMonitors();
        foreach (var monitor in monitors)
        {
            var overlay = new ScreenOverlayWindow(monitor, OverlayMode.Capture);

            overlay.CaptureTriggered += async (targetMon) =>
            {
                if (isHandled) return;
                isHandled = true;

                CloseAllOverlays();
                await _homePage.ExecuteCaptureAsync(targetMon);

                if (wasVisible)
                {
                    ShowAndRestore();
                }
            };

            overlay.NumberKeyTriggered += async (num) =>
            {
                if (isHandled) return;

                var targetMon = monitors.FirstOrDefault(m => m.DisplayNumber == num);
                if (targetMon != null)
                {
                    isHandled = true;
                    CloseAllOverlays();
                    await _homePage.ExecuteCaptureAsync(targetMon);

                    if (wasVisible)
                    {
                        ShowAndRestore();
                    }
                }
            };

            overlay.CancelTriggered += () =>
            {
                if (isHandled) return;
                isHandled = true;

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

    #region Shortcut Configuration Dialog

    public void ShowShortcutsDialog()
    {
        var config = AppConfig.Instance;

        // 1. Chụp tương tác
        ChkOverlayCtrl.IsChecked = (config.CaptureModifiers & NativeMethods.MOD_CONTROL) != 0;
        ChkOverlayShift.IsChecked = (config.CaptureModifiers & NativeMethods.MOD_SHIFT) != 0;
        ChkOverlayAlt.IsChecked = (config.CaptureModifiers & NativeMethods.MOD_ALT) != 0;
        ChkOverlayWin.IsChecked = (config.CaptureModifiers & NativeMethods.MOD_WIN) != 0;
        TxtOverlayKey.Tag = config.CaptureKey;
        TxtOverlayKey.Text = AppConfig.FormatKey(config.CaptureKey);

        // 2. Nhận diện màn hình
        ChkIdentifyCtrl.IsChecked = (config.IdentifyModifiers & NativeMethods.MOD_CONTROL) != 0;
        ChkIdentifyShift.IsChecked = (config.IdentifyModifiers & NativeMethods.MOD_SHIFT) != 0;
        ChkIdentifyAlt.IsChecked = (config.IdentifyModifiers & NativeMethods.MOD_ALT) != 0;
        ChkIdentifyWin.IsChecked = (config.IdentifyModifiers & NativeMethods.MOD_WIN) != 0;
        TxtIdentifyKey.Tag = config.IdentifyKey;
        TxtIdentifyKey.Text = AppConfig.FormatKey(config.IdentifyKey);

        // 3. Chụp tức thì từng màn hình (Phím số 1..9 cố định, chỉ đổi modifiers)
        ChkInstantCtrl.IsChecked = (config.InstantModifiers & NativeMethods.MOD_CONTROL) != 0;
        ChkInstantShift.IsChecked = (config.InstantModifiers & NativeMethods.MOD_SHIFT) != 0;
        ChkInstantAlt.IsChecked = (config.InstantModifiers & NativeMethods.MOD_ALT) != 0;
        ChkInstantWin.IsChecked = (config.InstantModifiers & NativeMethods.MOD_WIN) != 0;

        TxtDialogError.Visibility = Visibility.Collapsed;
        OverlayDialogContainer.Visibility = Visibility.Visible;
    }

    public void CloseShortcutsDialog()
    {
        OverlayDialogContainer.Visibility = Visibility.Collapsed;
    }

    private void OverlayDialogContainer_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CloseShortcutsDialog();
    }

    private void DialogCardBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void BtnDialogCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseShortcutsDialog();
    }

    private void HotkeyField_Changed(object? sender, RoutedEventArgs? e)
    {
        if (TxtDialogError != null)
        {
            TxtDialogError.Visibility = Visibility.Collapsed;
        }
    }

    private void KeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
        {
            tb.Text = "...";
            tb.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
        }
    }

    private void KeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
        {
            tb.ClearValue(System.Windows.Controls.Border.BorderBrushProperty);
            if (tb.Tag is uint vk)
            {
                tb.Text = AppConfig.FormatKey(vk);
            }
        }
    }

    private void KeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb) return;

        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

        // Bỏ qua các phím bổ trợ thuần tuý khi nhấn riêng lẻ
        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or
                   System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or
                   System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or
                   System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
        {
            e.Handled = true;
            return;
        }

        if (key == System.Windows.Input.Key.Escape)
        {
            if (tb.Tag is uint oldVk) tb.Text = AppConfig.FormatKey(oldVk);
            System.Windows.Input.Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
        if (vk > 0)
        {
            // Chặn phím số 0..9 trên bàn phím chính (0x30..0x39) và numpad (0x60..0x69)
            if ((vk >= 0x30 && vk <= 0x39) || (vk >= 0x60 && vk <= 0x69))
            {
                if (TxtDialogError != null)
                {
                    TxtDialogError.Text = LocalizationManager.GetString("Loc_Dialog_ErrorNoNumberKeys");
                    TxtDialogError.Visibility = Visibility.Visible;
                }
                if (tb.Tag is uint oldVk)
                {
                    tb.Text = AppConfig.FormatKey(oldVk);
                }
                e.Handled = true;
                return;
            }

            tb.Tag = (uint)vk;
            tb.Text = AppConfig.FormatKey((uint)vk);
            if (TxtDialogError != null) TxtDialogError.Visibility = Visibility.Collapsed;
            System.Windows.Input.Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void BtnDialogReset_Click(object sender, RoutedEventArgs e)
    {
        // Mặc định: Ctrl + Shift + X, Ctrl + Shift + ~, Ctrl + Shift + 1..9
        ChkOverlayCtrl.IsChecked = true;
        ChkOverlayShift.IsChecked = true;
        ChkOverlayAlt.IsChecked = false;
        ChkOverlayWin.IsChecked = false;
        TxtOverlayKey.Tag = (uint)0x58;
        TxtOverlayKey.Text = AppConfig.FormatKey(0x58);

        ChkIdentifyCtrl.IsChecked = true;
        ChkIdentifyShift.IsChecked = true;
        ChkIdentifyAlt.IsChecked = false;
        ChkIdentifyWin.IsChecked = false;
        TxtIdentifyKey.Tag = (uint)0xC0;
        TxtIdentifyKey.Text = AppConfig.FormatKey(0xC0);

        ChkInstantCtrl.IsChecked = true;
        ChkInstantShift.IsChecked = true;
        ChkInstantAlt.IsChecked = false;
        ChkInstantWin.IsChecked = false;

        TxtDialogError.Visibility = Visibility.Collapsed;
    }

    private void BtnDialogSave_Click(object sender, RoutedEventArgs e)
    {
        uint overlayMods = GetModifiersFromCheckboxes(ChkOverlayCtrl, ChkOverlayShift, ChkOverlayAlt, ChkOverlayWin);
        uint overlayKey = TxtOverlayKey.Tag is uint ok ? ok : (uint)0x58;

        uint identifyMods = GetModifiersFromCheckboxes(ChkIdentifyCtrl, ChkIdentifyShift, ChkIdentifyAlt, ChkIdentifyWin);
        uint identifyKey = TxtIdentifyKey.Tag is uint ik ? ik : (uint)0xC0;

        uint instantMods = GetModifiersFromCheckboxes(ChkInstantCtrl, ChkInstantShift, ChkInstantAlt, ChkInstantWin);

        // Kiểm tra hợp lệ: Bắt buộc phải có ít nhất 1 phím bổ trợ
        if (overlayMods == 0 || identifyMods == 0 || instantMods == 0)
        {
            TxtDialogError.Text = LocalizationManager.GetString("Loc_Dialog_ErrorNoModifier");
            TxtDialogError.Visibility = Visibility.Visible;
            return;
        }

        // Kiểm tra không được chọn phím số cho Overlay hoặc Identify
        if ((overlayKey >= 0x30 && overlayKey <= 0x39) || (overlayKey >= 0x60 && overlayKey <= 0x69) ||
            (identifyKey >= 0x30 && identifyKey <= 0x39) || (identifyKey >= 0x60 && identifyKey <= 0x69))
        {
            TxtDialogError.Text = LocalizationManager.GetString("Loc_Dialog_ErrorNoNumberKeys");
            TxtDialogError.Visibility = Visibility.Visible;
            return;
        }

        // Kiểm tra xung đột giữa Overlay và Identify
        if (overlayMods == identifyMods && overlayKey == identifyKey)
        {
            TxtDialogError.Text = LocalizationManager.GetString("Loc_Dialog_ErrorConflict");
            TxtDialogError.Visibility = Visibility.Visible;
            return;
        }

        var config = AppConfig.Instance;
        config.CaptureModifiers = overlayMods;
        config.CaptureKey = overlayKey;
        config.IdentifyModifiers = identifyMods;
        config.IdentifyKey = identifyKey;
        config.InstantModifiers = instantMods;
        config.Save();

        // Đăng ký lại Hotkeys toàn hệ thống ngay lập tức
        _hotkeyManager.ReRegister(ScreenCapturer.GetMonitors().Count);

        // Cập nhật tab Phím tắt
        _shortcutsPage.RefreshShortcuts();

        CloseShortcutsDialog();
    }

    private static uint GetModifiersFromCheckboxes(System.Windows.Controls.CheckBox ctrl, System.Windows.Controls.CheckBox shift, System.Windows.Controls.CheckBox alt, System.Windows.Controls.CheckBox win)
    {
        uint mods = 0;
        if (ctrl.IsChecked == true) mods |= NativeMethods.MOD_CONTROL;
        if (shift.IsChecked == true) mods |= NativeMethods.MOD_SHIFT;
        if (alt.IsChecked == true) mods |= NativeMethods.MOD_ALT;
        if (win.IsChecked == true) mods |= NativeMethods.MOD_WIN;
        return mods;
    }

    #endregion
}
