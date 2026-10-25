using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace AiChatExport;

internal static class CopilotCatalog
{
    public static List<Chat> Load(string root, IList<string> notes)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var database = Path.Combine(root, "session-store.db");
        if (!File.Exists(database))
        {
            return [];
        }

        try
        {
            return Read(root, database);
        }
        catch (Exception exc) when (exc is SqliteException or IOException)
        {
            notes.Add($"Unreadable Copilot store: {exc.Message}");
            return [];
        }
    }

    private static List<Chat> Read(string root, string database)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadOnly,
        };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        try
        {
            var firstLines = FirstLines(connection);
            var rows = new List<Chat>();
            using var sessions = connection.CreateCommand();
            sessions.CommandText = "SELECT id, cwd, summary, updated_at FROM sessions";
            using var reader = sessions.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetValue(0) is not string id || id.Length == 0)
                {
                    continue;
                }

                var cwd = reader.GetValue(1) as string ?? "";
                var summary = reader.GetValue(2) as string;
                var updated = reader.GetValue(3) as string;
                var (updatedMs, timeLabel) = Updated(updated);
                firstLines.TryGetValue(id, out var firstLine);
                rows.Add(new Chat(
                    id,
                    Title(summary, firstLine ?? ""),
                    cwd,
                    cwd.Length == 0 ? "" : Catalog.WorkspaceFolder(cwd),
                    updatedMs,
                    true,
                    Path.Combine(root, "session-state", id),
                    "cp-cli",
                    timeLabel));
            }

            return rows;
        }
        finally
        {
            connection.Close();
            SqliteConnection.ClearPool(connection);
        }
    }

    private static Dictionary<string, string> FirstLines(SqliteConnection connection)
    {
        var turns = new List<(string Id, long Index, string Message)>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT session_id, turn_index, user_message FROM turns";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetValue(0) is not string id || reader.GetValue(2) is not string message)
            {
                continue;
            }

            var index = reader.GetValue(1) is long number ? number : 0;
            turns.Add((id, index, message));
        }

        var lines = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var turn in turns.OrderBy(item => item.Id, StringComparer.Ordinal).ThenBy(item => item.Index))
        {
            if (lines.ContainsKey(turn.Id))
            {
                continue;
            }

            var text = turn.Message.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            lines[turn.Id] = text.Split('\n', 2)[0].Trim();
        }

        return lines;
    }

    private static string? Title(string? summary, string firstLine)
    {
        if (!string.IsNullOrWhiteSpace(summary))
        {
            return summary.Trim();
        }

        return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine.Trim();
    }

    private static (long Milliseconds, string? Label) Updated(string? value)
    {
        var collapsed = string.IsNullOrEmpty(value)
            ? ""
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length == 0)
        {
            return (0, "");
        }

        var stamp = collapsed.EndsWith('Z') ? collapsed[..^1] + "+00:00" : collapsed;
        if (DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment))
        {
            return (moment.ToUnixTimeMilliseconds(), null);
        }

        return (0, collapsed);
    }

    public static Chat? SessionFromEvents(string directory, IList<string> notes, string source = "cp-cli")
    {
        var log = Path.Combine(directory, "events.jsonl");
        if (!File.Exists(log))
        {
            return null;
        }

        var cwd = "";
        var firstLine = "";
        var latest = "";
        var hasTurn = false;
        try
        {
            foreach (var line in File.ReadLines(log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)))
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
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (root.TryGetProperty("timestamp", out var stampNode)
                        && stampNode.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(stampNode.GetString()))
                    {
                        latest = stampNode.GetString()!.Trim();
                    }

                    var kind = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
                    var data = root.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == JsonValueKind.Object
                        ? dataNode
                        : default;
                    if (kind == "session.start" && cwd.Length == 0
                        && data.ValueKind == JsonValueKind.Object
                        && data.TryGetProperty("context", out var context)
                        && context.ValueKind == JsonValueKind.Object
                        && context.TryGetProperty("cwd", out var cwdNode)
                        && cwdNode.ValueKind == JsonValueKind.String)
                    {
                        cwd = cwdNode.GetString() ?? "";
                    }
                    else if (kind == "user.message")
                    {
                        var text = EventBody(data);
                        if (text.Length > 0)
                        {
                            hasTurn = true;
                            if (firstLine.Length == 0)
                            {
                                firstLine = text.Split('\n', 2)[0].Trim();
                            }
                        }
                    }
                    else if (kind == "assistant.message" && EventBody(data).Length > 0)
                    {
                        hasTurn = true;
                    }
                }
            }
        }
        catch (Exception exc) when (exc is IOException or DecoderFallbackException or ArgumentException)
        {
            notes.Add($"Unreadable Copilot events {Path.GetFileName(directory)}: {exc.Message}");
            return null;
        }

        if (!hasTurn)
        {
            return null;
        }

        var (updatedMs, timeLabel) = Updated(latest);
        if (updatedMs == 0)
        {
            updatedMs = new DateTimeOffset(File.GetLastWriteTimeUtc(log)).ToUnixTimeMilliseconds();
            timeLabel = null;
        }

        return new Chat(
            Path.GetFileName(directory),
            string.IsNullOrWhiteSpace(firstLine) ? null : firstLine,
            cwd,
            cwd.Length == 0 ? "" : Catalog.WorkspaceFolder(cwd),
            updatedMs,
            true,
            directory,
            source,
            timeLabel);
    }

    public static List<Chat> LoadUnlisted(string root, HashSet<string> skip, IList<string> notes)
    {
        var state = Path.Combine(root, "session-state");
        if (!Directory.Exists(state))
        {
            return [];
        }

        var rows = new List<Chat>();
        foreach (var child in Directory.EnumerateDirectories(state))
        {
            var name = Path.GetFileName(child);
            if (name.StartsWith('.') || skip.Contains(name))
            {
                continue;
            }

            var chat = SessionFromEvents(child, notes);
            if (chat is not null)
            {
                rows.Add(chat);
            }
        }

        return rows;
    }

    private static string EventBody(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("content", out var content))
        {
            return "";
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return (content.GetString() ?? "").Trim();
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                parts.Add(item.GetString()!.Trim());
            }
            else if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(text.GetString()))
            {
                parts.Add(text.GetString()!.Trim());
            }
        }

        return string.Join("\n\n", parts);
    }
}

