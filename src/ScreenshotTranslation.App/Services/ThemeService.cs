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
        var firstColorDictionaryIndex = -1;
        var colorDictionaries = new List<ResourceDictionary>();
        for (var index = 0; index < mergedDictionaries.Count; index++)
        {
            var dictionary = mergedDictionaries[index];
            if (!IsColorDictionarySource(dictionary.Source))
            {
                continue;
            }

            firstColorDictionaryIndex = firstColorDictionaryIndex < 0
                ? index
                : firstColorDictionaryIndex;
            colorDictionaries.Add(dictionary);
        }

        foreach (var colorDictionary in colorDictionaries)
        {
            mergedDictionaries.Remove(colorDictionary);
        }

        var insertionIndex = firstColorDictionaryIndex < 0
            ? 0
            : Math.Min(firstColorDictionaryIndex, mergedDictionaries.Count);
        mergedDictionaries.Insert(insertionIndex, new ResourceDictionary
        {
            Source = new Uri(themeSource, UriKind.Relative)
        });
    }

    internal static bool IsColorDictionarySource(Uri? source)
    {
        if (source is null)
        {
            return false;
        }

        var normalizedSource = Uri.UnescapeDataString(source.OriginalString).Replace('\\', '/');
        var queryOrFragmentIndex = normalizedSource.IndexOfAny(['?', '#']);
        if (queryOrFragmentIndex >= 0)
        {
            normalizedSource = normalizedSource[..queryOrFragmentIndex];
        }

        var fileName = normalizedSource[(normalizedSource.LastIndexOf('/') + 1)..];
        return string.Equals(fileName, "Colors.Light.xaml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase);
    }

    private static AppTheme ReadSystemTheme()
    {
        using var personalizeKey = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
        return personalizeKey?.GetValue("AppsUseLightTheme") is int appsUseLightTheme && appsUseLightTheme == 0
            ? AppTheme.Dark
            : AppTheme.Light;
    }
}
