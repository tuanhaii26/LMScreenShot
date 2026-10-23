using System.Windows;
using LMScreenShot.Core.Config;

namespace LMScreenShot;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Tự động nạp hoặc khởi tạo config.json ngay khi bật ứng dụng
        _ = AppConfig.Instance;
        base.OnStartup(e);
    }
}
