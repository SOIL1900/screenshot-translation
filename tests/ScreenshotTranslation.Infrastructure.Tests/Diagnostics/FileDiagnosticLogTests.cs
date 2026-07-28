using System.Text.Json;
using ScreenshotTranslation.Infrastructure.Diagnostics;

namespace ScreenshotTranslation.Infrastructure.Tests.Diagnostics;

public sealed class FileDiagnosticLogTests
{
    private static readonly string[] ExpectedSchema =
        ["timestamp", "eventName", "exceptionType", "hResult", "captureStage"];

    [Fact]
    public async Task Diagnostic_log_never_writes_user_content()
    {
        using var directory = new TemporaryDirectory();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 27, 23, 15, 30, 123, TimeSpan.FromHours(8)));
        var log = new FileDiagnosticLog(directory.Path, timeProvider);
        var exception = new InvalidOperationException("sk-secret translated text");

        await log.WriteAsync("translation_failed", exception);
        var text = await File.ReadAllTextAsync(log.LogPath);

        Assert.Contains("InvalidOperationException", text);
        Assert.DoesNotContain("sk-secret", text);
        Assert.DoesNotContain("translated text", text);

        using var document = JsonDocument.Parse(text);
        Assert.Equal(
            ExpectedSchema,
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("2026-07-27T15:15:30.123Z", document.RootElement.GetProperty("timestamp").GetString());
        Assert.Equal("translation_failed", document.RootElement.GetProperty("eventName").GetString());
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            document.RootElement.GetProperty("exceptionType").GetString());
        Assert.Equal(exception.HResult, document.RootElement.GetProperty("hResult").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("captureStage").ValueKind);
    }

    [Fact]
    public async Task Diagnostic_log_does_not_serialize_an_unrecognized_event_name()
    {
        using var directory = new TemporaryDirectory();
        var log = new FileDiagnosticLog(directory.Path, TimeProvider.System);

        await log.WriteAsync("sk-secret translated text");
        var text = await File.ReadAllTextAsync(log.LogPath);

        Assert.DoesNotContain("sk-secret", text);
        Assert.DoesNotContain("translated text", text);
        Assert.Contains("unrecognized_event", text);
    }

    [Theory]
    [InlineData("hotkey_registration_failed")]
    [InlineData("startup_failed")]
    [InlineData("capture_workflow_failed")]
    public async Task App_failure_categories_keep_the_fixed_content_free_schema(string eventName)
    {
        using var directory = new TemporaryDirectory();
        var log = new FileDiagnosticLog(directory.Path, TimeProvider.System);
        var exception = new InvalidOperationException("sk-secret screenshot translation");

        await log.WriteAsync(eventName, exception);

        var text = await File.ReadAllTextAsync(log.LogPath);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(eventName, document.RootElement.GetProperty("eventName").GetString());
        Assert.Equal(
            ExpectedSchema,
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("sk-secret", text);
        Assert.DoesNotContain("screenshot translation", text);
    }

    [Theory]
    [InlineData("show_overlay", "show_overlay")]
    [InlineData("sk-secret screenshot translation", null)]
    public async Task Capture_stage_only_serializes_allowlisted_content(
        string suppliedStage,
        string? expectedStage)
    {
        using var directory = new TemporaryDirectory();
        var log = new FileDiagnosticLog(directory.Path, TimeProvider.System);
        var exception = new InvalidOperationException("discard me");
        exception.Data["CaptureStage"] = suppliedStage;

        await log.WriteAsync("capture_workflow_failed", exception);

        var text = await File.ReadAllTextAsync(log.LogPath);
        using var document = JsonDocument.Parse(text);
        var captureStage = document.RootElement.GetProperty("captureStage");
        if (expectedStage is null)
        {
            Assert.Equal(JsonValueKind.Null, captureStage.ValueKind);
        }
        else
        {
            Assert.Equal(expectedStage, captureStage.GetString());
        }

        Assert.DoesNotContain("sk-secret", text);
        Assert.DoesNotContain("screenshot translation", text);
        Assert.DoesNotContain("discard me", text);
    }

    [Fact]
    public async Task Diagnostic_log_appends_one_json_object_per_line()
    {
        using var directory = new TemporaryDirectory();
        var log = new FileDiagnosticLog(directory.Path, TimeProvider.System);

        await log.WriteAsync("translation_failed");
        await log.WriteAsync("translation_failed", new InvalidOperationException("discard me"));

        var lines = await File.ReadAllLinesAsync(log.LogPath);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => JsonDocument.Parse(line).Dispose());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
