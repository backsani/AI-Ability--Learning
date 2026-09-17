using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace AiUsageTrainer.Views;

/// <summary>
/// Renders the subset of markdown problem statements use — headings, bullet and numbered
/// lists, fenced code, block quotes, rules, and inline bold/code — as native controls.
/// Colors come from the app's classes/styles, so both themes work without extra code.
/// </summary>
public sealed class MarkdownView : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Text));

    private readonly StackPanel _root = new() { Spacing = 8 };

    public MarkdownView()
    {
        Content = _root;
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) Rebuild();
    }

    private void Rebuild()
    {
        _root.Children.Clear();
        var text = Text ?? "";
        if (text.Length == 0) return;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var paragraph = new StringBuilder();
        var i = 0;

        void FlushParagraph()
        {
            if (paragraph.Length == 0) return;
            _root.Children.Add(Paragraph(paragraph.ToString().Trim(), "md-p"));
            paragraph.Clear();
        }

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Fenced code: everything up to the closing fence, verbatim.
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    code.AppendLine(lines[i]);
                    i++;
                }
                i++; // closing fence
                _root.Children.Add(CodeBlock(code.ToString().TrimEnd()));
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                i++;
                continue;
            }

            if (trimmed is "---" or "***" or "___")
            {
                FlushParagraph();
                _root.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 6),
                    Classes = { "md-rule" }
                });
                i++;
                continue;
            }

            var level = HeadingLevel(trimmed);
            if (level > 0)
            {
                FlushParagraph();
                var heading = Paragraph(trimmed[level..].Trim(), level switch
                {
                    1 => "md-h1",
                    2 => "md-h2",
                    _ => "md-h3"
                });
                heading.Margin = new Thickness(0, level == 1 ? 10 : 8, 0, 0);
                _root.Children.Add(heading);
                i++;
                continue;
            }

            if (trimmed.StartsWith("> ", StringComparison.Ordinal) || trimmed == ">")
            {
                FlushParagraph();
                var quote = new StringBuilder();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
                {
                    quote.AppendLine(lines[i].TrimStart().TrimStart('>').Trim());
                    i++;
                }
                var block = Paragraph(quote.ToString().Trim(), "md-quote");
                _root.Children.Add(new Border
                {
                    Classes = { "md-quote-border" },
                    Padding = new Thickness(12, 6),
                    Child = block
                });
                continue;
            }

            if (TryListItem(line, out var indent, out var marker, out var body))
            {
                FlushParagraph();
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                row.Margin = new Thickness(indent * 16, 0, 0, 0);

                var bullet = new TextBlock
                {
                    Text = marker,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    Classes = { "md-bullet" }
                };
                Grid.SetColumn(bullet, 0);

                var content = Paragraph(body, "md-p");
                Grid.SetColumn(content, 1);

                row.Children.Add(bullet);
                row.Children.Add(content);
                _root.Children.Add(row);
                i++;
                continue;
            }

            if (paragraph.Length > 0) paragraph.Append(' ');
            paragraph.Append(trimmed);
            i++;
        }

        FlushParagraph();
    }

    private static int HeadingLevel(string s)
    {
        var n = 0;
        while (n < s.Length && n < 6 && s[n] == '#') n++;
        return n > 0 && n < s.Length && s[n] == ' ' ? n : 0;
    }

    private static bool TryListItem(string line, out int indent, out string marker, out string body)
    {
        indent = 0;
        marker = "";
        body = "";

        var spaces = 0;
        while (spaces < line.Length && line[spaces] == ' ') spaces++;
        var rest = line[spaces..];

        if (rest.StartsWith("- ", StringComparison.Ordinal) || rest.StartsWith("* ", StringComparison.Ordinal))
        {
            indent = spaces / 2;
            marker = "•";
            body = rest[2..];
            return true;
        }

        var dot = rest.IndexOf(". ", StringComparison.Ordinal);
        if (dot > 0 && dot <= 3 && int.TryParse(rest[..dot], out _))
        {
            indent = spaces / 2;
            marker = rest[..(dot + 1)];
            body = rest[(dot + 2)..];
            return true;
        }

        return false;
    }

    private static Control CodeBlock(string code)
        => new Border
        {
            Classes = { "md-code" },
            Padding = new Thickness(12, 10),
            Child = new SelectableTextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        };

    private static SelectableTextBlock Paragraph(string text, string cls)
    {
        var block = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Classes = { cls }
        };
        foreach (var inline in ParseInlines(text)) block.Inlines!.Add(inline);
        return block;
    }

    /// <summary>**bold** and `code`; anything else is plain text.</summary>
    private static IEnumerable<Inline> ParseInlines(string text)
    {
        var buffer = new StringBuilder();
        var i = 0;

        Run Flush()
        {
            var run = new Run(buffer.ToString());
            buffer.Clear();
            return run;
        }

        while (i < text.Length)
        {
            if (text.AsSpan(i).StartsWith("**"))
            {
                var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    if (buffer.Length > 0) yield return Flush();
                    yield return new Bold { Inlines = { new Run(text[(i + 2)..end]) } };
                    i = end + 2;
                    continue;
                }
            }

            if (text[i] == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    if (buffer.Length > 0) yield return Flush();
                    yield return new Run(text[(i + 1)..end])
                    {
                        FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                        Classes = { "md-inline-code" }
                    };
                    i = end + 1;
                    continue;
                }
            }

            buffer.Append(text[i]);
            i++;
        }

        if (buffer.Length > 0) yield return Flush();
    }
}
