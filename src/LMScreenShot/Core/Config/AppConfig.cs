using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LMScreenShot.Core.Native;
using Microsoft.Win32;

namespace LMScreenShot.Core.Config;

public class AppConfig
{
    private static readonly string ConfigFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "config.json");

    private static AppConfig? _instance;
    public static AppConfig Instance => _instance ??= Load();

    [JsonPropertyName("autosave")]
    public int AutoSave { get; set; } = 1;

    [JsonPropertyName("autoclipboard")]
    public int AutoClipboard { get; set; } = 1;

    [JsonPropertyName("minimizetotray")]
    public int MinimizeToTray { get; set; } = 1;

    [JsonPropertyName("minimizeonclose")]
    public int MinimizeOnClose { get; set; } = 0;

    [JsonPropertyName("isdarkmode")]
    public int IsDarkMode { get; set; } = 1;

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "vi";

    [JsonPropertyName("savepath")]
    public string SavePath { get; set; } = @"Pictures\Screenshots";

    [JsonPropertyName("windowwidth")]
    public double WindowWidth { get; set; } = 1080;

    [JsonPropertyName("windowheight")]
    public double WindowHeight { get; set; } = 580;

    [JsonPropertyName("ismaximized")]
    public int IsMaximized { get; set; } = 0;

    [JsonPropertyName("startup")]
    public int Startup { get; set; } = 0;

    [JsonPropertyName("capture_modifiers")]
    public uint CaptureModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;

    [JsonPropertyName("capture_key")]
    public uint CaptureKey { get; set; } = 0x58; // VK_X

    [JsonPropertyName("identify_modifiers")]
    public uint IdentifyModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;

    [JsonPropertyName("identify_key")]
    public uint IdentifyKey { get; set; } = 0xC0; // VK_OEM_3 (~)

    [JsonPropertyName("instant_modifiers")]
    public uint InstantModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;

    [JsonIgnore]
    public string CaptureHotkeyText => FormatHotkey(CaptureModifiers, CaptureKey);

    [JsonIgnore]
    public string IdentifyHotkeyText => FormatHotkey(IdentifyModifiers, IdentifyKey);

    [JsonIgnore]
    public string InstantHotkeyText => $"{FormatModifiers(InstantModifiers)} + 1..9";

    public static string FormatModifiers(uint modifiers)
    {
        var list = new List<string>();
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0) list.Add("Ctrl");
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0) list.Add("Shift");
        if ((modifiers & NativeMethods.MOD_ALT) != 0) list.Add("Alt");
        if ((modifiers & NativeMethods.MOD_WIN) != 0) list.Add("Win");
        return list.Count > 0 ? string.Join(" + ", list) : "None";
    }

    public static string FormatKey(uint vk)
    {
        if (vk == 0xC0) return "`";
        if (vk == 0xDB) return "[";
        if (vk == 0xDD) return "]";
        if (vk == 0xDC) return "\\";
        if (vk == 0xBA) return ";";
        if (vk == 0xDE) return "'";
        if (vk == 0xBC) return ",";
        if (vk == 0xBE) return ".";
        if (vk == 0xBF) return "/";
        if (vk == 0xBD) return "-";
        if (vk == 0xBB) return "=";
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        if (vk >= 0x70 && vk <= 0x87) return $"F{vk - 0x70 + 1}";
        if (vk == 0x2C) return "PrtScn";
        if (vk == 0x20) return "Space";
        if (vk == 0x2D) return "Insert";
        if (vk == 0x2E) return "Delete";
        if (vk == 0x24) return "Home";
        if (vk == 0x23) return "End";
        if (vk == 0x21) return "PageUp";
        if (vk == 0x22) return "PageDown";
        if (vk == 0x0D) return "Enter";

        try
        {
            var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)vk);
            return key.ToString();
        }
        catch
        {
            return $"0x{vk:X2}";
        }
    }

    public static string FormatHotkey(uint modifiers, uint vk)
    {
        string mods = FormatModifiers(modifiers);
        string key = FormatKey(vk);
        return string.IsNullOrEmpty(mods) || mods == "None" ? key : $"{mods} + {key}";
    }

    [JsonIgnore]
    public string ResolvedSavePath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SavePath))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Screenshots");
            }

            if (Path.IsPathRooted(SavePath))
            {
                return SavePath;
            }

            if (SavePath.StartsWith("%", StringComparison.Ordinal))
            {
                return Environment.ExpandEnvironmentVariables(SavePath);
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, SavePath);
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    return config;
                }
            }
        }
        catch
        {
            // If error reading, re-create default
        }

        var defaultConfig = CreateDefault();
        defaultConfig.Save();
        return defaultConfig;
    }

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            AutoSave = 1,
            AutoClipboard = 1,
            MinimizeToTray = 1,
            MinimizeOnClose = 0,
            IsDarkMode = DetectWindowsDarkMode() ? 1 : 0,
            Lang = "vi",
            SavePath = @"Pictures\Screenshots",
            Startup = 0,
            CaptureModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT,
            CaptureKey = 0x58,
            IdentifyModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT,
            IdentifyKey = 0xC0,
            InstantModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT
        };
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    private static bool DetectWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int lightThemeValue)
            {
                return lightThemeValue == 0; // 0 = Dark, 1 = Light
            }
        }
        catch { }

        return true;
    }
}
