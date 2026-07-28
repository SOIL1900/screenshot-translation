using System.Windows;
using ScreenshotTranslation.App.Services;
using ScreenshotTranslation.Core.Configuration;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextSearch = System.Windows.Controls.TextSearch;

namespace ScreenshotTranslation.App.Tests.Services;

public sealed class ThemeServiceTests
{
    [Fact]
    public void Apply_replaces_relative_light_and_remains_idempotent_with_one_color_dictionary()
    {
        StaTestHost.Run(() =>
        {
            var application = new ScreenshotTranslation.App.App();
            application.InitializeComponent();
            var mergedDictionaries = application.Resources.MergedDictionaries;
            var initialColorDictionary = Assert.Single(
                mergedDictionaries,
                dictionary => ThemeService.IsColorDictionarySource(dictionary.Source));
            Assert.Equal("Themes/Colors.Light.xaml", initialColorDictionary.Source.OriginalString);
            var nonColorDictionaries = mergedDictionaries
                .Where(dictionary => !ThemeService.IsColorDictionarySource(dictionary.Source))
                .ToArray();
            var service = new ThemeService(application.Resources);

            service.Apply(AppTheme.Dark);

            var darkDictionary = Assert.Single(
                mergedDictionaries,
                dictionary => ThemeService.IsColorDictionarySource(dictionary.Source));
            Assert.EndsWith("Themes/Colors.Dark.xaml", darkDictionary.Source.OriginalString);
            var pageTitleStyle = Assert.IsType<Style>(
                application.FindResource("PageTitleTextStyle"));
            var sectionHeaderStyle = Assert.IsType<Style>(
                application.FindResource("SectionHeaderTextStyle"));
            var comboBoxStyle = Assert.IsType<Style>(
                application.FindResource(typeof(WpfComboBox)));
            Assert.Contains(
                pageTitleStyle.Setters.OfType<Setter>(),
                setter => setter.Property == WpfTextBlock.ForegroundProperty);
            Assert.Contains(
                sectionHeaderStyle.Setters.OfType<Setter>(),
                setter => setter.Property == WpfTextBlock.ForegroundProperty);
            Assert.Contains(
                comboBoxStyle.Setters.OfType<Setter>(),
                setter => setter.Property == WpfComboBox.TemplateProperty);
            Assert.Contains(
                comboBoxStyle.Setters.OfType<Setter>(),
                setter => setter.Property == WpfTextSearch.TextPathProperty &&
                          Equals(setter.Value, "DisplayName"));
            Assert.Equal(
                nonColorDictionaries,
                mergedDictionaries.Where(dictionary => !ThemeService.IsColorDictionarySource(dictionary.Source)));

            service.Apply(AppTheme.Light);
            service.Apply(AppTheme.Light);

            var finalDictionary = Assert.Single(
                mergedDictionaries,
                dictionary => ThemeService.IsColorDictionarySource(dictionary.Source));
            Assert.EndsWith("Themes/Colors.Light.xaml", finalDictionary.Source.OriginalString);
            Assert.Equal(
                nonColorDictionaries,
                mergedDictionaries.Where(dictionary => !ThemeService.IsColorDictionarySource(dictionary.Source)));
        });
    }

    [Fact]
    public void Color_dictionary_recognition_accepts_relative_and_pack_style_uris()
    {
        Assert.True(ThemeService.IsColorDictionarySource(
            new Uri("Themes/Colors.Light.xaml", UriKind.Relative)));
        Assert.True(ThemeService.IsColorDictionarySource(
            new Uri("/ScreenshotTranslation;component/Themes/Colors.Dark.xaml", UriKind.Relative)));

        if (Uri.TryCreate(
                "pack://application:,,,/ScreenshotTranslation;component/Themes/Colors.Light.xaml",
                UriKind.Absolute,
                out var absolutePackUri))
        {
            Assert.True(ThemeService.IsColorDictionarySource(absolutePackUri));
        }
    }

    private static class StaTestHost
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public static void Run(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            })
            {
                IsBackground = true,
                Name = "ScreenshotTranslation.ThemeServiceTests.STA"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Assert.True(thread.Join(Timeout), "The STA theme test thread did not stop.");
            if (failure is not null)
            {
                throw new Xunit.Sdk.XunitException(
                    $"The STA theme operation failed: {failure.GetType().Name}: {failure.Message}");
            }
        }
    }
}
