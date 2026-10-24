using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiChatExport;

public sealed class Piece
{
    public Piece(string kind, string text, string media = "")
    {
        Kind = kind;
        Text = text;
        Media = media;
    }

    public string Kind { get; }

    public string Text { get; }

    public string Media { get; }
}

public sealed class Block
{
    public Block(
        string kind,
        string text = "",
        IReadOnlyList<string>? captions = null,
        IReadOnlyList<(string Media, string Data)>? images = null,
        IReadOnlyList<Piece>? pieces = null,
        string timestamp = "",
        string detail = "")
    {
        Kind = kind;
        Text = text;
        Captions = captions ?? [];
        Images = images ?? [];
        Pieces = pieces ?? [];
        Timestamp = timestamp;
        Detail = detail;
    }

    public string Kind { get; }

    public string Text { get; }

    public IReadOnlyList<string> Captions { get; }

    public IReadOnlyList<(string Media, string Data)> Images { get; }

    public IReadOnlyList<Piece> Pieces { get; }

    public string Timestamp { get; }

    public string Detail { get; }
}

public sealed class Transcript
{
    public List<Block> Blocks { get; } = [];

    public List<string> Footer { get; } = [];
}

public static class TranscriptBuilder
{
    private static readonly Regex TurnTime = new(
        @"^(?<weekday>[A-Za-z]+), (?<month>[A-Za-z]{3}) (?<day>\d{1,2}), (?<year>\d{4}), (?<hour>\d{1,2}):(?<minute>\d{2}) (?<ampm>AM|PM) \(UTC[+-]\d+\)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Element = new(
        @"</?[A-Za-z_][\w.-]*>",
        RegexOptions.CultureInvariant);

    public static Transcript Build(
        IEnumerable<JsonObject> messages,
        IReadOnlyList<string> prompts,
        bool verbose = false,
        bool showTools = false,
        bool showDetail = false,
        IReadOnlyList<string>? skipped = null,
        IReadOnlyList<string>? unlinkedImages = null)
    {
        var includeCaptions = verbose || showTools;
        var transcript = new Transcript();
        List<Piece>? stretch = null;
        var stretchTime = "";

        void Flush()
        {
            if (stretch is null || stretch.Count == 0)
            {
                stretch = null;
                return;
            }

            var text = string.Join(
                "\n\n",
                stretch.Where(piece => piece.Kind == "text" && piece.Text.Length > 0).Select(piece => piece.Text));
            var captions = stretch.Where(piece => piece.Kind == "caption").Select(piece => piece.Text).ToList();
            var images = stretch.Where(piece => piece.Kind == "image").Select(piece => (piece.Media, piece.Text)).ToList();
            transcript.Blocks.Add(new Block(
                "agent",
                text,
                captions,
                images,
                stretch.ToList(),
                stretchTime));
            stretch = null;
        }

        void Ensure()
        {
            stretch ??= [];
        }

        foreach (var message in messages)
        {
            var role = message["role"]?.GetValue<string>();
            var content = message["content"];
            if (role == "system")
            {
                if (verbose)
                {
                    Flush();
                    transcript.Blocks.Add(new Block("system", text: PlainText(content)));
                }

                continue;
            }

            if (role == "user")
            {
                var parts = TextParts(content);
                var (typed, context) = SplitUserText(parts, prompts);
                var images = ImageParts(content);
                var query = QueryText(typed);
                var detail = showDetail ? DetailText(parts) : "";
                var turnTime = TurnTimestamp(parts);
                if (query.Length > 0 || images.Count > 0 || detail.Length > 0)
                {
                    Flush();
                    stretchTime = turnTime;
                    transcript.Blocks.Add(new Block(
                        "you",
                        query,
                        images: images,
                        timestamp: turnTime,
                        detail: detail));
                }

                if (verbose)
                {
                    var contextBody = ContextText(PreambleParts(typed).Concat(context).ToList(), showDetail);
                    if (contextBody.Length > 0)
                    {
                        transcript.Blocks.Add(new Block("context", text: contextBody));
                    }
                }

                continue;
            }

            if (role == "assistant")
            {
                var text = Joined(content, "text");
                var captions = includeCaptions
                    ? Parts(content, "tool-call").Select(Caption).ToList()
                    : [];
                var reasoning = verbose ? Joined(content, "reasoning") : "";
                var images = ImageParts(content);
                if (text.Length == 0 && captions.Count == 0 && reasoning.Length == 0 && images.Count == 0)
                {
                    continue;
                }

                Ensure();
                if (text.Length > 0)
                {
                    stretch!.Add(new Piece("text", text));
                }

                foreach (var label in captions)
                {
                    stretch!.Add(new Piece("caption", label));
                }

                if (reasoning.Length > 0)
                {
                    stretch!.Add(new Piece("reasoning", reasoning));
                }

                foreach (var (media, data) in images)
                {
                    stretch!.Add(new Piece("image", data, media));
                }

                continue;
            }

            if (role == "tool" && verbose)
            {
                var result = ToolResult(content);
                if (result.Length == 0)
                {
                    continue;
                }

                Ensure();
                stretch!.Add(new Piece("tool_output", result));
            }
        }

        Flush();
        foreach (var blobId in skipped ?? [])
        {
            transcript.Footer.Add($"Skipped unrecognized data {blobId}");
        }

        foreach (var blobId in unlinkedImages ?? [])
        {
            transcript.Footer.Add($"Image not attached to a turn: {blobId}");
        }

        return transcript;
    }

