using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Infrastructure.Configuration;

namespace ScreenshotTranslation.Infrastructure.Tests.Configuration;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task Save_and_load_round_trip_plaintext_api_key()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSettingsStore(directory.Path, TimeProvider.System);
        var expected = AppSettings.CreateDefault() with
        {
            Model = AppSettings.CreateDefault().Model with { ApiKey = "sk-personal-value" }
        };

        await store.SaveAsync(AppSettings.CreateDefault(), CancellationToken.None);
        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, actual);

        var json = await File.ReadAllTextAsync(store.SettingsPath);
        Assert.Contains("sk-personal-value", json);
        Assert.Contains("\"apiKey\"", json);
        Assert.Contains(Environment.NewLine, json);
        Assert.False(File.Exists(store.SettingsPath + ".tmp"));
    }

    [Fact]
    public async Task Load_backs_up_corrupt_file_and_returns_defaults()
    {
        using var directory = new TemporaryDirectory();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 27, 23, 15, 30, 123, TimeSpan.FromHours(8)));
        var store = new JsonSettingsStore(directory.Path, timeProvider);
        await File.WriteAllTextAsync(store.SettingsPath, "{ definitely not valid JSON");

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AppSettings.CreateDefault(), actual);
        Assert.False(File.Exists(store.SettingsPath));

        var backupPath = System.IO.Path.Combine(directory.Path, "settings.corrupt-20260727-151530123.json");
        Assert.True(File.Exists(backupPath));
        Assert.Equal("{ definitely not valid JSON", await File.ReadAllTextAsync(backupPath));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
