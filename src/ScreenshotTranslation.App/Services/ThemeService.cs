using System.Windows;
using Microsoft.Win32;
using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.App.Services;

public interface IThemeService
{
    void Apply(AppTheme theme);
}

public sealed class ThemeService : IThemeService
{
    private const string LightThemeSource = "/ScreenshotTranslation;component/Themes/Colors.Light.xaml";
    private const string DarkThemeSource = "/ScreenshotTranslation;component/Themes/Colors.Dark.xaml";
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly ResourceDictionary _applicationResources;

    public ThemeService()
        : this(System.Windows.Application.Current?.Resources ??
            throw new InvalidOperationException("WPF application resources are not available."))
    {
    }

    internal ThemeService(ResourceDictionary applicationResources)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);
        _applicationResources = applicationResources;
    }

    public void Apply(AppTheme theme)
    {
        var resolvedTheme = theme == AppTheme.System
            ? ReadSystemTheme()
            : theme;
        var themeSource = resolvedTheme == AppTheme.Dark
            ? DarkThemeSource
            : LightThemeSource;

        var mergedDictionaries = _applicationResources.MergedDictionaries;
        var existingThemes = mergedDictionaries
            .Where(dictionary => IsThemeDictionary(dictionary.Source))
            .ToArray();
        foreach (var existingTheme in existingThemes)
        {
            mergedDictionaries.Remove(existingTheme);
        }

        mergedDictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri(themeSource, UriKind.Relative)
        });
    }

    private static bool IsThemeDictionary(Uri? source)
    {
        var value = source?.OriginalString;
        return value is not null &&
            (value.EndsWith("/Themes/Colors.Light.xaml", StringComparison.OrdinalIgnoreCase) ||
             value.EndsWith("/Themes/Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase));
    }

    private static AppTheme ReadSystemTheme()
    {
        using var personalizeKey = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
        return personalizeKey?.GetValue("AppsUseLightTheme") is int appsUseLightTheme && appsUseLightTheme == 0
            ? AppTheme.Dark
            : AppTheme.Light;
    }
}
