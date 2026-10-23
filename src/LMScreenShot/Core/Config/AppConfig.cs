using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            IsDarkMode = DetectWindowsDarkMode() ? 1 : 0,
            Lang = "vi",
            SavePath = @"Pictures\Screenshots"
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
