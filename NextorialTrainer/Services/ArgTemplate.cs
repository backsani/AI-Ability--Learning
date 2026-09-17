using System;
using System.Collections.Generic;
using System.Text;

namespace NextorialTrainer.Services;

/// <summary>
/// Turns an argument template into a real argument list.
///
/// Placeholders are {name}. A group in square brackets is dropped entirely when any
/// placeholder inside it resolves to empty, so "[--model {model}]" leaves no dangling flag.
/// Double quotes group a single argument and "" produces a deliberate empty argument
/// (needed for flags like --tools "").
/// </summary>
public static class ArgTemplate
{
    public static List<string> Build(string template, IReadOnlyDictionary<string, string> values)
    {
        var result = new List<string>();

        foreach (var group in SplitGroups(template))
        {
            var tokens = Tokenize(group.Text);
            var rendered = new List<string>(tokens.Count);
            var groupOk = true;

            foreach (var token in tokens)
            {
                var (text, ok) = Substitute(token.Text, values);
                if (!ok && group.Optional)
                {
                    groupOk = false;
                    break;
                }

                // A token that was written as "" stays as a real empty argument;
                // one that merely collapsed to empty is dropped.
                if (text.Length == 0 && !token.Quoted) continue;

                rendered.Add(text);
            }

            if (groupOk) result.AddRange(rendered);
        }

        return result;
    }

    private readonly record struct Group(string Text, bool Optional);

    private static List<Group> SplitGroups(string template)
    {
        var groups = new List<Group>();
        var buffer = new StringBuilder();
        var depth = 0;

        foreach (var c in template)
        {
            if (c == '[' && depth == 0)
            {
                if (buffer.Length > 0) groups.Add(new Group(buffer.ToString(), false));
                buffer.Clear();
                depth = 1;
                continue;
            }

            if (c == ']' && depth == 1)
            {
                groups.Add(new Group(buffer.ToString(), true));
                buffer.Clear();
                depth = 0;
                continue;
            }

            buffer.Append(c);
        }

        if (buffer.Length > 0) groups.Add(new Group(buffer.ToString(), depth == 1));
        return groups;
    }

    private readonly record struct Token(string Text, bool Quoted);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var buffer = new StringBuilder();
        var inQuotes = false;
        var quotedToken = false;
        var started = false;

        void Flush()
        {
            if (started) tokens.Add(new Token(buffer.ToString(), quotedToken));
            buffer.Clear();
            quotedToken = false;
            started = false;
        }

        foreach (var c in text)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                quotedToken = true;
                started = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                Flush();
                continue;
            }

            buffer.Append(c);
            started = true;
        }

        Flush();
        return tokens;
    }

    /// <summary>Returns the substituted text, and false when a placeholder resolved to empty.</summary>
    private static (string Text, bool AllFilled) Substitute(string token, IReadOnlyDictionary<string, string> values)
    {
        if (token.IndexOf('{') < 0) return (token, true);

        var sb = new StringBuilder();
        var allFilled = true;

        for (var i = 0; i < token.Length; i++)
        {
            if (token[i] != '{')
            {
                sb.Append(token[i]);
                continue;
            }

            var end = token.IndexOf('}', i + 1);
            if (end < 0)
            {
                sb.Append(token[i]);
                continue;
            }

            var key = token.Substring(i + 1, end - i - 1);
            values.TryGetValue(key, out var value);
            if (string.IsNullOrEmpty(value)) allFilled = false;

            sb.Append(value ?? "");
            i = end;
        }

        return (sb.ToString(), allFilled);
    }
}