    public static (List<string> Typed, List<string> Context) SplitUserText(
        IReadOnlyList<string> parts,
        IReadOnlyList<string> prompts)
    {
        var usable = prompts.Where(prompt => prompt.Trim().Length > 0).ToList();
        if (usable.Count == 0)
        {
            if (parts.Count == 0)
            {
                return ([], []);
            }

            return ([parts[^1]], parts.Take(parts.Count - 1).ToList());
        }

        var typed = new List<string>();
        var context = new List<string>();
        foreach (var part in parts)
        {
            if (usable.Any(prompt => part.Contains(prompt, StringComparison.Ordinal)))
            {
                typed.Add(part);
            }
            else
            {
                context.Add(part);
            }
        }

        if (typed.Count > 0)
        {
            return (typed, context);
        }

        if (parts.Count == 0)
        {
            return ([], []);
        }

        return ([parts[^1]], parts.Take(parts.Count - 1).ToList());
    }

    public static string QueryText(IReadOnlyList<string> typed)
    {
        var extracted = new List<string>();
        var sawQuery = false;
        foreach (var part in typed)
        {
            var found = ElementTexts(part, "user_query");
            if (found.Count > 0)
            {
                sawQuery = true;
                extracted.AddRange(found);
            }
        }

        if (sawQuery)
        {
            return string.Join("\n\n", extracted);
        }

        var plain = typed.Where(part => part.Trim().Length > 0 && !Element.IsMatch(part)).Select(part => part.Trim());
        return string.Join("\n\n", plain);
    }

    public static string DetailText(IReadOnlyList<string> parts)
    {
        var chunks = new List<string>();
        foreach (var name in new[] { "conversation_summary", "cursor_commands" })
        {
            foreach (var part in parts)
            {
                chunks.AddRange(ElementTexts(part, name));
            }
        }

        return string.Join("\n\n", chunks);
    }

    public static string TurnTimestamp(IReadOnlyList<string> parts)
    {
        foreach (var part in parts)
        {
            var found = ElementTexts(part, "timestamp");
            if (found.Count > 0)
            {
                return FormatTurnTime(found[0]);
            }
        }

        return "";
    }

    public static List<string> ElementTexts(string text, string name)
    {
        var pattern = new Regex($"<{Regex.Escape(name)}>(.*?)</{Regex.Escape(name)}>", RegexOptions.Singleline);
        return pattern.Matches(text).Select(match => match.Groups[1].Value.Trim()).Where(value => value.Length > 0).ToList();
    }

    public static string StripElement(string text, string name)
    {
        var pattern = new Regex($"<{Regex.Escape(name)}>.*?</{Regex.Escape(name)}>", RegexOptions.Singleline);
        return pattern.Replace(text, "").Trim();
    }

