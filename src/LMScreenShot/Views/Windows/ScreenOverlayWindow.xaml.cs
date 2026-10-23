using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LMScreenShot.Core.Native;

namespace LMScreenShot.Views.Windows;

public enum OverlayMode
{
    Capture,
    Identify,
    Notification
}

public partial class ScreenOverlayWindow : Window
{
    public MonitorItem Monitor { get; }
    public OverlayMode Mode { get; }

    public event Action<MonitorItem>? CaptureTriggered;
    public event Action<int>? NumberKeyTriggered;
    public event Action? CancelTriggered;

    private DispatcherTimer? _autoCloseTimer;

    public ScreenOverlayWindow(MonitorItem monitor, OverlayMode mode, string? notifTitle = null, string? notifSub = null)
    {
        InitializeComponent();

        Monitor = monitor;
        Mode = mode;

        TxtNumber.Text = monitor.DisplayNumber.ToString();

        // Định vị vị trí ban đầu
        Left = monitor.Left;
        Top = monitor.Top;
        Width = monitor.Width;
        Height = monitor.Height;

        Loaded += ScreenOverlayWindow_Loaded;
        SourceInitialized += ScreenOverlayWindow_SourceInitialized;

        if (Mode == OverlayMode.Capture)
        {
            KeyDown += ScreenOverlayWindow_KeyDown;
        }
        else if (Mode == OverlayMode.Identify)
        {
            // Chế độ Identify: Nền hoàn toàn trong suốt, chỉ hiện ô vuông số ở góc
            RootGrid.Background = Brushes.Transparent;
            RootGrid.Cursor = Cursors.Arrow;
            TipBadge.Visibility = Visibility.Collapsed;
        }
        else if (Mode == OverlayMode.Notification)
        {
            // Chế độ Notification: Nền trong suốt, ẩn ô số và tip, hiện Toast ở giữa dưới
            RootGrid.Background = Brushes.Transparent;
            RootGrid.Cursor = Cursors.Arrow;
            NumberBadge.Visibility = Visibility.Collapsed;
            TipBadge.Visibility = Visibility.Collapsed;
            NotificationBadge.Visibility = Visibility.Visible;

            if (!string.IsNullOrEmpty(notifTitle))
                TxtNotificationTitle.Text = notifTitle;
            if (!string.IsNullOrEmpty(notifSub))
                TxtNotificationSub.Text = notifSub;
        }
    }

    private void ScreenOverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        nint hWnd = helper.Handle;

        if (hWnd != nint.Zero)
        {
            // Đảm bảo cửa sổ phủ đúng 1:1 pixel vật lý trên màn hình bất kể DPI
            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOPMOST,
                Monitor.Left,
                Monitor.Top,
                Monitor.Width,
                Monitor.Height,
                NativeMethods.SWP_SHOWWINDOW);

            if (Mode == OverlayMode.Identify || Mode == OverlayMode.Notification)
            {
                // Cho phép click chuột xuyên thấu (không chặn tương tác ứng dụng khác)
                int exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLong(
                    hWnd,
                    NativeMethods.GWL_EXSTYLE,
                    exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
            }
        }
    }

    private void ScreenOverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (Mode == OverlayMode.Identify)
        {
            // Tự động mờ dần và tắt sau 2.2 giây
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.2)
            };
            _autoCloseTimer.Tick += (s, args) =>
            {
                _autoCloseTimer.Stop();

                var fadeAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                fadeAnim.Completed += (a, b) => Close();
                BeginAnimation(OpacityProperty, fadeAnim);
            };
            _autoCloseTimer.Start();
        }
        else if (Mode == OverlayMode.Notification)
        {
            // Hiệu ứng trượt nhẹ lên và fade in
            NotificationTransform.Y = 16;
            NotificationBadge.Opacity = 0;

            var slideIn = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));

            NotificationTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            NotificationBadge.BeginAnimation(OpacityProperty, fadeIn);

            // Tự động mờ dần và tắt sau 1.8 giây
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.8)
            };
            _autoCloseTimer.Tick += (s, args) =>
            {
                _autoCloseTimer.Stop();

                var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                fadeAnim.Completed += (a, b) => Close();
                BeginAnimation(OpacityProperty, fadeAnim);
            };
            _autoCloseTimer.Start();
        }
        else
        {
            Focus();
            Activate();
        }
    }

    private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Mode == OverlayMode.Capture)
        {
            CaptureTriggered?.Invoke(Monitor);
        }
    }

    private void ScreenOverlayWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (Mode != OverlayMode.Capture) return;

        if (e.Key == Key.Escape)
        {
            CancelTriggered?.Invoke();
            e.Handled = true;
            return;
        }

        // Bắt phím số 1..9 (hàng phím số trên)
        if (e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            int number = e.Key - Key.D1 + 1;
            NumberKeyTriggered?.Invoke(number);
            e.Handled = true;
            return;
        }

        // Bắt phím số 1..9 (bàn phím phụ NumPad)
        if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
        {
            int number = e.Key - Key.NumPad1 + 1;
            NumberKeyTriggered?.Invoke(number);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer?.Stop();
        base.OnClosed(e);
    }
}
