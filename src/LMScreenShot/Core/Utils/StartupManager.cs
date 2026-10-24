using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace LMScreenShot.Core.Utils;

public static class StartupManager
{
    private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "LMScreenShot";

    public static void ApplyStartup(int startupMode)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, writable: true);
            if (key == null) return;

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(exePath)) return;

            if (startupMode == 0) // Tắt
            {
                key.DeleteValue(APP_NAME, false);
            }
            else if (startupMode == 1) // Bật
            {
                key.SetValue(APP_NAME, $"\"{exePath}\"");
            }
            else if (startupMode == 2) // Bật, thu nhỏ
            {
                key.SetValue(APP_NAME, $"\"{exePath}\" --minimized");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupManager] Error applying startup: {ex.Message}");
        }
    }

    public static int GetCurrentStartupMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, writable: false);
            if (key?.GetValue(APP_NAME) is string val)
            {
                if (val.Contains("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    return 2; // Bật, thu nhỏ
                }
                return 1; // Bật
            }
        }
        catch { }

        return 0; // Tắt
    }
}
