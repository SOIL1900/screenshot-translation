using System.Text.Json.Nodes;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;
using ScreenshotTranslation.Infrastructure.Translation;

namespace ScreenshotTranslation.Infrastructure.Tests.Translation;

public sealed class OpenAiRequestFactoryTests
{
    [Fact]
    public void Screenshot_request_contains_image_target_language_and_disabled_thinking()
    {
        var settings = AppSettings.CreateDefault().Model with { ApiKey = "sk-test" };
        var pngBytes = TestPngFactory.CreateSolid(64, 32);

        var json = OpenAiRequestFactory.CreateScreenshotRequest(
            settings,
            pngBytes,
            "zh-CN");

        Assert.Equal("qwen3.7-flash", json["model"]!.GetValue<string>());
        Assert.False(json["enable_thinking"]!.GetValue<bool>());
        Assert.False(json["stream"]!.GetValue<bool>());
        Assert.Equal(0.2, json["temperature"]!.GetValue<double>());
        Assert.Equal(2048, json["max_tokens"]!.GetValue<int>());
        Assert.StartsWith(
            "data:image/png;base64,",
            json["messages"]![0]!["content"]![1]!["image_url"]!["url"]!.GetValue<string>());
        Assert.Contains(
            "zh-CN",
            json["messages"]![0]!["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Screenshot_request_normalizes_four_k_png_before_base64_encoding()
    {
        var settings = AppSettings.CreateDefault().Model with { ApiKey = "sk-test" };
        var pngBytes = TestPngFactory.CreateSolid(3840, 2160);

        var json = OpenAiRequestFactory.CreateScreenshotRequest(settings, pngBytes, "zh-CN");

        var dataUrl = json["messages"]![0]!["content"]![1]!["image_url"]!["url"]!.GetValue<string>();
        var normalized = Convert.FromBase64String(dataUrl["data:image/png;base64,".Length..]);
        var image = TestPngFactory.Inspect(normalized);
        Assert.Equal(2048, image.Width);
        Assert.Equal(1152, image.Height);
        Assert.True(normalized.Length <= PngRequestImageNormalizer.MaxEncodedPngBytes);
    }

    [Fact]
    public void Extra_parameters_merge_without_overwriting_reserved_request_fields()
    {
        var settings = AppSettings.CreateDefault().Model with
        {
            ModelName = "protected-model",
            EnableThinking = false,
            ExtraParametersJson = """
                {
                  "top_p": 0.75,
                  "model": "attacker-model",
                  "messages": [{ "role": "user", "content": "attacker-message" }],
                  "stream": true,
                  "enable_thinking": true,
                  "temperature": 1.9,
                  "max_tokens": 9999
                }
                """
        };

        var json = OpenAiRequestFactory.CreateReplyRequest(settings, "hello", "ja");

        Assert.Equal("protected-model", json["model"]!.GetValue<string>());
        Assert.False(json["stream"]!.GetValue<bool>());
        Assert.False(json["enable_thinking"]!.GetValue<bool>());
        Assert.Equal(0.2, json["temperature"]!.GetValue<double>());
        Assert.Equal(2048, json["max_tokens"]!.GetValue<int>());
        Assert.Equal(0.75, json["top_p"]!.GetValue<double>());
        Assert.Contains("hello", json["messages"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void Reply_and_connection_requests_use_text_only_prompts()
    {
        var settings = AppSettings.CreateDefault().Model;

        JsonObject reply = OpenAiRequestFactory.CreateReplyRequest(settings, "good game", "de");
        JsonObject connection = OpenAiRequestFactory.CreateConnectionTestRequest(settings);

        Assert.Contains("de", reply["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Contains("good game", reply["messages"]![0]!["content"]!.GetValue<string>());
        Assert.DoesNotContain("image_url", reply.ToJsonString());
        Assert.Contains("OK", connection["messages"]![0]!["content"]!.GetValue<string>());
        Assert.DoesNotContain("image_url", connection.ToJsonString());
    }

    [Fact]
    public void Language_catalog_exposes_the_single_immutable_supported_language_list()
    {
        Assert.Equal(15, LanguageCatalog.All.Count);
        Assert.Equal("zh-CN", LanguageCatalog.All[0].Code);
        Assert.Contains(LanguageCatalog.All, language => language.Code == "ar");
        Assert.Equal(
            LanguageCatalog.All.Count,
            LanguageCatalog.All.Select(language => language.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(LanguageCatalog.All, language =>
        {
            Assert.False(string.IsNullOrWhiteSpace(language.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(language.PromptName));
        });
    }
}
