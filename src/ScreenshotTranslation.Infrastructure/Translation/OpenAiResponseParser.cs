using System.Text.Json;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.Infrastructure.Translation;

public static class OpenAiResponseParser
{
    public static ScreenshotTranslationResult ParseScreenshotContent(string content)
    {
        string candidate = StripOptionalFence(content);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw InvalidResponse("The model returned empty translation content.");
        }

        if (LooksLikeJson(candidate))
        {
            return ParseScreenshotJson(candidate);
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

        string translation = StripOptionalFence(content).Trim();
        if (string.IsNullOrWhiteSpace(translation) || LooksLikeJson(translation))
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

    private static string StripOptionalFence(string content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        string candidate = content.Trim();
        if (!candidate.StartsWith("```", StringComparison.Ordinal) ||
            !candidate.EndsWith("```", StringComparison.Ordinal))
        {
            return candidate;
        }

        int firstLineBreak = candidate.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return string.Empty;
        }

        return candidate[(firstLineBreak + 1)..^3].Trim();
    }

    private static bool LooksLikeJson(string content)
    {
        string candidate = content.TrimStart();
        return candidate.StartsWith('{') || candidate.StartsWith('[');
    }

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
}
