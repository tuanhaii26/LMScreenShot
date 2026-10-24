using System.Windows.Interop;
using LMScreenShot.Core.Config;
using LMScreenShot.Core.Native;

namespace LMScreenShot.Core.Hotkey;

public class GlobalHotkeyManager : IDisposable
{
    private const int HOTKEY_CAPTURE_OVERLAY = 1000;
    private const int HOTKEY_IDENTIFY_OVERLAY = 1001;
    private const int HOTKEY_INSTANT_BASE = 2000;

    private nint _hWnd = nint.Zero;
    private HwndSource? _source;
    private readonly List<int> _registeredIds = new();
    private int _lastMonitorCount = 9;

    public event Action? CaptureOverlayRequested;
    public event Action? IdentifyOverlayRequested;
    public event Action<int>? InstantCaptureRequested;

    public void Register(nint hWnd, int monitorCount = 9)
    {
        Unregister();

        _hWnd = hWnd;
        _lastMonitorCount = monitorCount;
        if (_hWnd == nint.Zero) return;

        _source = HwndSource.FromHwnd(_hWnd);
        _source?.AddHook(HwndHook);

        var config = AppConfig.Instance;

        // 1. Phím tắt chụp tương tác
        uint captureMods = config.CaptureModifiers | NativeMethods.MOD_NOREPEAT;
        uint captureKey = config.CaptureKey;
        if (NativeMethods.RegisterHotKey(_hWnd, HOTKEY_CAPTURE_OVERLAY, captureMods, captureKey))
        {
            _registeredIds.Add(HOTKEY_CAPTURE_OVERLAY);
        }

        // 2. Phím tắt nhận diện màn hình (Identify)
        uint identifyMods = config.IdentifyModifiers | NativeMethods.MOD_NOREPEAT;
        uint identifyKey = config.IdentifyKey;
        if (NativeMethods.RegisterHotKey(_hWnd, HOTKEY_IDENTIFY_OVERLAY, identifyMods, identifyKey))
        {
            _registeredIds.Add(HOTKEY_IDENTIFY_OVERLAY);
        }

        // 3. Phím tắt chụp tức thì màn hình (Giữ nguyên phím số 1..N, chỉ đổi modifiers)
        uint instantMods = config.InstantModifiers | NativeMethods.MOD_NOREPEAT;
        int maxScreens = Math.Min(9, Math.Max(1, monitorCount));
        for (int i = 1; i <= maxScreens; i++)
        {
            int hotkeyId = HOTKEY_INSTANT_BASE + i;
            uint vkKey = (uint)(0x30 + i); // 0x31 là VK_1, 0x32 là VK_2, ...

            if (NativeMethods.RegisterHotKey(_hWnd, hotkeyId, instantMods, vkKey))
            {
                _registeredIds.Add(hotkeyId);
            }
        }
    }

    public void ReRegister(int? monitorCount = null)
    {
        if (_hWnd != nint.Zero)
        {
            nint hwnd = _hWnd;
            Register(hwnd, monitorCount ?? _lastMonitorCount);
        }
    }

    public void Unregister()
    {
        if (_hWnd != nint.Zero)
        {
            foreach (int id in _registeredIds)
            {
                NativeMethods.UnregisterHotKey(_hWnd, id);
            }
            _registeredIds.Clear();
        }

        if (_source != null)
        {
            _source.RemoveHook(HwndHook);
            _source = null;
        }

        _hWnd = nint.Zero;
    }

    private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();

            if (id == HOTKEY_CAPTURE_OVERLAY)
            {
                CaptureOverlayRequested?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_IDENTIFY_OVERLAY)
            {
                IdentifyOverlayRequested?.Invoke();
                handled = true;
            }
            else if (id >= HOTKEY_INSTANT_BASE && id < HOTKEY_INSTANT_BASE + 10)
            {
                int monitorNumber = id - HOTKEY_INSTANT_BASE;
                InstantCaptureRequested?.Invoke(monitorNumber);
                handled = true;
            }
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }
}
