namespace LMScreenShot.Core.Native;

public class MonitorItem
{
    public int Index { get; set; }
    public nint Handle { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public NativeMethods.RECT Bounds { get; set; }
    public NativeMethods.RECT WorkArea { get; set; }
    public bool IsPrimary { get; set; }
    public uint DpiX { get; set; } = 96;
    public uint DpiY { get; set; } = 96;
    public int RefreshRate { get; set; } = 60;
    public string FriendlyName { get; set; } = "Display";

    public int Left => Bounds.Left;
    public int Top => Bounds.Top;
    public int Right => Bounds.Right;
    public int Bottom => Bounds.Bottom;
    public int Width => Bounds.Width;
    public int Height => Bounds.Height;

    public int DisplayNumber => Index + 1;
    public double ScalePercent => Math.Round((DpiX / 96.0) * 100);

    // Chuỗi định dạng hiển thị y hệt mockup: "Laptop Display 1920x1080@144Hz 125%"
    public string FormattedDetail => $"{FriendlyName} {Width}x{Height}@{RefreshRate}Hz {ScalePercent}%";

    public string DisplayTitle => $"Màn hình {DisplayNumber} {(IsPrimary ? "(Chính)" : "")}";

    public string BoundsSummary => $"Vị trí: ({Left}, {Top}) | Kích thước: {Width}x{Height} px";

    // Tọa độ tính toán cho Canvas Topology Layout
    public double LayoutLeft { get; set; }
    public double LayoutTop { get; set; }
    public double LayoutWidth { get; set; }
    public double LayoutHeight { get; set; }
}
