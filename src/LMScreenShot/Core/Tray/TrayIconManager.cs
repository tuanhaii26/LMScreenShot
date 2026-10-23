using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using LMScreenShot.Core.Localization;
using LMScreenShot.Core.Native;

namespace LMScreenShot.Core.Tray;

public class TrayIconManager : IDisposable
{
    private const int TRAY_ICON_ID = 1001;

    private MainWindow? _mainWindow;
    private nint _hWnd = nint.Zero;
    private HwndSource? _source;
    private nint _hIcon = nint.Zero;
    private bool _isIconCreated = false;
    private bool _isAdded = false;
    private ContextMenu? _contextMenu;

    public void Initialize(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        var helper = new WindowInteropHelper(mainWindow);
        _hWnd = helper.Handle;

        if (_hWnd == nint.Zero) return;

        _source = HwndSource.FromHwnd(_hWnd);
        _source?.AddHook(HwndHook);

        AddOrUpdateTrayIcon();

        LocalizationManager.LanguageChanged += OnLanguageChanged;
    }

    private void AddOrUpdateTrayIcon()
    {
        if (_hWnd == nint.Zero) return;

        if (_hIcon == nint.Zero)
        {
            _hIcon = LoadAppIcon();
        }

        string tip = LocalizationManager.GetString("Loc_Tray_Tooltip");
        if (tip.Length > 120) tip = tip.Substring(0, 120);

        var data = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = TRAY_ICON_ID,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = _hIcon,
            szTip = tip
        };

        if (!_isAdded)
        {
            _isAdded = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
        }
        else
        {
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
        }
    }

    private void OnLanguageChanged()
    {
        AddOrUpdateTrayIcon();
        _contextMenu = null;
    }

    private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_TRAYICON)
        {
            int iconId = wParam.ToInt32();
            if (iconId == TRAY_ICON_ID)
            {
                int mouseMsg = lParam.ToInt32();

                if (mouseMsg == NativeMethods.WM_LBUTTONUP)
                {
                    // Single click chuột trái -> Mở trình chụp tương tác Ctrl + Shift + X
                    _mainWindow?.Dispatcher.Invoke(() =>
                    {
                        _mainWindow.OpenCaptureOverlay();
                    });
                    handled = true;
                }
                else if (mouseMsg == NativeMethods.WM_RBUTTONUP)
                {
                    // Chuột phải -> Hiện menu với 2 tuỳ chọn: Mở LMScreenShot và Thoát
                    ShowContextMenu();
                    handled = true;
                }
                else if (mouseMsg == NativeMethods.WM_LBUTTONDBLCLK)
                {
                    _mainWindow?.Dispatcher.Invoke(() =>
                    {
                        _mainWindow.ShowAndRestore();
                    });
                    handled = true;
                }
            }
        }

        return nint.Zero;
    }

    private void ShowContextMenu()
    {
        if (_mainWindow == null) return;

        _mainWindow.Dispatcher.Invoke(() =>
        {
            if (_contextMenu == null)
            {
                _contextMenu = new ContextMenu
                {
                    Placement = PlacementMode.MousePoint
                };

                var itemOpen = new MenuItem
                {
                    Header = LocalizationManager.GetString("Loc_Tray_Open"),
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.AppGeneric24 }
                };
                itemOpen.Click += (s, e) => _mainWindow.ShowAndRestore();

                var itemExit = new MenuItem
                {
                    Header = LocalizationManager.GetString("Loc_Tray_Exit"),
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Dismiss24 }
                };
                itemExit.Click += (s, e) => _mainWindow.ExitApplication();

                _contextMenu.Items.Add(itemOpen);
                _contextMenu.Items.Add(new Separator());
                _contextMenu.Items.Add(itemExit);
            }
            else
            {
                if (_contextMenu.Items.Count >= 3)
                {
                    if (_contextMenu.Items[0] is MenuItem openItem)
                        openItem.Header = LocalizationManager.GetString("Loc_Tray_Open");
                    if (_contextMenu.Items[2] is MenuItem exitItem)
                        exitItem.Header = LocalizationManager.GetString("Loc_Tray_Exit");
                }
            }

            NativeMethods.SetForegroundWindow(_hWnd);
            _contextMenu.IsOpen = true;
        });
    }

    private nint LoadAppIcon()
    {
        try
        {
            string[] candidatePaths = [
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Resources\app.ico"),
                @"D:\LMScreenShot\src\LMScreenShot\Resources\app.ico",
                @"D:\LMScreenShot\Untitled.ico"
            ];

            foreach (var icoPath in candidatePaths)
            {
                if (File.Exists(icoPath))
                {
                    nint h = NativeMethods.LoadImage(nint.Zero, Path.GetFullPath(icoPath), 1 /* IMAGE_ICON */, 0, 0, 0x0010 /* LR_LOADFROMFILE */);
                    if (h != nint.Zero) { _isIconCreated = true; return h; }
                }
            }

            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                if (NativeMethods.ExtractIconEx(exePath, 0, out nint _, out nint hSmall, 1) > 0 && hSmall != nint.Zero)
                {
                    _isIconCreated = true;
                    return hSmall;
                }
            }
        }
        catch { }

        // Fallback default Windows Application Icon
        return NativeMethods.LoadIcon(nint.Zero, (nint)32512 /* IDI_APPLICATION */);
    }

    public void Dispose()
    {
        LocalizationManager.LanguageChanged -= OnLanguageChanged;

        if (_isAdded && _hWnd != nint.Zero)
        {
            var data = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = TRAY_ICON_ID
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
            _isAdded = false;
        }

        if (_source != null)
        {
            _source.RemoveHook(HwndHook);
            _source = null;
        }

        if (_isIconCreated && _hIcon != nint.Zero)
        {
            NativeMethods.DestroyIcon(_hIcon);
            _hIcon = nint.Zero;
        }
    }
}
