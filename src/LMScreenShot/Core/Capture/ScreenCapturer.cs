using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using LMScreenShot.Core.Native;

namespace LMScreenShot.Core.Capture;

public static class ScreenCapturer
{
    /// <summary>
    /// Lấy danh sách tất cả các màn hình đang kết nối kèm thông số vật lý và DPI.
    /// </summary>
    public static List<MonitorItem> GetMonitors()
    {
        var monitors = new List<MonitorItem>();
        int index = 0;

        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (nint hMonitor, nint hdcMonitor, ref NativeMethods.RECT lprcMonitor, nint dwData) =>
        {
            var info = new NativeMethods.MONITORINFOEX();
            info.Init();

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                uint dpiX = 96;
                uint dpiY = 96;

                try
                {
                    _ = NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MonitorDpiType.EffectiveDpi, out dpiX, out dpiY);
                }
                catch
                {
                    dpiX = 96;
                    dpiY = 96;
                }

                int refreshRate = 60;
                try
                {
                    var devMode = new NativeMethods.DEVMODE();
                    devMode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DEVMODE>();
                    if (NativeMethods.EnumDisplaySettings(info.szDevice, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        if (devMode.dmDisplayFrequency > 0)
                        {
                            refreshRate = devMode.dmDisplayFrequency;
                        }
                    }
                }
                catch { }

                string friendlyName = "Display";
                try
                {
                    var displayDevice = new NativeMethods.DISPLAY_DEVICE();
                    displayDevice.cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>();
                    if (NativeMethods.EnumDisplayDevices(info.szDevice, 0, ref displayDevice, 0) &&
                        !string.IsNullOrWhiteSpace(displayDevice.DeviceString))
                    {
                        friendlyName = displayDevice.DeviceString;
                        // Rút gọn các tên quá dài như "Generic PnP Monitor"
                        if (friendlyName.Contains("Generic PnP", StringComparison.OrdinalIgnoreCase))
                        {
                            friendlyName = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0 ? "Laptop Display" : $"Màn hình {index + 1}";
                        }
                    }
                    else
                    {
                        friendlyName = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0 ? "Laptop Display" : $"Màn hình {index + 1}";
                    }
                }
                catch 
                {
                    friendlyName = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0 ? "Laptop Display" : $"Màn hình {index + 1}";
                }

                var item = new MonitorItem
                {
                    Index = index++,
                    Handle = hMonitor,
                    DeviceName = info.szDevice,
                    FriendlyName = friendlyName,
                    RefreshRate = refreshRate,
                    Bounds = info.rcMonitor,
                    WorkArea = info.rcWork,
                    IsPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                    DpiX = dpiX,
                    DpiY = dpiY
                };

                monitors.Add(item);
            }

            return true;
        }, nint.Zero);

        // Sắp xếp: Màn hình chính lên đầu, sau đó sắp xếp theo tọa độ X từ trái qua phải
        return monitors
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Left)
            .ToList();
    }

    /// <summary>
    /// Tìm màn hình mà con trỏ chuột hiện đang đứng trên đó.
    /// </summary>
    public static MonitorItem? GetCurrentMonitor()
    {
        if (!NativeMethods.GetCursorPos(out var cursorPos))
        {
            return GetMonitors().FirstOrDefault(m => m.IsPrimary) ?? GetMonitors().FirstOrDefault();
        }

        nint hMonitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.Handle == hMonitor) ?? monitors.FirstOrDefault();
    }

    /// <summary>
    /// Kiểm tra xem cửa sổ có đang hiển thị / giao thoa trên màn hình chỉ định hay không.
    /// </summary>
    public static bool IsWindowOnMonitor(nint hWnd, MonitorItem monitor)
    {
        if (hWnd == nint.Zero || monitor == null) return false;

        if (NativeMethods.GetWindowRect(hWnd, out var winRect))
        {
            // Kiểm tra xem hình chữ nhật cửa sổ có giao nhau với màn hình không
            bool intersects = !(winRect.Right <= monitor.Left ||
                                winRect.Left >= monitor.Right ||
                                winRect.Bottom <= monitor.Top ||
                                winRect.Top >= monitor.Bottom);
            return intersects;
        }

        nint hMonitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return hMonitor == monitor.Handle;
    }

    /// <summary>
    /// Chụp toàn bộ màn hình chỉ định (bao gồm đầy đủ Taskbar) chuẩn 1:1 pixel vật lý.
    /// </summary>
    public static BitmapSource CaptureMonitor(MonitorItem monitor)
    {
        return CaptureRect(monitor.Left, monitor.Top, monitor.Width, monitor.Height);
    }

    /// <summary>
    /// Chụp nhanh màn hình mà con trỏ chuột đang đứng.
    /// </summary>
    public static BitmapSource? CaptureCurrentMonitor(out MonitorItem? capturedMonitor)
    {
        capturedMonitor = GetCurrentMonitor();
        if (capturedMonitor == null) return null;

        return CaptureMonitor(capturedMonitor);
    }

    /// <summary>
    /// Chụp một vùng tọa độ vật lý bất kỳ (đảm bảo giải phóng GDI ngay lập tức).
    /// </summary>
    public static BitmapSource CaptureRect(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Kích thước vùng chụp không hợp lệ.");
        }

        nint hdcScreen = NativeMethods.GetDC(nint.Zero);
        nint hdcMemory = NativeMethods.CreateCompatibleDC(hdcScreen);
        nint hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, width, height);
        nint hOldBitmap = NativeMethods.SelectObject(hdcMemory, hBitmap);

        try
        {
            // SRCCOPY | CAPTUREBLT để chụp được cả layered window, hiệu ứng mờ Acrylic/Taskbar
            int rop = NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT;
            bool success = NativeMethods.BitBlt(hdcMemory, 0, 0, width, height, hdcScreen, x, y, rop);

            if (!success)
            {
                throw new InvalidOperationException("BitBlt thất bại khi chụp màn hình.");
            }

            // Chuyển sang BitmapSource của WPF
            BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                nint.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Đóng băng (Freeze) để có thể truyền giữa các Thread an toàn
            source.Freeze();
            return source;
        }
        finally
        {
            // DỌN DẸP GDI CHỐNG RÒ RỈ BỘ NHỚ
            NativeMethods.SelectObject(hdcMemory, hOldBitmap);
            NativeMethods.DeleteObject(hBitmap);
            NativeMethods.DeleteDC(hdcMemory);
            NativeMethods.ReleaseDC(nint.Zero, hdcScreen);
        }
    }
}
