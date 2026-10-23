using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LMScreenShot.Views.Pages;

public partial class GalleryPage : Page
{
    private string _screenshotDirectory = string.Empty;

    private CancellationTokenSource? _loadCts;

    public GalleryPage()
    {
        InitializeComponent();
        Loaded += GalleryPage_Loaded;
        Unloaded += GalleryPage_Unloaded;
    }

    private void GalleryPage_Loaded(object sender, RoutedEventArgs e)
    {
        _screenshotDirectory = LMScreenShot.Core.Config.AppConfig.Instance.ResolvedSavePath;

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadGalleryAsync(_loadCts.Token);
    }

    private void GalleryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
    }

    private void ScrollGallery_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        AdjustThumbnailSizes();
    }

    private void AdjustThumbnailSizes()
    {
        if (ScrollGallery == null || PanelGallery == null) return;

        double availWidth = ScrollGallery.ActualWidth;
        if (availWidth <= 0) return;

        // Trừ khoảng trống biên để không xuất hiện thanh cuộn ngang
        double usableWidth = availWidth - 16;
        if (usableWidth < 180) return;

        // Tính số cột tự động dàn trải đều từ trái sang phải
        int cols = Math.Max(1, (int)(usableWidth / 230));

        double marginTotalPerCol = 12.0;
        double totalMargins = cols * marginTotalPerCol;
        double itemWidth = Math.Max(160, Math.Floor((usableWidth - totalMargins) / cols));
        double itemHeight = Math.Round(itemWidth * 9.0 / 16.0); // Tỉ lệ 16:9 chuẩn màn hình

        foreach (UIElement child in PanelGallery.Children)
        {
            if (child is System.Windows.Controls.Border border)
            {
                border.Width = itemWidth;
                border.Height = itemHeight;
                border.Margin = new Thickness(6);
            }
        }
    }

    private async Task LoadGalleryAsync(CancellationToken token)
    {
        PanelGallery.Children.Clear();

        if (!Directory.Exists(_screenshotDirectory)) return;

        // 1. Quét danh sách file ở Background Thread (không chặn luồng UI)
        var files = await Task.Run(() =>
        {
            try
            {
                return Directory.GetFiles(_screenshotDirectory, "*.png")
                    .Select(f => new FileInfo(f))
                    // Đảm bảo 100% ảnh mới nhất luôn đứng đầu:
                    // 1) Theo thời gian mới nhất (Max giữa LastWriteTime và CreationTime)
                    // 2) Kết hợp tên file Screenshot_yyyyMMdd_HHmmss.png giảm dần
                    .OrderByDescending(f => Math.Max(f.LastWriteTimeUtc.Ticks, f.CreationTimeUtc.Ticks))
                    .ThenByDescending(f => f.Name)
                    .Select(f => f.FullName)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }, token);

        if (token.IsCancellationRequested || files.Count == 0) return;

        // 2. Tải cuốn chiếu theo lô (Batch Progressive Loading):
        // Lô đầu tiên tải ngay 12 ảnh (~50ms) để lấp đầy màn hình tức thì, các lô sau nạp tiếp theo
        int processed = 0;
        while (processed < files.Count)
        {
            if (token.IsCancellationRequested) return;

            int batchSize = processed == 0 ? 12 : 16;
            var batchFiles = files.Skip(processed).Take(batchSize).ToList();
            processed += batchFiles.Count;

            // Giải mã ảnh nén ở luồng nền (Task.Run) và Freeze() để gửi sang UI thread
            var batchItems = await Task.Run(() =>
            {
                var list = new List<(string FilePath, BitmapSource Bitmap)>();
                foreach (var file in batchFiles)
                {
                    if (token.IsCancellationRequested) break;
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(file);
                        bitmap.DecodePixelWidth = 260; // Thumbnail nhẹ, sắc nét
                        bitmap.EndInit();
                        bitmap.Freeze(); // Cho phép chia sẻ qua thread an toàn
                        list.Add((file, bitmap));
                    }
                    catch { }
                }
                return list;
            }, token);

            if (token.IsCancellationRequested) return;

            // Thêm các thẻ ảnh vào giao diện
            foreach (var item in batchItems)
            {
                var border = CreateThumbnailCard(item.FilePath, item.Bitmap);
                PanelGallery.Children.Add(border);
            }

            AdjustThumbnailSizes();

            // Nhường CPU để UI cập nhật mượt mà 60/120 FPS
            await Task.Yield();
        }
    }

    private System.Windows.Controls.Border CreateThumbnailCard(string file, BitmapSource bitmap)
    {
        string fileName = Path.GetFileName(file);

        var border = new System.Windows.Controls.Border
        {
            Width = 220,
            Height = 124,
            Margin = new Thickness(6),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Cursor = Cursors.Hand,
            ToolTip = $"{fileName}\n• {LMScreenShot.Core.Localization.LocalizationManager.GetString("Loc_Gallery_Tooltip")}"
        };
        border.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "CardBackgroundFillColorSecondaryBrush");
        border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");

        border.MouseEnter += (s, e) =>
        {
            border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
        };
        border.MouseLeave += (s, e) =>
        {
            border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        };

        var img = new System.Windows.Controls.Image
        {
            Stretch = Stretch.UniformToFill,
            Source = bitmap
        };
        border.Child = img;

        Point dragStartPoint = new();

        // 1. Cơ chế Bấm đúp (Double-click) để mở ảnh
        border.PreviewMouseLeftButtonDown += (s, e) =>
        {
            dragStartPoint = e.GetPosition(null);

            if (e.ClickCount == 2)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                }
                catch { }
                e.Handled = true;
            }
        };

        // 2. Cơ chế Kéo thả (Drag & Drop) sang Desktop, File Explorer hoặc các app khác
        border.PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(null);
                Vector diff = dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var dataObject = new DataObject();
                    dataObject.SetData(DataFormats.FileDrop, new string[] { file });
                    DragDrop.DoDragDrop(border, dataObject, DragDropEffects.Copy);
                }
            }
        };

        return border;
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_screenshotDirectory))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", _screenshotDirectory) { UseShellExecute = true });
        }
    }
}
