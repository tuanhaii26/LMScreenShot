using System.Threading;
using System.Windows;
using LMScreenShot.Core.Config;
using LMScreenShot.Core.Native;
using LMScreenShot.Core.Utils;

namespace LMScreenShot;

public partial class App : System.Windows.Application
{
    private const string AppGuid = "LMScreenShot_Unique_Mutex_7A84E32C_2026";
    private static Mutex? _mutex;
    public static readonly uint WM_SHOWME = NativeMethods.RegisterWindowMessage("LMScreenShot_ShowInstance_Message");

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, AppGuid, out bool isNewInstance);

        if (!isNewInstance)
        {
            // Đã có tiến trình LMScreenShot khác đang chạy!
            // Đánh thức và đưa cửa sổ đang chạy lên màn hình
            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, WM_SHOWME, nint.Zero, nint.Zero);

            // Thoát ngay instance khởi động sau này
            Shutdown();
            return;
        }

        // Tự động nạp hoặc khởi tạo config.json ngay khi bật ứng dụng
        var config = AppConfig.Instance;
        StartupManager.ApplyStartup(config.Startup);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch { }
            _mutex.Dispose();
            _mutex = null;
        }
        base.OnExit(e);
    }
}
