using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace AiChatExport;

internal static class ImageEmbed
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
    };

    public static bool TryAccept(string? media, string payload, out string acceptedMedia)
    {
        acceptedMedia = string.IsNullOrEmpty(media) ? "image/png" : media;
        if (!Allowed.Contains(acceptedMedia) || !IsBase64(payload))
        {
            acceptedMedia = "";
            return false;
        }

        return true;
    }

    public static string Html(string? media, string payload)
    {
        if (!TryAccept(media, payload, out var accepted))
        {
            return "";
        }

        return $"<img alt=\"attachment\" src=\"data:{WebUtility.HtmlEncode(accepted)};base64,{WebUtility.HtmlEncode(payload)}\">";
    }

    public static string Markdown(string? media, string payload)
    {
        if (!TryAccept(media, payload, out var accepted))
        {
            return "";
        }

        return $"![attachment](data:{accepted};base64,{payload})";
    }

    private static bool IsBase64(string payload)
    {
        if (payload.Length == 0 || payload.Length % 4 != 0)
        {
            return false;
        }

        var padding = 0;
        foreach (var character in payload)
        {
            if (character == '=')
            {
                padding++;
                if (padding > 2)
                {
                    return false;
                }

                continue;
            }

            if (padding > 0)
            {
                return false;
            }

            var allowed = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '+' or '/';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}

internal static class MarkdownRenderer
{
    private static readonly Dictionary<string, string> Headings = new(StringComparer.Ordinal)
    {
        ["you"] = "User",
        ["agent"] = "Agent",
        ["system"] = "System",
        ["context"] = "Context",
        ["reasoning"] = "Reasoning",
        ["tool_output"] = "Tool output",
    };

    public static string Render(Transcript transcript, string title, string workspace, string timestamp)
    {
        var parts = new List<string> { $"# {SingleLine(title)}", "", $"{SingleLine(workspace)} · {SingleLine(timestamp)}", "" };
        foreach (var block in transcript.Blocks)
        {
            var heading = Headings.GetValueOrDefault(block.Kind, block.Kind);
            if (block.Timestamp.Length > 0 && block.Kind is "you" or "agent")
            {
                heading = $"{heading} · {block.Timestamp}";
            }

            parts.Add($"## {heading}");
            parts.Add("");
            if (block.Pieces.Count > 0)
            {
                parts.AddRange(PieceMarkdown(block));
                continue;
            }

            if (block.Text.Length > 0)
            {
                parts.Add(block.Text);
                parts.Add("");
            }

            if (block.Detail.Length > 0)
            {
                parts.Add(block.Detail);
                parts.Add("");
            }

            foreach (var caption in block.Captions)
            {
                parts.Add($"- {caption}");
            }

            if (block.Captions.Count > 0)
            {
                parts.Add("");
            }

            foreach (var (media, data) in block.Images)
            {
                var image = ImageEmbed.Markdown(media, data);
                if (image.Length == 0)
                {
                    continue;
                }

                parts.Add(image);
                parts.Add("");
            }
        }

        if (transcript.Footer.Count > 0)
        {
            parts.Add("## Notes");
            parts.Add("");
            foreach (var line in transcript.Footer)
            {
                parts.Add($"- {line}");
            }

            parts.Add("");
        }

        return string.Join("\n", parts).TrimEnd() + "\n";
    }

    private static string SingleLine(string value)
    {
        return value.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
    }

    private static List<string> PieceMarkdown(Block block)
    {
        var parts = new List<string>();
        foreach (var piece in block.Pieces)
        {
            if (piece.Kind == "text" && piece.Text.Length > 0)
            {
                parts.Add(piece.Text);
                parts.Add("");
            }
            else if (piece.Kind == "caption")
            {
                parts.Add($"- {piece.Text}");
                parts.Add("");
            }
            else if (piece.Kind == "image")
            {
                var image = ImageEmbed.Markdown(piece.Media, piece.Text);
                if (image.Length == 0)
                {
                    continue;
                }

                parts.Add(image);
                parts.Add("");
            }
            else if (piece.Kind == "reasoning")
            {
                parts.Add("### Reasoning");
                parts.Add("");
                parts.Add(piece.Text);
                parts.Add("");
            }
            else if (piece.Kind == "tool_output")
            {
                parts.Add("### Tool output");
                parts.Add("");
                parts.Add(piece.Text);
                parts.Add("");
            }
        }

        return parts;
    }
}

internal static class HtmlRenderer
{
    private const string WebBody = "18px/1.5 Georgia, \"Iowan Old Style\", Palatino, serif";
    private const string WebNav = "\"Segoe UI\", system-ui, sans-serif";
    private const string TerminalBody = "18px/1.5 Consolas, ui-monospace, monospace";
    private const string TerminalNav = "Consolas, ui-monospace, monospace";

    private static readonly Dictionary<string, string> DarkWeb = Theme(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bg"] = "#161616",
            ["text"] = "#f2f2f2",
            ["muted"] = "#a3a3a3",
            ["time"] = "#e0b040",
            ["panel"] = "#222222",
            ["user"] = "#2c261c",
            ["agent"] = "#1e2428",
            ["mark"] = "#e0b040",
            ["rule"] = "#3a3a3a",
            ["code"] = "#101010",
        },
        WebBody,
        WebNav,
        "8px");

    private static readonly Dictionary<string, string> LightWeb = Theme(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bg"] = "#f7f7f4",
            ["text"] = "#1c1c1c",
            ["muted"] = "#5e5e5e",
            ["time"] = "#8a5a00",
            ["panel"] = "#ffffff",
            ["user"] = "#f6efe4",
            ["agent"] = "#f3f5f7",
            ["mark"] = "#8a5a00",
            ["rule"] = "#d9d4cc",
            ["code"] = "#f0ece4",
        },
        WebBody,
        WebNav,
        "8px");

    private static readonly Dictionary<string, string> TerminalDark = Theme(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bg"] = "#071207",
            ["text"] = "#c6f7c0",
            ["muted"] = "#6ea86a",
            ["time"] = "#e2c15a",
            ["panel"] = "#0c1a10",
            ["user"] = "#12301a",
            ["agent"] = "#0b1610",
            ["mark"] = "#e2c15a",
            ["rule"] = "#1e3a24",
            ["code"] = "#040a05",
        },
        TerminalBody,
        TerminalNav,
        "0");

    private static readonly Dictionary<string, string> TerminalLight = Theme(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bg"] = "#e6f5df",
            ["text"] = "#0e3a16",
            ["muted"] = "#3f6b45",
            ["time"] = "#7a5b12",
            ["panel"] = "#f4fbf1",
            ["user"] = "#d7f0c8",
            ["agent"] = "#e7f3e2",
            ["mark"] = "#7a5b12",
            ["rule"] = "#b7d4b0",
            ["code"] = "#f7fff4",
        },
        TerminalBody,
        TerminalNav,
        "0");

    public static readonly string[] ThemeNames =
    [
        "terminal-dark",
        "terminal-light",
        "dark-web",
        "light-web",
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> Themes = new(StringComparer.Ordinal)
    {
        ["terminal-dark"] = TerminalDark,
        ["terminal-light"] = TerminalLight,
        ["dark-web"] = DarkWeb,
        ["light-web"] = LightWeb,
    };

    private static readonly Dictionary<string, string> Headings = new(StringComparer.Ordinal)
    {
        ["you"] = "User",
        ["agent"] = "Agent",
        ["system"] = "System",
        ["context"] = "Context",
        ["reasoning"] = "Reasoning",
        ["tool_output"] = "Tool output",
    };

    public static string Render(
        Transcript transcript,
        string title,
        string workspace,
        string timestamp,
        string theme = "terminal-dark")
    {
        var palette = Themes.TryGetValue(theme, out var found) ? found : Themes["terminal-dark"];
        var turns = transcript.Blocks.Count(block => block.Kind == "you");
        var css = Stylesheet(palette, turns);
        var sections = Sections(transcript.Blocks);
        var navigator = Navigator(transcript.Blocks);
        var footer = Footer(transcript.Footer);
        return "<!DOCTYPE html>\n"
            + "<html lang=\"en\">\n"
            + "<head>\n"
            + "<meta charset=\"utf-8\">\n"
            + $"<title>{WebUtility.HtmlEncode(title)}</title>\n"
            + $"<style>\n{css}\n</style>\n"
            + "</head>\n"
            + "<body>\n"
            + navigator
            + "<article>\n"
            + "<header>\n"
            + $"<h1>{WebUtility.HtmlEncode(title)}</h1>\n"
            + "<p class=\"meta\">"
            + $"<span class=\"workspace\">{WebUtility.HtmlEncode(workspace)}</span>"
            + $"<span class=\"time\">{WebUtility.HtmlEncode(timestamp)}</span>"
            + "</p>\n"
            + "</header>\n"
            + $"{sections}\n"
            + $"{footer}\n"
            + "</article>\n"
            + "</body>\n"
            + "</html>\n";
    }

    public static string RenderAssistant(string text) => Blocks(text);

    private static Dictionary<string, string> Theme(Dictionary<string, string> colors, string body, string nav, string radius)
    {
        colors["body"] = body;
        colors["nav"] = nav;
        colors["radius"] = radius;
        return colors;
    }

    private static string Stylesheet(Dictionary<string, string> palette, int turns)
    {
        var rules = new List<string>
        {
                ":root {",
                $"  --bg: {palette["bg"]};",
                $"  --text: {palette["text"]};",
                $"  --muted: {palette["muted"]};",
                $"  --time: {palette["time"]};",
                $"  --panel: {palette["panel"]};",
                $"  --user: {palette["user"]};",
                $"  --agent: {palette["agent"]};",
                $"  --mark: {palette["mark"]};",
                $"  --rule: {palette["rule"]};",
                $"  --code: {palette["code"]};",
                "}",
                "body {",
                "  margin: 0;",
                "  background: var(--bg);",
                "  color: var(--text);",
                $"  font: {palette["body"]};",
                "}",
                "article { max-width: 44rem; margin: 0 auto; padding: 2.5rem 1.5rem 4rem; }",
                "header { border-bottom: 1px solid var(--rule); margin-bottom: 1.5rem; }",
                "h1 { font-size: 1.8rem; font-weight: 600; margin: 0 0 0.4rem; }",
                "h2 { font-size: 0.85rem; letter-spacing: 0.04em; text-transform: uppercase; color: var(--muted); margin: 0 0 0.6rem; }",
                ".meta { display: flex; justify-content: space-between; gap: 1rem; color: var(--muted); }",
                ".time { color: var(--time); }",
                $"section {{ background: var(--panel); border: 1px solid var(--rule); border-radius: {palette["radius"]}; padding: 1rem 1.1rem; margin: 0 0 1rem; }}",
                "section.you { background: var(--user); border-left: 3px solid var(--mark); scroll-margin-top: 5rem; }",
                "section.you:target { outline: 2px solid var(--mark); }",
                "section.agent { background: var(--agent); }",
                "section.system, section.context { background: var(--panel); }",
                "nav { position: sticky; top: 0; overflow: auto; max-height: 40vh; background: var(--bg); border-bottom: 1px solid var(--rule); padding: 0.8rem 1.2rem; font-family: "
                + palette["nav"]
                + "; }",
                "nav ol { list-style: none; margin: 0; padding: 0; }",
                "nav a { display: flex; flex-direction: column; gap: 0; min-width: 0; color: var(--text); text-decoration: none; }",
                "nav li { display: flex; align-items: flex-start; gap: 0.5rem; }",
                "nav li + li { margin-top: 0.85rem; }",
                "nav .label { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }",
                "nav .time { font-size: 10px; line-height: 1; white-space: nowrap; }",
                "nav button { display: none; font: inherit; color: var(--text); background: var(--bg); border: 1px solid var(--rule); }",
                "body:has(nav input:checked) nav button { display: block; }",
                "nav form > button:first-child { position: sticky; top: 0; z-index: 1; }",
                "body:has(nav input:checked) article section { display: none; }",
                "@media (min-width: 70rem) {",
                "  body { display: grid; grid-template-columns: 30rem minmax(0, 1fr); align-items: start; }",
                "  nav { max-height: 100vh; border-bottom: 0; border-right: 1px solid var(--rule); }",
                "}",
                ".tool { font-family: Consolas, ui-monospace, monospace; color: var(--mark); margin: 0.35rem 0; }",
                "pre { background: var(--code); padding: 0.8rem 1rem; overflow: auto; }",
                "code { font-family: Consolas, ui-monospace, monospace; }",
                "footer { color: var(--muted); font-size: 0.9rem; }",
                "img { max-width: 100%; }",
        };
        for (var number = 1; number <= turns; number++)
        {
            rules.Add($"body:has(#keep-{number}:checked) article [data-turn=\"{number}\"] {{ display: block; }}");
        }

        return string.Join("\n", rules);
    }

    private static string Sections(IReadOnlyList<Block> blocks)
    {
        var rendered = new List<string>();
        var number = 0;
        var turn = 0;
        foreach (var block in blocks)
        {
            var anchor = "";
            var mark = 0;
            if (block.Kind == "you")
            {
                number++;
                turn = number;
                anchor = $"user-{number}";
                mark = turn;
            }
            else if (block.Kind == "agent" && turn > 0)
            {
                mark = turn;
            }

            rendered.Add(Section(block, anchor, mark));
        }

        return string.Join("\n", rendered);
    }

    private static string Navigator(IReadOnlyList<Block> blocks)
    {
        var items = new List<string>();
        var number = 0;
        foreach (var block in blocks)
        {
            if (block.Kind != "you")
            {
                continue;
            }

            number++;
            var stamp = "";
            if (block.Timestamp.Length > 0)
            {
                stamp = $"<span class=\"time\">{WebUtility.HtmlEncode(CompactNavTime(block.Timestamp))}</span>";
            }

            var label = WebUtility.HtmlEncode(NavLabel(block));
            items.Add(
                "<li>"
                + $"<input type=\"checkbox\" id=\"keep-{number.ToString(CultureInfo.InvariantCulture)}\">"
                + $"<a href=\"#user-{number.ToString(CultureInfo.InvariantCulture)}\">{stamp}<span class=\"label\">{label}</span></a>"
                + "</li>");
        }

        var body = string.Join("\n", items);
        const string clear = "<button type=\"reset\">Clear filter</button>";
        return $"<nav>\n<form>\n{clear}\n<ol>\n{body}\n</ol>\n{clear}\n</form>\n</nav>\n";
    }

    private static string CompactNavTime(string stamp)
    {
        var match = Regex.Match(
            stamp,
            @"^(?<month>[A-Za-z]{3}) (?<day>\d{1,2}), (?<year>\d{4}) (?<hour>\d{2}):(?<minute>\d{2}) (?<ampm>AM|PM)$");
        if (!match.Success)
        {
            return stamp;
        }

        if (!DateTime.TryParseExact(
                match.Groups["month"].Value,
                "MMM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var monthDate))
        {
            return stamp;
        }

        var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var ampm = match.Groups["ampm"].Value;
        if (ampm == "PM" && hour != 12)
        {
            hour += 12;
        }

        if (ampm == "AM" && hour == 12)
        {
            hour = 0;
        }

        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture) % 100;
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        return $"{year:00}-{monthDate.Month:00}-{day:00} {hour:00}:{minute:00}";
    }

    private static string NavLabel(Block block)
    {
        var text = block.Text.Trim();
        if (text.Length > 0)
        {
            var split = text.Split('\n', 2);
            return split[0].Trim();
        }

        if (block.Images.Count > 0)
        {
            return "Image";
        }

        return "User";
    }

    private static string Section(Block block, string anchor, int turn = 0)
    {
        var heading = Headings.GetValueOrDefault(block.Kind, block.Kind);
        string body;
        if (block.Pieces.Count > 0)
        {
            body = string.Join("\n", block.Pieces.Select(PieceHtml).Where(part => part.Length > 0));
        }
        else
        {
            var prose = block.Kind == "agent" ? RenderAssistant(block.Text) : PlainParagraphs(block.Text);
            var detail = PlainParagraphs(block.Detail);
            var captions = string.Join(
                "\n",
                block.Captions.Select(caption => $"<p class=\"tool\">&gt; {WebUtility.HtmlEncode(caption)}</p>"));
            var images = string.Join("\n", block.Images.Select(image => ImageHtml(image.Media, image.Data)));
            body = string.Join("\n", new[] { prose, detail, captions, images }.Where(part => part.Length > 0));
        }

        var ident = anchor.Length > 0 ? $" id=\"{WebUtility.HtmlEncode(anchor)}\"" : "";
        var marked = turn > 0 ? $" data-turn=\"{turn.ToString(CultureInfo.InvariantCulture)}\"" : "";
        return $"<section class=\"{WebUtility.HtmlEncode(block.Kind)}\"{ident}{marked}>\n<h2>{Heading(block, heading)}</h2>\n{body}\n</section>";
    }

    private static string Heading(Block block, string label)
    {
        if (block.Timestamp.Length > 0 && block.Kind is "you" or "agent")
        {
            return $"{WebUtility.HtmlEncode(label)} <span class=\"time\">{WebUtility.HtmlEncode(block.Timestamp)}</span>";
        }

        return WebUtility.HtmlEncode(label);
    }

    private static string PieceHtml(Piece piece)
    {
        if (piece.Kind == "text")
        {
            return RenderAssistant(piece.Text);
        }

        if (piece.Kind == "caption")
        {
            return $"<p class=\"tool\">&gt; {WebUtility.HtmlEncode(piece.Text)}</p>";
        }

        if (piece.Kind == "image")
        {
            return ImageHtml(piece.Media, piece.Text);
        }

        var label = piece.Kind == "reasoning" ? "Reasoning" : "Tool output";
        return $"<h3>{label}</h3>\n{PlainParagraphs(piece.Text)}";
    }

    private static string ImageHtml(string media, string data)
    {
        return ImageEmbed.Html(media, data);
    }

    private static string Footer(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return "";
        }

        var items = string.Join("\n", lines.Select(line => $"<p>{WebUtility.HtmlEncode(line)}</p>"));
        return $"<footer>\n{items}\n</footer>";
    }

    private static string PlainParagraphs(string text)
    {
        if (text.Length == 0)
        {
            return "";
        }

        var chunks = Regex.Split(text, @"\n\s*\n");
        return string.Join(
            "\n",
            chunks.Where(chunk => chunk.Length > 0).Select(chunk => $"<p>{WebUtility.HtmlEncode(chunk).Replace("\n", "<br>")}</p>"));
    }

    private static string Blocks(string text)
    {
        if (text.Length == 0)
        {
            return "";
        }

        var lines = text.Split('\n');
        var pieces = new List<string>();
        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                index++;
                var buffer = new List<string>();
                while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    buffer.Add(lines[index]);
                    index++;
                }

                if (index < lines.Length)
                {
                    index++;
                }

                pieces.Add("<pre><code>" + WebUtility.HtmlEncode(string.Join("\n", buffer)) + "</code></pre>");
                continue;
            }

            if (line.StartsWith("    ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                var buffer = new List<string>();
                while (index < lines.Length && lines[index].StartsWith("    ", StringComparison.Ordinal))
                {
                    buffer.Add(lines[index][4..]);
                    index++;
                }

                pieces.Add("<pre><code>" + WebUtility.HtmlEncode(string.Join("\n", buffer)) + "</code></pre>");
                continue;
            }

            if (Regex.IsMatch(line, @"^(\*|-|\d+\.) "))
            {
                var items = new List<string>();
                while (index < lines.Length && Regex.IsMatch(lines[index], @"^(\*|-|\d+\.) "))
                {
                    var item = Regex.Replace(lines[index], @"^(\*|-|\d+\.) ", "");
                    items.Add("<li>" + Inline(item) + "</li>");
                    index++;
                }

                pieces.Add("<ul>" + string.Join("", items) + "</ul>");
                continue;
            }

            if (line.Trim().Length == 0)
            {
                index++;
                continue;
            }

            var paragraph = new List<string> { line };
            index++;
            while (index < lines.Length && lines[index].Trim().Length > 0 && !BlockStart(lines[index]))
            {
                paragraph.Add(lines[index]);
                index++;
            }

            pieces.Add("<p>" + Inline(string.Join(" ", paragraph)) + "</p>");
        }

        return string.Join("\n", pieces);
    }

    private static bool BlockStart(string line)
    {
        return line.StartsWith("```", StringComparison.Ordinal)
            || line.StartsWith("    ", StringComparison.Ordinal)
            || Regex.IsMatch(line, @"^(\*|-|\d+\.) ");
    }

    private static string Inline(string text)
    {
        var parts = Regex.Split(text, "(`[^`]+`)");
        var rendered = new List<string>();
        foreach (var part in parts)
        {
            if (part.Length >= 2 && part.StartsWith('`') && part.EndsWith('`'))
            {
                rendered.Add("<code>" + WebUtility.HtmlEncode(part[1..^1]) + "</code>");
            }
            else
            {
                rendered.Add(WebUtility.HtmlEncode(part));
            }
        }

        return string.Join("", rendered);
    }
}
