using System.Windows.Interop;
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

    public event Action? CaptureOverlayRequested;
    public event Action? IdentifyOverlayRequested;
    public event Action<int>? InstantCaptureRequested;

    public void Register(nint hWnd, int monitorCount = 9)
    {
        Unregister();

        _hWnd = hWnd;
        if (_hWnd == nint.Zero) return;

        _source = HwndSource.FromHwnd(_hWnd);
        _source?.AddHook(HwndHook);

        uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;

        // 1. Ctrl + Shift + X: Mở giao diện Overlay Chụp tương tác
        if (NativeMethods.RegisterHotKey(_hWnd, HOTKEY_CAPTURE_OVERLAY, modifiers, (uint)NativeMethods.VK_X))
        {
            _registeredIds.Add(HOTKEY_CAPTURE_OVERLAY);
        }

        // 2. Ctrl + Shift + ` : Nhận diện màn hình (Identify)
        if (NativeMethods.RegisterHotKey(_hWnd, HOTKEY_IDENTIFY_OVERLAY, modifiers, (uint)NativeMethods.VK_OEM_3))
        {
            _registeredIds.Add(HOTKEY_IDENTIFY_OVERLAY);
        }

        // 3. Ctrl + Shift + 1..N: Chụp tức thì màn hình tương ứng
        int maxScreens = Math.Min(9, Math.Max(1, monitorCount));
        for (int i = 1; i <= maxScreens; i++)
        {
            int hotkeyId = HOTKEY_INSTANT_BASE + i;
            uint vkKey = (uint)(0x30 + i); // 0x31 là VK_1, 0x32 là VK_2, ...

            if (NativeMethods.RegisterHotKey(_hWnd, hotkeyId, modifiers, vkKey))
            {
                _registeredIds.Add(hotkeyId);
            }
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
