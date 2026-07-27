using ScreenshotTranslation.Core.Translation;
using ScreenshotTranslation.Infrastructure.Translation;

namespace ScreenshotTranslation.Infrastructure.Tests.Translation;

public sealed class OpenAiResponseParserTests
{
    [Theory]
    [InlineData("{\"status\":\"ok\",\"sourceLanguage\":\"English\",\"sourceLanguageCode\":\"en\",\"translation\":\"你好\"}")]
    [InlineData("```json\n{\"status\":\"ok\",\"sourceLanguage\":\"English\",\"sourceLanguageCode\":\"en\",\"translation\":\"你好\"}\n```")]
    public void Parser_accepts_plain_and_fenced_json(string content)
    {
        var result = OpenAiResponseParser.ParseScreenshotContent(content);

        Assert.Equal(TranslationResultStatus.Ok, result.Status);
        Assert.Equal("English", result.SourceLanguage);
        Assert.Equal("en", result.SourceLanguageCode);
        Assert.Equal("你好", result.Translation);
    }

    [Fact]
    public void Parser_accepts_no_text_status()
    {
        var result = OpenAiResponseParser.ParseScreenshotContent(
            "{\"status\":\"no_text\",\"sourceLanguage\":\"\",\"sourceLanguageCode\":\"\",\"translation\":\"\"}");

        Assert.Equal(TranslationResultStatus.NoText, result.Status);
        Assert.Empty(result.Translation);
    }

    [Theory]
    [InlineData("  你好，队友！  ", "你好，队友！")]
    [InlineData("Alice: 你好！\nBob: 去 B 点。\nGG", "Alice: 你好！\nBob: 去 B 点。\nGG")]
    public void Parser_falls_back_to_unambiguous_pure_text(string content, string expected)
    {
        var result = OpenAiResponseParser.ParseScreenshotContent(content);

        Assert.Equal(TranslationResultStatus.Ok, result.Status);
        Assert.Equal("Unknown", result.SourceLanguage);
        Assert.Equal("und", result.SourceLanguageCode);
        Assert.Equal(expected, result.Translation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json }")]
    [InlineData("```json\n{ not valid json }\n```")]
    [InlineData("```json\nnot json\n```")]
    [InlineData("```json\n{\"status\":\"ok\"}\n")]
    [InlineData("{\"status\":\"ok\",\"translation\":\"hello\"}")]
    [InlineData("{\"status\":\"unexpected\",\"sourceLanguage\":\"English\",\"sourceLanguageCode\":\"en\",\"translation\":\"hello\"}")]
    [InlineData("Here is the translation:\n你好")]
    [InlineData("Sure, here's the translation:\n你好")]
    [InlineData("The translation is:\n你好")]
    [InlineData("Sorry, I cannot translate this image")]
    [InlineData("I'm sorry, but I can't translate this screenshot.")]
    [InlineData("Unfortunately, I cannot translate this screenshot.")]
    [InlineData("Regrettably, I am unable to translate the image.")]
    [InlineData("以下是翻译：\nHello")]
    [InlineData("翻译如下：\nHello")]
    [InlineData("抱歉，我无法翻译这张图片")]
    [InlineData("对不起，我不能翻译这个截图")]
    [InlineData("不好意思，我无法翻译这张图片")]
    [InlineData("很抱歉，不能处理这个截图翻译请求")]
    public void Parser_rejects_invalid_or_incomplete_screenshot_content(string content)
    {
        var exception = Assert.Throws<TranslationClientException>(
            () => OpenAiResponseParser.ParseScreenshotContent(content));

        Assert.Equal(TranslationErrorCode.InvalidResponse, exception.Code);
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("```text\nhello\n```", "hello")]
    public void Reply_parser_returns_only_trimmed_translation(string content, string expected)
    {
        var result = OpenAiResponseParser.ParseReplyContent(content, "en");

        Assert.Equal("en", result.TargetLanguageCode);
        Assert.Equal(expected, result.Translation);
    }

    [Fact]
    public void Reply_parser_rejects_empty_content()
    {
        var exception = Assert.Throws<TranslationClientException>(
            () => OpenAiResponseParser.ParseReplyContent("  ", "en"));

        Assert.Equal(TranslationErrorCode.InvalidResponse, exception.Code);
    }
}
