using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LMScreenShot.Core.Capture;
using LMScreenShot.Core.Native;
using LMScreenShot.Views.Windows;
using TextBlock = System.Windows.Controls.TextBlock;

namespace LMScreenShot.Views.Pages;

public partial class HomePage : Page
{
    private List<MonitorItem> _monitors = new();
    private string _screenshotDirectory = string.Empty;
    private string? _latestScreenshotPath = null;

    public HomePage()
    {
        InitializeComponent();
        Loaded += HomePage_Loaded;
        LMScreenShot.Core.Localization.LocalizationManager.LanguageChanged += () =>
        {
            RefreshMonitors();
        };
    }

    private void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        var config = LMScreenShot.Core.Config.AppConfig.Instance;
        ToggleAutoSave.IsChecked = config.AutoSave == 1;
        ToggleAutoClipboard.IsChecked = config.AutoClipboard == 1;

        _screenshotDirectory = config.ResolvedSavePath;

        if (!Directory.Exists(_screenshotDirectory))
        {
            Directory.CreateDirectory(_screenshotDirectory);
        }

        RefreshMonitors();
    }

    private void ToggleAutoSave_Click(object sender, RoutedEventArgs e)
    {
        LMScreenShot.Core.Config.AppConfig.Instance.AutoSave = ToggleAutoSave.IsChecked == true ? 1 : 0;
        LMScreenShot.Core.Config.AppConfig.Instance.Save();
    }

    private void ToggleAutoClipboard_Click(object sender, RoutedEventArgs e)
    {
        LMScreenShot.Core.Config.AppConfig.Instance.AutoClipboard = ToggleAutoClipboard.IsChecked == true ? 1 : 0;
        LMScreenShot.Core.Config.AppConfig.Instance.Save();
    }

    public void RefreshMonitors()
    {
        _monitors = ScreenCapturer.GetMonitors();
        TxtMonitorSummary.Text = LMScreenShot.Core.Localization.LocalizationManager.GetString("Loc_Home_DetectedScreens", _monitors.Count);
        RenderTopologyMap();
    }

    private void CanvasMonitors_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderTopologyMap();
    }

    private void RenderTopologyMap()
    {
        if (_monitors.Count == 0 || CanvasMonitors == null) return;

        CanvasMonitors.Children.Clear();

        double canvasWidth = CanvasMonitors.ActualWidth > 0 ? CanvasMonitors.ActualWidth : 480;
        double canvasHeight = CanvasMonitors.ActualHeight > 0 ? CanvasMonitors.ActualHeight : 300;

        int minX = _monitors.Min(m => m.Left);
        int minY = _monitors.Min(m => m.Top);
        int maxX = _monitors.Max(m => m.Right);
        int maxY = _monitors.Max(m => m.Bottom);

        double totalWidth = maxX - minX;
        double totalHeight = maxY - minY;

        if (totalWidth <= 0 || totalHeight <= 0) return;

        double padding = 20.0;
        double availWidth = Math.Max(100, canvasWidth - padding * 2);
        double availHeight = Math.Max(100, canvasHeight - padding * 2);

        double scaleX = availWidth / totalWidth;
        double scaleY = availHeight / totalHeight;
        double scale = Math.Min(scaleX, scaleY);

        double scaledWidth = totalWidth * scale;
        double scaledHeight = totalHeight * scale;

        double offsetX = padding + (availWidth - scaledWidth) / 2.0;
        double offsetY = padding + (availHeight - scaledHeight) / 2.0;

        foreach (var monitor in _monitors)
        {
            double left = offsetX + (monitor.Left - minX) * scale;
            double top = offsetY + (monitor.Top - minY) * scale;
            double width = Math.Max(70, monitor.Width * scale);
            double height = Math.Max(50, monitor.Height * scale);

            var card = CreateMonitorCard(monitor, width, height);
            Canvas.SetLeft(card, left);
            Canvas.SetTop(card, top);
            CanvasMonitors.Children.Add(card);
        }
    }

    private FrameworkElement CreateMonitorCard(MonitorItem monitor, double width, double height)
    {
        // TẮT HIỆU ỨNG MICA: Sử dụng màu nền lì (Solid) không xuyên thấu theo chuẩn ảnh thực tế
        bool isLight = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme() == Wpf.Ui.Appearance.ApplicationTheme.Light;
        var normalBg = isLight 
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)) 
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 43, 43));
        var hoverBg = isLight 
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 225, 225)) 
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 56, 56));
        var normalBorder = isLight 
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 210, 210)) 
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 58));
        var textBrush = isLight 
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)) 
            : System.Windows.Media.Brushes.White;

        var border = new System.Windows.Controls.Border
        {
            Width = width,
            Height = height,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            Tag = monitor,
            Background = normalBg,
            BorderBrush = normalBorder,
            ToolTip = $"{monitor.DisplayTitle}: {monitor.FriendlyName} ({monitor.Width}×{monitor.Height} @ {monitor.RefreshRate}Hz - {monitor.ScalePercent}%)"
        };

        // Chữ số màn hình nằm chính giữa khung hiển thị (cả ngang và dọc)
        var numberText = new TextBlock
        {
            Text = monitor.DisplayNumber.ToString(),
            FontSize = Math.Max(20, Math.Min(34, height * 0.36)),
            FontWeight = FontWeights.Medium,
            Foreground = textBrush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };

        border.Child = numberText;

        border.MouseEnter += (s, e) =>
        {
            border.Background = hoverBg;
            border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
        };

        border.MouseLeave += (s, e) =>
        {
            border.Background = normalBg;
            border.BorderBrush = normalBorder;
        };

        border.MouseDown += async (s, e) =>
        {
            if (border.Tag is MonitorItem targetMonitor)
            {
                await ExecuteCaptureAsync(targetMonitor);
            }
        };

        return border;
    }

    private void ShowSnackbarNotification(string title, string message, bool isSuccess = true)
    {
        var snackbar = new Wpf.Ui.Controls.Snackbar(RootSnackbarPresenter)
        {
            Title = title,
            Content = message,
            Timeout = TimeSpan.FromSeconds(2),
            Icon = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = isSuccess ? Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24 : Wpf.Ui.Controls.SymbolRegular.ErrorCircle24
            }
        };
        snackbar.Show();
    }

    public async Task ExecuteCaptureAsync(MonitorItem targetMonitor)
    {
        var window = Window.GetWindow(this) ?? Application.Current.MainWindow;
        nint hWnd = window != null ? new System.Windows.Interop.WindowInteropHelper(window).Handle : nint.Zero;

        // Nếu window đang ẩn (ví dụ do mở từ Capture Overlay), không cần ẩn thêm
        bool wasHidden = window != null && window.Visibility != Visibility.Visible;
        bool isAppOnTargetMonitor = window != null && !wasHidden && ScreenCapturer.IsWindowOnMonitor(hWnd, targetMonitor);

        try
        {
            if (isAppOnTargetMonitor)
            {
                window!.Hide();
                await Task.Delay(160);
            }
            else if (wasHidden)
            {
                // Cho một khoảng trễ cực nhỏ để overlay giải phóng DWM trước khi chụp
                await Task.Delay(50);
            }

            var capturedBitmap = ScreenCapturer.CaptureMonitor(targetMonitor);

            // Hiện lại cửa sổ và đưa lên Foreground nếu trước đó đang mở
            if (window != null && !wasHidden)
            {
                window.Show();
                window.Activate();
            }

            if (ToggleAutoClipboard.IsChecked == true)
            {
                CopyImageToClipboard(capturedBitmap);
            }

            string savedPath = string.Empty;
            if (ToggleAutoSave.IsChecked == true)
            {
                savedPath = AutoSaveImage(capturedBitmap);
                _latestScreenshotPath = savedPath;
            }

            string statusMsg;
            if (ToggleAutoClipboard.IsChecked == true && !string.IsNullOrEmpty(savedPath))
                statusMsg = LMScreenShot.Core.Localization.LocalizationManager.GetString("Loc_Toast_SavedAndCopied");
            else if (!string.IsNullOrEmpty(savedPath))
                statusMsg = LMScreenShot.Core.Localization.LocalizationManager.GetString("Loc_Toast_SavedOnly");
            else if (ToggleAutoClipboard.IsChecked == true)
                statusMsg = LMScreenShot.Core.Localization.LocalizationManager.GetString("Loc_Toast_CopiedOnly");
            else
                statusMsg = $"{capturedBitmap.PixelWidth}×{capturedBitmap.PixelHeight} px";

            string capturedTitle = LMScreenShot.Core.Localization.LocalizationManager.GetString("Loc_Toast_Captured", targetMonitor.DisplayTitle);
            ShowOnScreenNotification(targetMonitor, capturedTitle, statusMsg);
            ShowSnackbarNotification(capturedTitle, statusMsg, isSuccess: true);
        }
        catch (Exception ex)
        {
            if (window != null)
            {
                window.Show();
                window.Activate();
            }
            ShowSnackbarNotification(LMScreenShot.Core.Localization.LocalizationManager.GetString("Loc_Toast_CaptureError"), ex.Message, isSuccess: false);
        }
    }

    private void ShowOnScreenNotification(MonitorItem targetMonitor, string title, string message)
    {
        try
        {
            var notif = new ScreenOverlayWindow(targetMonitor, OverlayMode.Notification, title, message);
            notif.Show();
        }
        catch { }
    }

    private void CardNewCapture_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Khi bấm vào nút "Ảnh chụp mới", gọi Overlay tương tác giống hệt Ctrl + Shift + X
        (Application.Current.MainWindow as MainWindow)?.OpenCaptureOverlay();
    }

    private void CopyImageToClipboard(BitmapSource image)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                Clipboard.SetImage(image);
                break;
            }
            catch
            {
                Thread.Sleep(50);
            }
        }
    }

    private string AutoSaveImage(BitmapSource image)
    {
        try
        {
            string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(_screenshotDirectory, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);

            return fullPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
