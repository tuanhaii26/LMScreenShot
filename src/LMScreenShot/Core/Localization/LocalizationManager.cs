using System;
using System.Linq;
using System.Windows;

namespace LMScreenShot.Core.Localization;

public static class LocalizationManager
{
    private static string _currentLanguage = "vi-VN";
    public static string CurrentLanguage => _currentLanguage;

    public static event Action? LanguageChanged;

    public static void SetLanguage(string cultureCode)
    {
        if (cultureCode != "vi-VN" && cultureCode != "en-US")
        {
            cultureCode = "vi-VN";
        }

        _currentLanguage = cultureCode;

        var dictUri = new Uri($"pack://application:,,,/Resources/Languages/Strings.{cultureCode}.xaml", UriKind.Absolute);
        var newDict = new ResourceDictionary { Source = dictUri };

        // Find existing language dictionary if present
        var appResources = Application.Current.Resources;
        var existingDict = appResources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));

        if (existingDict != null)
        {
            int index = appResources.MergedDictionaries.IndexOf(existingDict);
            appResources.MergedDictionaries[index] = newDict;
        }
        else
        {
            appResources.MergedDictionaries.Add(newDict);
        }

        LanguageChanged?.Invoke();
    }

    public static string GetString(string key, params object[] args)
    {
        if (Application.Current.TryFindResource(key) is string str)
        {
            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(str, args);
                }
                catch
                {
                    return str;
                }
            }
            return str;
        }
        return key;
    }
}