    public static string FormatTurnTime(string raw)
    {
        var text = string.Join(" ", raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var match = TurnTime.Match(text);
        if (!match.Success)
        {
            return text;
        }

        if (!DateTime.TryParseExact(
                match.Groups["month"].Value,
                "MMM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var monthDate))
        {
            return text;
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

        var moment = new DateTime(
            int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture),
            monthDate.Month,
            int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture),
            hour,
            int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture),
            0);
        return Catalog.FormatDateTime(moment);
    }

    public static string Caption(JsonObject part)
    {
        var name = part["toolName"]?.GetValue<string>() ?? "tool";
        if (string.IsNullOrEmpty(name))
        {
            name = "tool";
        }

        var args = Args(part["args"]);
        if (name == "Read")
        {
            return $"Read {ArgString(args, "path")}".TrimEnd();
        }

        if (name == "Glob")
        {
            return $"Glob {ArgString(args, "glob_pattern")}".TrimEnd();
        }

        if (name == "Shell")
        {
            if (args["description"] is JsonValue description
                && description.TryGetValue<string>(out var text)
                && text.Trim().Length > 0)
            {
                return $"Ran {text.Trim()}";
            }

            return "Ran command";
        }

        return name;
    }

    private static List<string> PreambleParts(IReadOnlyList<string> typed)
    {
        var extras = new List<string>();
        foreach (var part in typed)
        {
            if (ElementTexts(part, "user_query").Count > 0 || part.Trim().Length == 0 || !Element.IsMatch(part))
            {
                continue;
            }

            var remainder = part;
            foreach (var name in new[] { "conversation_summary", "cursor_commands", "timestamp" })
            {
                remainder = StripElement(remainder, name);
            }

            if (remainder.Trim().Length > 0)
            {
                extras.Add(part);
            }
        }

        return extras;
    }

    private static string ContextText(IReadOnlyList<string> parts, bool hideCommands)
    {
        var chunks = new List<string>();
        foreach (var part in parts)
        {
            var text = part;
            if (hideCommands)
            {
                foreach (var name in new[] { "cursor_commands", "conversation_summary" })
                {
                    text = StripElement(text, name);
                }
            }

            if (text.Trim().Length > 0)
            {
                chunks.Add(text.Trim());
            }
        }

        return string.Join("\n\n", chunks);
    }

    private static JsonObject Args(JsonNode? value)
    {
        if (value is JsonObject obj)
        {
            return obj;
        }

        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            try
            {
                if (JsonNode.Parse(text) is JsonObject parsed)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return [];
    }

    private static string ArgString(JsonObject args, string name)
    {
        if (args[name] is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return "";
    }

    private static List<JsonObject> Parts(JsonNode? content, string partType)
    {
        if (content is not JsonArray array)
        {
            return [];
        }

        return array
            .OfType<JsonObject>()
            .Where(part => part["type"]?.GetValue<string>() == partType)
            .ToList();
    }

    private static List<string> TextParts(JsonNode? content)
    {
        if (content is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return [text];
        }

        return Parts(content, "text").Select(part => part["text"]?.GetValue<string>() ?? "").ToList();
    }

    private static string Joined(JsonNode? content, string partType)
    {
        if (partType == "text" && content is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return string.Join(
            "\n\n",
            Parts(content, partType).Select(part => part["text"]?.GetValue<string>() ?? "").Where(item => item.Length > 0));
    }

    private static string PlainText(JsonNode? content)
    {
        if (content is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return Joined(content, "text");
    }

    private static List<(string Media, string Data)> ImageParts(JsonNode? content)
    {
        var images = new List<(string Media, string Data)>();
        foreach (var part in Parts(content, "image"))
        {
            if (part["data"] is not JsonValue dataValue
                || !dataValue.TryGetValue<string>(out var data)
                || string.IsNullOrEmpty(data))
            {
                continue;
            }

            var media = "image/png";
            if (part["mediaType"] is JsonValue mediaValue
                && mediaValue.TryGetValue<string>(out var mediaText)
                && !string.IsNullOrEmpty(mediaText))
            {
                media = mediaText;
            }

            images.Add((media, data));
        }

        return images;
    }

    private static string ToolResult(JsonNode? content)
    {
        var chunks = new List<string>();
        foreach (var part in Parts(content, "tool-result"))
        {
            if (part["result"] is JsonValue result && result.TryGetValue<string>(out var text))
            {
                chunks.Add(text);
            }
            else if (part["result"] is JsonNode node)
            {
                chunks.Add(node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        return string.Join("\n\n", chunks);
    }
}
