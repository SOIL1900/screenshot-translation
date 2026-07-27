using System.Text.Json;
using System.Text.Json.Nodes;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Infrastructure.Configuration;

namespace ScreenshotTranslation.Infrastructure.Tests.Configuration;

public sealed class JsonSettingsStoreTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    [Theory]
    [MemberData(nameof(StructurallyInvalidSettingsJson))]
    public async Task Load_backs_up_structurally_invalid_settings_and_returns_defaults(string json)
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSettingsStore(directory.Path, TimeProvider.System);
        await File.WriteAllTextAsync(store.SettingsPath, json);

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AppSettings.CreateDefault(), actual);
        Assert.False(File.Exists(store.SettingsPath));
        var backup = Assert.Single(Directory.GetFiles(directory.Path, "settings.corrupt-*.json"));
        Assert.Equal(json, await File.ReadAllTextAsync(backup));
    }

    [Fact]
    public async Task Load_accepts_valid_first_run_settings_with_an_empty_api_key()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonSettingsStore(directory.Path, TimeProvider.System);
        var expected = AppSettings.CreateDefault();
        await File.WriteAllTextAsync(
            store.SettingsPath,
            JsonSerializer.Serialize(expected, JsonOptions));

        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.True(File.Exists(store.SettingsPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "settings.corrupt-*.json"));
    }

    public static IEnumerable<object[]> StructurallyInvalidSettingsJson()
    {
        yield return ["{}"];
        yield return [Mutate(root => root["general"] = null)];
        yield return [Mutate(root => root["model"] = null)];
        yield return [Mutate(root => ((JsonObject)root["general"]!)["captureHotkey"] = null)];
        yield return [Mutate(root =>
        {
            var model = (JsonObject)root["model"]!;
            model["baseUrl"] = "not-a-url";
            model["modelName"] = " ";
        })];
        yield return [Mutate(root =>
        {
            var hotkey = (JsonObject)((JsonObject)root["general"]!)["captureHotkey"]!;
            hotkey["modifiers"] = 0;
            hotkey["virtualKey"] = 0x11;
        })];
        yield return [Mutate(root =>
        {
            var model = (JsonObject)root["model"]!;
            model["temperature"] = 3;
            model["maxOutputTokens"] = 1;
            model["requestTimeoutSeconds"] = 1;
        })];
        yield return [Mutate(root => ((JsonObject)root["model"]!)["extraParametersJson"] = "not json")];
    }

    private static string Mutate(Action<JsonObject> mutation)
    {
        var root = Assert.IsType<JsonObject>(
            JsonSerializer.SerializeToNode(AppSettings.CreateDefault(), JsonOptions));
        mutation(root);
        return root.ToJsonString();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
