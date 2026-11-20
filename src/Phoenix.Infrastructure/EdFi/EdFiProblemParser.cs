using System.Text.Json;
using Phoenix.Core.Modules.EdFi;

namespace Phoenix.Infrastructure.EdFi;

/// <summary>
/// Parses an Ed-Fi ODS/API error body into <see cref="EdFiProblem"/>.
/// ODS/API 7.3 sends RFC 7807 Problem Details as application/json. A body
/// that is not JSON (the App Service cold-start page, an empty 5xx) still
/// yields a problem carrying the HTTP status, never the raw HTML.
/// </summary>
internal static class EdFiProblemParser
{
    public static EdFiProblem Parse(int statusCode, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    return new EdFiProblem(
                        GetString(root, "type"),
                        GetString(root, "title"),
                        GetInt(root, "status") ?? statusCode,
                        GetString(root, "detail"),
                        GetString(root, "correlationId"),
                        GetStringList(root, "errors"),
                        GetValidationErrors(root));
                }
            }
            catch (JsonException)
            {
            }
        }

        var detail = string.IsNullOrWhiteSpace(body)
            ? null
            : $"Non-JSON response body ({body.Length} chars).";

        return new EdFiProblem(
            null,
            null,
            statusCode,
            detail,
            null,
            [],
            new Dictionary<string, IReadOnlyList<string>>());
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
                ? number
                : null;

    private static IReadOnlyList<string> GetStringList(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return [];

        return ReadMessages(value);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> GetValidationErrors(JsonElement root)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        if (root.TryGetProperty("validationErrors", out var errors)
            && errors.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in errors.EnumerateObject())
            {
                var messages = ReadMessages(field.Value);
                if (messages.Count > 0)
                    result[field.Name] = messages;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ReadMessages(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return [value.GetString()!];

        if (value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }
}
