using System.Text.Json;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.Infrastructure.Translation;

public static class OpenAiResponseParser
{
    private static readonly string[] EnglishExplanationPrefixes =
    [
        "here is the translation",
        "here's the translation",
        "here is your translation",
        "the translation is",
        "translation:",
        "translated text:",
        "sure, here is the translation",
        "sure, here's the translation",
        "certainly, here is the translation",
        "certainly, here's the translation"
    ];

    private static readonly string[] ChineseExplanationPrefixes =
    [
        "以下是翻译",
        "以下为翻译",
        "下面是翻译",
        "翻译如下",
        "译文如下",
        "这是翻译",
        "翻译结果"
    ];

    private static readonly string[] EnglishInabilityTerms =
    [
        "cannot",
        "can't",
        "unable",
        "not able",
        "won't",
        "will not",
        "refuse"
    ];

    private static readonly string[] EnglishResponseTopics =
    [
        "translat",
        "image",
        "screenshot",
        "request",
        "assist",
        "help"
    ];

    private static readonly string[] ChineseInabilityTerms = ["无法", "不能", "没法", "不支持", "拒绝"];
    private static readonly string[] ChineseResponseTopics = ["翻译", "图片", "图像", "截图", "请求", "帮助"];
    private static readonly string[] EnglishMetaPrefixes = ["as an ai", "as a language model"];
    private static readonly string[] ChineseMetaPrefixes = ["作为AI", "作为 AI", "作为一个AI", "作为一个 AI"];

    public static ScreenshotTranslationResult ParseScreenshotContent(string content)
    {
        NormalizedContent normalized = NormalizeContent(content);
        string candidate = normalized.Text;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw InvalidResponse("The model returned empty translation content.");
        }

        if (normalized.IsJsonFence || LooksLikeJson(candidate))
        {
            return ParseScreenshotJson(candidate);
        }

        if (LooksLikeExplanatoryOrRefusalText(candidate))
        {
            throw InvalidResponse("The model returned explanatory or refusal text instead of a translation.");
        }

        return new ScreenshotTranslationResult(
            TranslationResultStatus.Ok,
            "Unknown",
            "und",
            candidate.Trim());
    }

    public static ReplyTranslationResult ParseReplyContent(string content, string targetLanguageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguageCode);

        NormalizedContent normalized = NormalizeContent(content);
        string translation = normalized.Text.Trim();
        if (string.IsNullOrWhiteSpace(translation) ||
            normalized.IsJsonFence ||
            LooksLikeJson(translation) ||
            LooksLikeExplanatoryOrRefusalText(translation))
        {
            throw InvalidResponse("The model returned no usable reply translation.");
        }

        return new ReplyTranslationResult(targetLanguageCode, translation);
    }

    public static string ExtractAssistantContent(string responseJson)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(responseJson);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("choices", out JsonElement choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0 ||
                choices[0].ValueKind != JsonValueKind.Object ||
                !choices[0].TryGetProperty("message", out JsonElement message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("content", out JsonElement content) ||
                content.ValueKind != JsonValueKind.String)
            {
                throw InvalidResponse("The service response did not contain assistant content.");
            }

            string? value = content.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw InvalidResponse("The service response contained empty assistant content.");
            }

            return value;
        }
        catch (TranslationClientException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The service returned malformed JSON.", exception);
        }
    }

    private static ScreenshotTranslationResult ParseScreenshotJson(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetString(root, "status", out string status))
            {
                throw InvalidResponse("The screenshot translation response has an invalid status.");
            }

            if (string.Equals(status, "no_text", StringComparison.OrdinalIgnoreCase))
            {
                return new ScreenshotTranslationResult(
                    TranslationResultStatus.NoText,
                    GetOptionalString(root, "sourceLanguage"),
                    GetOptionalString(root, "sourceLanguageCode"),
                    string.Empty);
            }

            if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase) ||
                !TryGetNonEmptyString(root, "sourceLanguage", out string sourceLanguage) ||
                !TryGetNonEmptyString(root, "sourceLanguageCode", out string sourceLanguageCode) ||
                !TryGetNonEmptyString(root, "translation", out string translation))
            {
                throw InvalidResponse("The screenshot translation response is incomplete.");
            }

            return new ScreenshotTranslationResult(
                TranslationResultStatus.Ok,
                sourceLanguage,
                sourceLanguageCode,
                translation.Trim());
        }
        catch (TranslationClientException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The screenshot translation response is malformed.", exception);
        }
    }

    private static NormalizedContent NormalizeContent(string content)
    {
        if (content is null)
        {
            return new NormalizedContent(string.Empty, false);
        }

        string candidate = content.Trim();
        bool startsWithFence = candidate.StartsWith("```", StringComparison.Ordinal);
        bool endsWithFence = candidate.EndsWith("```", StringComparison.Ordinal);

        if (!startsWithFence && !endsWithFence)
        {
            if (candidate.Contains("```", StringComparison.Ordinal))
            {
                throw InvalidResponse("The model returned malformed fenced content.");
            }

            return new NormalizedContent(candidate, false);
        }

        if (!startsWithFence || !endsWithFence)
        {
            throw InvalidResponse("The model returned malformed fenced content.");
        }

        int firstLineBreak = candidate.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            throw InvalidResponse("The model returned malformed fenced content.");
        }

        string openingFence = candidate[..firstLineBreak].TrimEnd('\r');
        string fenceLanguage = openingFence[3..].Trim();
        string inner = candidate[(firstLineBreak + 1)..^3].Trim();
        if (inner.Contains("```", StringComparison.Ordinal))
        {
            throw InvalidResponse("The model returned malformed fenced content.");
        }

        return new NormalizedContent(
            inner,
            string.Equals(fenceLanguage, "json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeJson(string content)
    {
        string candidate = content.TrimStart();
        return candidate.StartsWith('{') || candidate.StartsWith('[');
    }

    private static bool LooksLikeExplanatoryOrRefusalText(string content)
    {
        string trimmed = content.TrimStart();
        string normalized = trimmed.ToLowerInvariant().Replace('’', '\'');

        if (StartsWithAny(normalized, EnglishExplanationPrefixes) ||
            StartsWithAny(trimmed, ChineseExplanationPrefixes) ||
            StartsWithAny(normalized, EnglishMetaPrefixes) ||
            StartsWithAny(trimmed, ChineseMetaPrefixes))
        {
            return true;
        }

        bool isEnglishRefusal =
            ContainsAny(normalized, EnglishInabilityTerms) &&
            ContainsAny(normalized, EnglishResponseTopics);
        bool isChineseRefusal =
            ContainsAny(trimmed, ChineseInabilityTerms) &&
            ContainsAny(trimmed, ChineseResponseTopics);

        return isEnglishRefusal || isChineseRefusal;
    }

    private static bool StartsWithAny(string content, IEnumerable<string> prefixes) =>
        prefixes.Any(prefix => content.StartsWith(prefix, StringComparison.Ordinal));

    private static bool ContainsAny(string content, IEnumerable<string> terms) =>
        terms.Any(term => content.Contains(term, StringComparison.Ordinal));

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetNonEmptyString(JsonElement root, string propertyName, out string value) =>
        TryGetString(root, propertyName, out value) && !string.IsNullOrWhiteSpace(value);

    private static string GetOptionalString(JsonElement root, string propertyName) =>
        TryGetString(root, propertyName, out string value) ? value : string.Empty;

    private static TranslationClientException InvalidResponse(string message, Exception? inner = null) =>
        new(TranslationErrorCode.InvalidResponse, message, inner);

    private readonly record struct NormalizedContent(string Text, bool IsJsonFence);
}
