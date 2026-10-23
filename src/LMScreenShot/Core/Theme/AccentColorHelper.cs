using System;
using System.Windows.Media;
using Microsoft.Win32;
using LMScreenShot.Core.Native;
using Wpf.Ui.Appearance;

namespace LMScreenShot.Core.Theme;

public static class AccentColorHelper
{
    public static readonly Color FallbackAccentColor = Color.FromRgb(0x00, 0x78, 0xD4);

    public static void Apply(ApplicationTheme theme = ApplicationTheme.Unknown)
    {
        try
        {
            var systemAccent = DetectSystemAccent();
            if (systemAccent.HasValue)
            {
                ApplicationAccentColorManager.Apply(systemAccent.Value, theme);
            }
            else
            {
                ApplicationAccentColorManager.Apply(FallbackAccentColor, theme);
            }
        }
        catch
        {
            try
            {
                ApplicationAccentColorManager.Apply(FallbackAccentColor, theme);
            }
            catch { }
        }
    }

    public static Color? DetectSystemAccent()
    {
        // 1. Kiểm tra qua Windows Registry DWM AccentColor (Độ chính xác cao nhất trên Windows 10/11)
        try
        {
            using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (dwmKey?.GetValue("AccentColor") is int rawColor)
            {
                // DWM AccentColor lưu dưới định dạng ABGR (0xAABBGGRR)
                byte a = (byte)((rawColor >> 24) & 0xFF);
                byte b = (byte)((rawColor >> 16) & 0xFF);
                byte g = (byte)((rawColor >> 8) & 0xFF);
                byte r = (byte)(rawColor & 0xFF);
                if (a == 0) a = 255;

                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { }

        // 2. Kiểm tra qua DwmGetColorizationColor API của Windows
        try
        {
            NativeMethods.DwmGetColorizationColor(out uint colorization, out _);
            if (colorization != 0)
            {
                byte a = (byte)((colorization >> 24) & 0xFF);
                byte r = (byte)((colorization >> 16) & 0xFF);
                byte g = (byte)((colorization >> 8) & 0xFF);
                byte b = (byte)(colorization & 0xFF);
                if (a == 0) a = 255;

                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { }

        // 3. Kiểm tra qua WPF-UI SystemAccent
        try
        {
            var color = ApplicationAccentColorManager.SystemAccent;
            if (color.A > 0)
            {
                return color;
            }
        }
        catch { }

        // 4. Nếu không detect được, fallback về màu mặc định Windows 11 #0078D4
        return FallbackAccentColor;
    }
}
