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

    public GalleryPage()
    {
        InitializeComponent();
        Loaded += GalleryPage_Loaded;
    }

    private void GalleryPage_Loaded(object sender, RoutedEventArgs e)
    {
        _screenshotDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Screenshots");

        LoadGallery();
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

    private void LoadGallery()
    {
        PanelGallery.Children.Clear();

        if (!Directory.Exists(_screenshotDirectory)) return;

        // Hiển thị đầy đủ tất cả ảnh trong thư mục (không giới hạn 16 ảnh)
        var files = Directory.GetFiles(_screenshotDirectory, "*.png")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToList();

        foreach (var file in files)
        {
            var border = new System.Windows.Controls.Border
            {
                Width = 220,
                Height = 124,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                ToolTip = "Bấm đúp để mở • Giữ và kéo thả sang cửa sổ khác để đính kèm hoặc sao chép"
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
                Stretch = Stretch.UniformToFill
            };

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(file);
                bitmap.DecodePixelWidth = 220;
                bitmap.EndInit();
                bitmap.Freeze();
                img.Source = bitmap;
            }
            catch { }

            border.Child = img;

            Point dragStartPoint = new();

            // Ghi nhận vị trí bắt đầu nhấn chuột
            border.PreviewMouseLeftButtonDown += (s, e) =>
            {
                dragStartPoint = e.GetPosition(null);

                // 1. Cơ chế Bấm đúp (Double-click) để mở ảnh
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

            // 2. Cơ chế Kéo thả (Drag & Drop) sang Desktop, File Explorer hoặc các app khác (Zalo, Discord, Web...)
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
                        // Format FileDrop: Cho phép kéo thả vào Desktop, Explorer, Browser, Discord, Telegram...
                        dataObject.SetData(DataFormats.FileDrop, new string[] { file });

                        // Kích hoạt kéo thả OLE chuẩn Windows
                        DragDrop.DoDragDrop(border, dataObject, DragDropEffects.Copy);
                    }
                }
            };

            PanelGallery.Children.Add(border);
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_screenshotDirectory))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", _screenshotDirectory) { UseShellExecute = true });
        }
    }
}
