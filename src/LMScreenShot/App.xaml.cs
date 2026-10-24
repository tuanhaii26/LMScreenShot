using System.Windows;
using LMScreenShot.Core.Config;
using LMScreenShot.Core.Utils;

namespace LMScreenShot;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Tự động nạp hoặc khởi tạo config.json ngay khi bật ứng dụng
        var config = AppConfig.Instance;
        StartupManager.ApplyStartup(config.Startup);
        base.OnStartup(e);
    }
}
