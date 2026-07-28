using System.Text.Json.Nodes;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Core.Translation;

namespace ScreenshotTranslation.Infrastructure.Translation;

public static class OpenAiRequestFactory
{
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "model",
        "messages",
        "stream",
        "enable_thinking",
        "temperature",
        "max_tokens"
    };

    public static JsonObject CreateScreenshotRequest(
        ModelSettings settings,
        string imageDataUrl,
        string targetLanguageCode)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDataUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguageCode);

        var prompt = TranslationPrompts.CreateScreenshotPrompt(targetLanguageCode);
        var content = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = prompt
            },
            new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject
                {
                    ["url"] = imageDataUrl
                }
            }
        };

        return CreateRequest(settings, new JsonObject
        {
            ["role"] = "user",
            ["content"] = content
        });
    }

    public static JsonObject CreateReplyRequest(
        ModelSettings settings,
        string input,
        string targetLanguageCode)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return CreateRequest(settings, new JsonObject
        {
            ["role"] = "user",
            ["content"] = TranslationPrompts.CreateReplyPrompt(input, targetLanguageCode)
        });
    }

    public static JsonObject CreateConnectionTestRequest(ModelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return CreateRequest(settings, new JsonObject
        {
            ["role"] = "user",
            ["content"] = TranslationPrompts.ConnectionTest
        });
    }

    private static JsonObject CreateRequest(ModelSettings settings, JsonObject message)
    {
        var request = new JsonObject
        {
            ["model"] = settings.ModelName,
            ["messages"] = new JsonArray(message),
            ["stream"] = false,
            ["enable_thinking"] = settings.EnableThinking,
            ["temperature"] = settings.Temperature,
            ["max_tokens"] = settings.MaxOutputTokens
        };

        MergeExtraParameters(request, settings.ExtraParametersJson);
        return request;
    }

    private static void MergeExtraParameters(JsonObject request, string extraParametersJson)
    {
        JsonObject extraParameters;

        try
        {
            extraParameters = JsonNode.Parse(extraParametersJson) as JsonObject
                ?? throw new ArgumentException(
                    "Extra parameters must be a JSON object.",
                    nameof(extraParametersJson));
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new ArgumentException(
                "Extra parameters must be a valid JSON object.",
                nameof(extraParametersJson),
                exception);
        }

        foreach ((string key, JsonNode? value) in extraParameters)
        {
            if (!ReservedKeys.Contains(key))
            {
                request[key] = value?.DeepClone();
            }
        }
    }
}
