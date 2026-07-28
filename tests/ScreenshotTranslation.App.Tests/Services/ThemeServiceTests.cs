using System.Windows;
using ScreenshotTranslation.App.Services;
using ScreenshotTranslation.App.Overlay;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextSearch = System.Windows.Controls.TextSearch;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

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

            var primaryButtonStyle = Assert.IsType<Style>(
                application.FindResource("PrimaryButtonStyle"));
            var primaryButton = new WpfButton { Style = primaryButtonStyle };
            var primaryForeground = Assert.IsType<WpfSolidColorBrush>(primaryButton.Foreground);
            Assert.Equal(System.Windows.Media.Colors.White, primaryForeground.Color);

            mergedDictionaries.Add(Assert.IsType<ResourceDictionary>(
                System.Windows.Application.LoadComponent(
                    new Uri(
                        "/ScreenshotTranslation;component/Overlay/OverlayColors.xaml",
                        UriKind.Relative))));
            var panel = new TranslationPanelView
            {
                Width = 920,
                DataContext = new PanelPreviewData(),
            };
            panel.Measure(new System.Windows.Size(920, OverlayViewModel.PanelMaximumHeight));
            panel.Arrange(new System.Windows.Rect(0, 0, 920, panel.DesiredSize.Height));
            panel.UpdateLayout();

            var replyInputTop = panel.ReplyInput.TranslatePoint(new System.Windows.Point(), panel).Y;
            var replyOutputTop = panel.ReplyTranslationOutput.TranslatePoint(new System.Windows.Point(), panel).Y;
            Assert.Equal(replyInputTop, replyOutputTop, precision: 3);
            Assert.Equal(32, panel.ReplyInput.ActualHeight, precision: 3);
            Assert.Equal(32, panel.ReplyTranslationOutput.ActualHeight, precision: 3);
            Assert.True(panel.ScreenshotTranslationOutput.ActualHeight > 32);
            Assert.True(panel.ActualHeight > OverlayViewModel.PanelMinimumHeight);
            Assert.Equal(
                WpfScrollBarVisibility.Disabled,
                panel.ScreenshotTranslationOutput.HorizontalScrollBarVisibility);
            Assert.Equal(
                WpfScrollBarVisibility.Disabled,
                panel.ReplyTranslationOutput.HorizontalScrollBarVisibility);
            var languageItemText = Assert.IsType<WpfTextBlock>(
                panel.ScreenshotTargetLanguageInput.ItemTemplate.LoadContent());
            var languageItemForeground = Assert.IsType<WpfSolidColorBrush>(
                languageItemText.Foreground);
            Assert.Equal(System.Windows.Media.Colors.White, languageItemForeground.Color);
            var copyButtonText = Assert.IsType<WpfTextBlock>(panel.CopyScreenshotButton.Content);
            var copyButtonForeground = Assert.IsType<WpfSolidColorBrush>(copyButtonText.Foreground);
            Assert.Equal(System.Windows.Media.Colors.White, copyButtonForeground.Color);
            panel.RetryScreenshotButton.IsEnabled = false;
            panel.UpdateLayout();
            Assert.Equal(1, panel.RetryScreenshotButton.Opacity);
            var retryButtonText = Assert.IsType<WpfTextBlock>(panel.RetryScreenshotButton.Content);
            var retryButtonForeground = Assert.IsType<WpfSolidColorBrush>(retryButtonText.Foreground);
            Assert.Equal(
                System.Windows.Media.Color.FromRgb(0xA9, 0xA6, 0xB4),
                retryButtonForeground.Color);
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

    private sealed class PanelPreviewData
    {
        public IReadOnlyList<LanguageOption> Languages { get; } = LanguageCatalog.All;

        public string ScreenshotTargetLanguage { get; } = "zh-CN";

        public string ScreenshotTranslation { get; } = string.Concat(
            Enumerable.Repeat("这是一段用于验证翻译内容框能够根据文字长度自动换行并增加高度的中文译文。", 8));

        public bool IsScreenshotLoadingVisible { get; } = false;

        public string StatusMessage { get; } = "截图翻译完成";

        public string ReplyInput { get; set; } = string.Empty;

        public string ReplyTargetLanguage { get; set; } = "en";

        public string ReplyTranslation { get; } = "Short reply";

        public string ReplyStatusMessage { get; } = "回复翻译完成";

        public string ClipboardError { get; } = string.Empty;
    }
}
