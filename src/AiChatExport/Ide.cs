using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiChatExport;

internal static class IdeTranscript
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string? FirstQueryLine(string path)
    {
        foreach (var line in File.ReadLines(path, Utf8))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("role", out var role)
                    || role.ValueKind != JsonValueKind.String
                    || role.GetString() != "user"
                    || !root.TryGetProperty("message", out var message))
                {
                    continue;
                }

                var query = TranscriptBuilder.QueryText(TextParts(message)).Trim();
                if (query.Length == 0)
                {
                    continue;
                }

                var first = query.Split('\n', 2)[0].Trim();
                return first.Length == 0 ? null : first;
            }
        }

        return null;
    }

    public static (string Cwd, string Workspace) Workspace(string projectDir)
    {
        var folder = Path.GetFileName(projectDir);
        var trusted = Path.Combine(projectDir, ".workspace-trusted");
        try
        {
            if (!File.Exists(trusted))
            {
                return ("", folder);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(trusted));
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("workspacePath", out var path)
                && path.ValueKind == JsonValueKind.String)
            {
                var cwd = path.GetString() ?? "";
                if (cwd.Length > 0)
                {
                    return (cwd, Catalog.WorkspaceFolder(cwd));
                }
            }
        }
        catch (Exception exc) when (exc is IOException or UnauthorizedAccessException or JsonException or DecoderFallbackException)
        {
        }

        return ("", folder);
    }

    public static Conversation Load(string directory)
    {
        var id = Path.GetFileName(directory);
        var path = Path.Combine(directory, id + ".jsonl");
        var conversation = new Conversation();
        var number = 0;
        foreach (var line in File.ReadLines(path, Utf8))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                conversation.Skipped.Add($"line {number}");
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    conversation.Skipped.Add($"line {number}");
                    continue;
                }

                var kind = root.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String
                    ? typeNode.GetString()
                    : null;
                if (kind == "turn_ended")
                {
                    continue;
                }

                var role = root.TryGetProperty("role", out var roleNode) && roleNode.ValueKind == JsonValueKind.String
                    ? roleNode.GetString()
                    : null;
                if (role is not ("user" or "assistant"))
                {
                    conversation.Skipped.Add($"line {number}");
                    continue;
                }

                var content = root.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.Object
                    && message.TryGetProperty("content", out var contentNode)
                    ? contentNode
                    : default;
                conversation.Messages.Add(new JsonObject
                {
                    ["role"] = role,
                    ["content"] = Adapt(content),
                });
            }
        }

        return conversation;
    }

    private static string ElementText(JsonElement node) =>
        node.ValueKind == JsonValueKind.String ? node.GetString() ?? "" : node.ToString();

    private static List<string> TextParts(JsonElement message)
    {
        var parts = new List<string>();
        if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content))
        {
            return parts;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            parts.Add(content.GetString() ?? "");
            return parts;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return parts;
        }

        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                parts.Add(part.GetString() ?? "");
            }
            else if (part.ValueKind == JsonValueKind.Object
                && part.TryGetProperty("type", out var type)
                && type.GetString() == "text")
            {
                parts.Add(part.TryGetProperty("text", out var text) ? ElementText(text) : "");
            }
        }

        return parts;
    }

    private static JsonNode Adapt(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = content.GetString() ?? "" },
            };
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return new JsonArray();
        }

        var parts = new JsonArray();
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                parts.Add(new JsonObject { ["type"] = "text", ["text"] = part.GetString() ?? "" });
                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = part.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
            if (type == "tool_use")
            {
                var name = part.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String
                    ? nameNode.GetString()
                    : null;
                JsonNode args = new JsonObject();
                if (part.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Object)
                {
                    args = JsonNode.Parse(input.GetRawText()) ?? new JsonObject();
                }

                parts.Add(new JsonObject
                {
                    ["type"] = "tool-call",
                    ["toolName"] = string.IsNullOrEmpty(name) ? "tool" : name,
                    ["args"] = args,
                });
            }
            else if (type == "text")
            {
                var text = part.TryGetProperty("text", out var textNode) ? ElementText(textNode) : "";
                parts.Add(new JsonObject { ["type"] = "text", ["text"] = text });
            }
            else
            {
                parts.Add(JsonNode.Parse(part.GetRawText()));
            }
        }

        return parts;
    }
}