internal static class CopilotTranscript
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static (Conversation Conversation, List<string> Prompts, string? Warning) Load(string directory)
    {
        var log = Path.Combine(directory, "events.jsonl");
        if (!File.Exists(log))
        {
            var fallback = FromTurns(directory);
            return (fallback.Conversation, fallback.Prompts, null);
        }

        try
        {
            var parsed = FromEvents(log);
            return (parsed.Conversation, parsed.Prompts, null);
        }
        catch (Exception exc) when (exc is IOException or DecoderFallbackException or ArgumentException)
        {
            var fallback = FromTurns(directory);
            return (fallback.Conversation, fallback.Prompts, $"Unreadable Copilot events {Path.GetFileName(directory)}: {exc.Message}");
        }
    }

    private static (Conversation Conversation, List<string> Prompts) FromTurns(string directory)
    {
        var conversation = new Conversation();
        var prompts = new List<string>();
        var database = Path.Combine(directory, "..", "..", "session-store.db");
        database = Path.GetFullPath(database);
        if (!File.Exists(database))
        {
            return (conversation, prompts);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadOnly,
        };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT user_message, assistant_response FROM turns WHERE session_id = $id ORDER BY turn_index";
            command.Parameters.AddWithValue("$id", Path.GetFileName(directory));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var user = (reader.GetValue(0) as string ?? "").Trim();
                var assistant = (reader.GetValue(1) as string ?? "").Trim();
                if (user.Length > 0)
                {
                    conversation.Messages.Add(TextMessage("user", user));
                    prompts.Add(user);
                }

                if (assistant.Length > 0)
                {
                    conversation.Messages.Add(TextMessage("assistant", assistant));
                }
            }
        }
        finally
        {
            connection.Close();
            SqliteConnection.ClearPool(connection);
        }

        return (conversation, prompts);
    }

    private static (Conversation Conversation, List<string> Prompts) FromEvents(string path)
    {
        var conversation = new Conversation();
        var prompts = new List<string>();
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
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var kind = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
                var data = root.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == JsonValueKind.Object
                    ? dataNode
                    : default;
                if (kind == "user.message")
                {
                    var text = EventText(data, "content");
                    if (text.Length > 0)
                    {
                        conversation.Messages.Add(TextMessage("user", text));
                        prompts.Add(text);
                    }
                }
                else if (kind == "system.message")
                {
                    var text = EventText(data, "content");
                    if (text.Length > 0)
                    {
                        conversation.Messages.Add(TextMessage("system", text));
                    }
                }
                else if (kind == "assistant.message")
                {
                    var content = new JsonArray();
                    var text = EventText(data, "content");
                    if (text.Length > 0)
                    {
                        content.Add(new JsonObject { ["type"] = "text", ["text"] = text });
                    }

                    if (data.ValueKind == JsonValueKind.Object
                        && data.TryGetProperty("reasoningText", out var reasoning)
                        && reasoning.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(reasoning.GetString()))
                    {
                        content.Add(new JsonObject { ["type"] = "reasoning", ["text"] = reasoning.GetString()!.Trim() });
                    }

                    if (content.Count > 0)
                    {
                        conversation.Messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
                    }
                }
                else if (kind == "tool.execution_start")
                {
                    conversation.Messages.Add(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = new JsonArray { ToolCall(data) },
                    });
                }
                else if (kind == "tool.execution_complete")
                {
                    if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (result.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(result.GetString()))
                    {
                        continue;
                    }

                    conversation.Messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "tool-result",
                                ["result"] = JsonNode.Parse(result.GetRawText()),
                            },
                        },
                    });
                }
            }
        }

        return (conversation, prompts);
    }

    private static JsonObject TextMessage(string role, string text) =>
        new()
        {
            ["role"] = role,
            ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } },
        };

    private static string EventText(JsonElement data, string name)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty(name, out var content))
        {
            return "";
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return (content.GetString() ?? "").Trim();
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                parts.Add(item.GetString()!.Trim());
            }
            else if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(text.GetString()))
            {
                parts.Add(text.GetString()!.Trim());
            }
        }

        return string.Join("\n\n", parts);
    }

    private static JsonObject ToolCall(JsonElement data)
    {
        var name = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("toolName", out var nameNode)
            ? nameNode.GetString()
            : null;
        var args = data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("arguments", out var argsNode)
            && argsNode.ValueKind == JsonValueKind.Object
            ? argsNode
            : default;
        JsonObject mapped = name switch
        {
            "view" => new JsonObject { ["path"] = Arg(args, "path") },
            "glob" => new JsonObject { ["glob_pattern"] = Arg(args, "pattern") },
            "powershell" => new JsonObject { ["description"] = Arg(args, "description") },
            "grep" => new JsonObject { ["pattern"] = Arg(args, "pattern") },
            "create" => new JsonObject { ["path"] = Arg(args, "path") },
            _ => args.ValueKind == JsonValueKind.Object
                ? JsonNode.Parse(args.GetRawText())!.AsObject()
                : new JsonObject(),
        };
        var toolName = name switch
        {
            "view" => "Read",
            "glob" => "Glob",
            "powershell" => "Shell",
            "grep" => "Grep",
            "create" => "Create",
            _ => string.IsNullOrEmpty(name) ? "tool" : name,
        };
        return new JsonObject
        {
            ["type"] = "tool-call",
            ["toolName"] = toolName,
            ["args"] = mapped,
        };
    }

    private static string Arg(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return "";
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }
}
