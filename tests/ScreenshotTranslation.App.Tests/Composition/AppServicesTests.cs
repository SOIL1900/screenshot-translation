using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Windows;
using ScreenshotTranslation.App.Composition;
using ScreenshotTranslation.App.Services;
using ScreenshotTranslation.Core.Configuration;

namespace ScreenshotTranslation.App.Tests.Composition;

public sealed class AppServicesTests
{
    [Fact]
    public void Composed_translation_uses_injected_local_handler_without_network()
    {
        StaTestHost.Run(async () =>
        {
            using var directory = new TemporaryDirectory();
            var handler = new DeterministicTranslationHandler();
            using var services = new AppServices(
                new ResourceDictionary(),
                directory.Path,
                Path.Combine(directory.Path, "ScreenshotTranslation.exe"),
                TimeProvider.System,
                handler);
            var settings = AppSettings.CreateDefault().Model with
            {
                BaseUrl = "https://local-fake.invalid/v1",
                ApiKey = "sk-local-test"
            };

            var result = await services.TranslationCoordinator.TranslateReplyAsync(
                "hello",
                "en",
                settings,
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("deterministic reply", result.Translation);
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(
                "https://local-fake.invalid/v1/chat/completions",
                handler.LastRequestUri?.AbsoluteUri);
        });
    }

    [Fact]
    public void Tray_icon_loads_generated_asset_and_has_exact_three_item_menu()
    {
        StaTestHost.Run(() =>
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "AppIcon.ico");

            using var tray = new TrayIconService(iconPath, visible: false);

            Assert.Equal(
                ["开始截图翻译", "设置", "退出"],
                tray.MenuItemTexts);
            return Task.CompletedTask;
        });
    }

    private sealed class DeterministicTranslationHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            const string responseJson =
                "{\"choices\":[{\"message\":{\"content\":\"deterministic reply\"}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ScreenshotTranslation.AppServicesTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static class StaTestHost
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        public static void Run(Func<Task> action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action().GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            })
            {
                IsBackground = true,
                Name = "ScreenshotTranslation.AppServicesTests.STA"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Assert.True(thread.Join(Timeout), "The STA composition smoke test did not stop.");
            if (failure is not null)
            {
                throw new Xunit.Sdk.XunitException(
                    $"The STA composition smoke failed: {failure.GetType().Name}: {failure.Message}");
            }
        }
    }
}
