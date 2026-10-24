using System;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using LMScreenShot.Core.Localization;

namespace LMScreenShot.Views.Pages;

public partial class AboutPage : Page
{
    private readonly string _versionStr;
    private readonly string _buildStr;

    public AboutPage()
    {
        InitializeComponent();

        var assembly = typeof(AboutPage).Assembly;
        var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (infoVersionAttr != null && !string.IsNullOrWhiteSpace(infoVersionAttr.InformationalVersion))
        {
            // Lấy chính xác chuỗi phiên bản trong .csproj (bỏ metadata commit hash sau dấu '+' nếu có)
            _versionStr = infoVersionAttr.InformationalVersion.Split('+')[0].Trim();
        }
        else
        {
            var version = assembly.GetName().Version;
            if (version != null)
            {
                _versionStr = (version.Build > 0 || version.Revision > 0)
                    ? $"{version.Major}.{version.Minor}.{version.Build}"
                    : $"{version.Major}.{version.Minor}";
            }
            else
            {
                _versionStr = "1.0";
            }
        }

        try
        {
            var path = Environment.ProcessPath ?? assembly.Location;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                _buildStr = File.GetLastWriteTime(path).ToString("yyyyMMdd");
            }
            else
            {
                _buildStr = DateTime.Now.ToString("yyyyMMdd");
            }
        }
        catch
        {
            _buildStr = DateTime.Now.ToString("yyyyMMdd");
        }

        Loaded += (_, _) => UpdateVersionText();
        LocalizationManager.LanguageChanged += UpdateVersionText;
        Unloaded += (_, _) => LocalizationManager.LanguageChanged -= UpdateVersionText;
    }

    private void UpdateVersionText()
    {
        TxtVersion.Text = LocalizationManager.GetString("Loc_About_Version", _versionStr);
        TxtBuild.Text = LocalizationManager.GetString("Loc_About_Build", _buildStr);
    }
}
