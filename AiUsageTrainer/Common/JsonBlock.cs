using System;
using System.Text.Json;

namespace AiUsageTrainer.Common;

/// <summary>
/// Model replies are asked to contain exactly one JSON object (usually inside a ```json fence).
/// Models occasionally add a stray sentence around it, so we extract the first balanced object
/// instead of trusting the whole payload to parse.
/// </summary>
public static class JsonBlock
{
    public static string? Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var fence = FindFenced(text);
        if (fence is not null && IsParsable(fence)) return fence;

        var balanced = FindBalanced(text);
        if (balanced is not null && IsParsable(balanced)) return balanced;

        return null;
    }

    public static bool IsParsable(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static string? FindFenced(string text)
    {
        var start = text.IndexOf("```", StringComparison.Ordinal);
        while (start >= 0)
        {
            var lineEnd = text.IndexOf('\n', start);
            if (lineEnd < 0) return null;
            var end = text.IndexOf("```", lineEnd, StringComparison.Ordinal);
            if (end < 0) return null;

            var body = text.Substring(lineEnd + 1, end - lineEnd - 1).Trim();
            if (body.StartsWith('{')) return body;

            start = text.IndexOf("```", end + 3, StringComparison.Ordinal);
        }
        return null;
    }

    private static string? FindBalanced(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start + 1);
            }
        }
        return null;
    }
}
