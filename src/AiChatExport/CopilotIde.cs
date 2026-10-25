using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace AiChatExport;

internal static class CopilotIde
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly HashSet<string> NitriteFields = new(StringComparer.Ordinal)
    {
        "sessionId", "modifiedAt", "createdAt", "dirName", "path", "source", "value", "name",
    };

    private static readonly Regex Uuid = new(
        "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string VscodeUserDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(roaming))
            {
                roaming = Path.Combine(home, "AppData", "Roaming");
            }

            return Path.Combine(roaming, "Code", "User");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", "Code", "User");
        }

        return Path.Combine(home, ".config", "Code", "User");
    }

    public static string GithubCopilotDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local))
            {
                local = Path.Combine(home, "AppData", "Local");
            }

            return Path.Combine(local, "github-copilot");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", "github-copilot");
        }

        return Path.Combine(home, ".config", "github-copilot");
    }

    public static List<Chat> LoadVscode(string root, IList<string> notes, bool includeEmpty)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var found = new List<(string Path, string? WorkspaceJson)>();
        var storage = Path.Combine(root, "workspaceStorage");
        if (Directory.Exists(storage))
        {
            foreach (var workspace in Directory.EnumerateDirectories(storage))
            {
                var sessions = Path.Combine(workspace, "chatSessions");
                if (!Directory.Exists(sessions))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(sessions))
                {
                    if (SessionFile(path))
                    {
                        found.Add((path, Path.Combine(workspace, "workspace.json")));
                    }
                }
            }
        }

        var empty = Path.Combine(root, "globalStorage", "emptyWindowChatSessions");
        if (Directory.Exists(empty))
        {
            foreach (var path in Directory.EnumerateFiles(empty))
            {
                if (SessionFile(path))
                {
                    found.Add((path, null));
                }
            }
        }

        var rows = new List<Chat>();
        foreach (var (path, workspaceJson) in found)
        {
            var chat = VscodeChat(path, workspaceJson, notes, includeEmpty);
            if (chat is not null)
            {
                rows.Add(chat);
            }
        }

        return rows;
    }

    public static List<Chat> LoadNitrite(string root, IList<string> notes, bool includeEmpty, string? eventsRoot)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var rows = new List<Chat>();
        foreach (var path in Directory.EnumerateFiles(root, "copilot-agent-sessions-nitrite.db", SearchOption.AllDirectories))
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Contains("bg-agent-sessions"))
            {
                continue;
            }

            if (!parts.Contains("rd") && !parts.Contains("vs"))
            {
                continue;
            }

            rows.AddRange(NitriteChats(path, notes, includeEmpty, eventsRoot));
        }

        return rows;
    }

    public static List<Chat> LoadAgentHost(string vscodeRoot, string eventsRoot, IList<string> notes, bool includeEmpty)
    {
        var database = Path.Combine(vscodeRoot, "globalStorage", "agent-host.db");
        if (!File.Exists(database))
        {
            return [];
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadOnly,
        };
        using var connection = new SqliteConnection(builder.ConnectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT provider_data, metadata, tombstoned FROM chats_v2";
            using var reader = command.ExecuteReader();
            var rows = new List<Chat>();
            while (reader.Read())
            {
                if (Tombstoned(reader.GetValue(2)))
                {
                    continue;
                }

                var sessionId = SdkSessionId(reader.GetValue(0));
                if (sessionId.Length == 0)
                {
                    continue;
                }

                var directory = Path.Combine(eventsRoot, "session-state", sessionId);
                var chat = CopilotCatalog.SessionFromEvents(directory, notes, "cp-ide");
                if (chat is null)
                {
                    if (includeEmpty)
                    {
                        rows.Add(new Chat(sessionId, SummaryTitle(reader.GetValue(1)), "", "", 0, false, directory, "cp-ide"));
                    }

                    continue;
                }

                var title = SummaryTitle(reader.GetValue(1));
                if (title is not null)
                {
                    chat = chat with { Title = title };
                }

                rows.Add(chat);
            }

            return rows;
        }
        catch (Exception exc) when (exc is SqliteException or IOException)
        {
            notes.Add($"Unreadable Copilot IDE session agent-host: {exc.Message}");
            return [];
        }
        finally
        {
            connection.Close();
            SqliteConnection.ClearPool(connection);
        }
    }

    public static (Conversation Conversation, List<string> Prompts) LoadConversation(string path, string sessionId)
    {
        if (Directory.Exists(path))
        {
            var loaded = CopilotTranscript.Load(path);
            return (loaded.Conversation, loaded.Prompts);
        }

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            return VscodeConversation(ReadSession(path) ?? new JsonObject());
        }

        foreach (var session in SessionsFromStrings(JavaStrings(File.ReadAllBytes(path))))
        {
            if (session.Id == sessionId)
            {
                return TurnConversation(session.Turns);
            }
        }

        return (new Conversation(), []);
    }

    private static bool SessionFile(string path) =>
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

    private static Chat? VscodeChat(string path, string? workspaceJson, IList<string> notes, bool includeEmpty)
    {
        JsonObject? document;
        try
        {
            document = ReadSession(path);
        }
        catch (Exception exc) when (exc is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or DecoderFallbackException or ArgumentException)
        {
            notes.Add($"Unreadable Copilot IDE session {Path.GetFileNameWithoutExtension(path)}: {exc.Message}");
            return null;
        }

        if (document is null)
        {
            notes.Add($"Unreadable Copilot IDE session {Path.GetFileNameWithoutExtension(path)}: not an object");
            return null;
        }

        var (firstLine, hasTurn) = TurnSummary(document);
        if (!hasTurn && !includeEmpty)
        {
            return null;
        }

        var sessionId = document["sessionId"] is JsonValue idNode
            && idNode.TryGetValue<string>(out var rawId)
            && !string.IsNullOrEmpty(rawId)
            ? rawId
            : Path.GetFileNameWithoutExtension(path);
        var cwd = WorkspaceCwd(workspaceJson);
        return new Chat(
            sessionId,
            Title(document["customTitle"], firstLine),
            cwd,
            cwd.Length == 0 ? "" : Catalog.WorkspaceFolder(cwd),
            UpdatedMs(document),
            hasTurn,
            path,
            "cp-ide");
    }

    private static JsonObject? ReadSession(string path)
    {
        var text = File.ReadAllText(path, Utf8);
        if (path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (JsonNode.Parse(line) is JsonObject item && KindZero(item["kind"]) && item["v"] is JsonObject snapshot)
                {
                    return snapshot;
                }
            }

            return new JsonObject();
        }

        var document = JsonNode.Parse(text);
        if (document is JsonObject obj)
        {
            if (KindZero(obj["kind"]) && obj["v"] is JsonObject wrapped)
            {
                return wrapped;
            }

            return obj;
        }

        throw new System.Text.Json.JsonException("not an object");
    }

    private static bool KindZero(JsonNode? node)
    {
        if (node is not JsonValue value || value.GetValueKind() != System.Text.Json.JsonValueKind.Number)
        {
            return false;
        }

        if (value.TryGetValue<int>(out var whole))
        {
            return whole == 0;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return number == 0;
        }

        return false;
    }

    private static (string FirstLine, bool HasTurn) TurnSummary(JsonObject document)
    {
        if (document["requests"] is not JsonArray requests)
        {
            return ("", false);
        }

        var firstLine = "";
        var hasTurn = false;
        foreach (var requestNode in requests)
        {
            if (requestNode is not JsonObject request)
            {
                continue;
            }

            var text = "";
            if (request["message"] is JsonObject message
                && message["text"] is JsonValue textNode
                && textNode.TryGetValue<string>(out var raw))
            {
                text = raw.Trim();
            }

            if (text.Length > 0)
            {
                hasTurn = true;
                if (firstLine.Length == 0)
                {
                    firstLine = text.Split('\n', 2)[0].Trim();
                }
            }

            if (request["response"] is not JsonArray response)
            {
                continue;
            }

            foreach (var partNode in response)
            {
                if (partNode is JsonObject part
                    && part["value"] is JsonValue value
                    && value.TryGetValue<string>(out var body)
                    && body.Trim().Length > 0)
                {
                    hasTurn = true;
                }
            }
        }

        return (firstLine, hasTurn);
    }

    private static string? Title(JsonNode? custom, string firstLine)
    {
        if (custom is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
    }

    private static long UpdatedMs(JsonObject document)
    {
        var latest = Millis(document["lastMessageDate"]);
        if (latest is not null)
        {
            return latest.Value;
        }

        return Millis(document["creationDate"]) ?? 0;
    }

    private static long? Millis(JsonNode? node)
    {
        if (node is not JsonValue value || value.GetValueKind() != System.Text.Json.JsonValueKind.Number)
        {
            return null;
        }

        if (value.TryGetValue<long>(out var whole))
        {
            return whole;
        }

        if (value.TryGetValue<double>(out var number))
        {
            return (long)number;
        }

        return null;
    }

    private static string WorkspaceCwd(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return "";
        }

        try
        {
            if (JsonNode.Parse(File.ReadAllText(path, Utf8)) is not JsonObject document)
            {
                return "";
            }

            if (document["folder"] is not JsonValue folder || !folder.TryGetValue<string>(out var uri))
            {
                return "";
            }

            return FileUriPath(uri);
        }
        catch (Exception exc) when (exc is IOException or System.Text.Json.JsonException or DecoderFallbackException)
        {
            return "";
        }
    }

    private static string FileUriPath(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeFile)
        {
            return "";
        }

        return uri.LocalPath;
    }

    private static bool Tombstoned(object value)
    {
        return value switch
        {
            long number => number != 0,
            int number => number != 0,
            _ => false,
        };
    }

    private static string SdkSessionId(object provider)
    {
        if (provider is not string text || string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        try
        {
            if (JsonNode.Parse(text) is JsonObject document
                && document["sdkSessionId"] is JsonValue id
                && id.TryGetValue<string>(out var sessionId)
                && !string.IsNullOrWhiteSpace(sessionId))
            {
                return sessionId.Trim();
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return "";
        }

        return "";
    }

    private static string? SummaryTitle(object metadata)
    {
        if (metadata is not string text || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(text) is not JsonObject document
                || document["data"] is not JsonObject data
                || data["summary"] is not JsonValue summary
                || !summary.TryGetValue<string>(out var title)
                || string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            return title.Trim();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static List<Chat> NitriteChats(string path, IList<string> notes, bool includeEmpty, string? eventsRoot)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (IOException exc)
        {
            notes.Add($"Unreadable Copilot IDE session {Path.GetFileNameWithoutExtension(path)}: {exc.Message}");
            return [];
        }

        var rows = new List<Chat>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var strings = JavaStrings(data);
        foreach (var session in LiveSessions(strings).Concat(SessionsFromStrings(strings)))
        {
            if (!seen.Add(session.Id))
            {
                continue;
            }

            var userLine = session.Turns.FirstOrDefault(turn => turn.Source == "user" && turn.Text.Trim().Length > 0).Text ?? "";
            var assistant = session.Turns.Any(turn => turn.Source == "copilot" && turn.Text.Trim().Length > 0);
            if (userLine.Trim().Length == 0 && !assistant && !includeEmpty)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(eventsRoot))
            {
                var logged = CopilotCatalog.SessionFromEvents(Path.Combine(eventsRoot, "session-state", session.Id), notes, "cp-ide");
                if (logged is not null)
                {
                    rows.Add(logged);
                    continue;
                }
            }

            var cwd = session.Workspace;
            rows.Add(new Chat(
                session.Id,
                userLine.Trim().Length == 0 ? null : userLine.Split('\n', 2)[0].Trim(),
                cwd,
                cwd.Length == 0 ? "" : Catalog.WorkspaceFolder(cwd),
                session.Modified ?? session.Created ?? 0,
                userLine.Trim().Length > 0 || assistant,
                path,
                "cp-ide"));
        }

        return rows;
    }

    private static List<string> JavaStrings(byte[] data)
    {
        var found = new List<string>();
        var index = 0;
        while (index < data.Length - 3)
        {
            if (data[index] != 0x74)
            {
                index++;
                continue;
            }

            var length = (data[index + 1] << 8) | data[index + 2];
            var end = index + 3 + length;
            if (length <= 0 || length > 10000 || end > data.Length)
            {
                index++;
                continue;
            }

            try
            {
                var text = Utf8.GetString(data, index + 3, length);
                if (text.Contains('\n') || text.All(static ch => !char.IsControl(ch)))
                {
                    found.Add(text);
                    index = end;
                    continue;
                }
            }
            catch (DecoderFallbackException)
            {
            }

            index++;
        }

        return found;
    }

    private static List<NitriteSession> SessionsFromStrings(List<string> strings)
    {
        var sessions = new List<NitriteSession>();
        NitriteSession? current = null;
        var index = 0;
        while (index < strings.Count)
        {
            var token = strings[index];
            if (token == "sessionId" && index + 1 < strings.Count && !NitriteFields.Contains(strings[index + 1]))
            {
                current = new NitriteSession { Id = strings[index + 1] };
                sessions.Add(current);
                index += 2;
                continue;
            }

            if (current is null)
            {
                index++;
                continue;
            }

            if (token is "modifiedAt" or "createdAt"
                && index + 1 < strings.Count
                && long.TryParse(strings[index + 1], out var millis))
            {
                if (token == "modifiedAt")
                {
                    current.Modified = millis;
                }
                else
                {
                    current.Created = millis;
                }

                index += 2;
                continue;
            }

            if (token is "dirName" or "path" && index + 1 < strings.Count && !NitriteFields.Contains(strings[index + 1]))
            {
                if (token == "path" || current.Workspace.Length == 0)
                {
                    current.Workspace = strings[index + 1];
                }

                index += 2;
                continue;
            }

            if (token == "source"
                && index + 3 < strings.Count
                && strings[index + 1] is "user" or "copilot"
                && strings[index + 2] == "value")
            {
                current.Turns.Add((strings[index + 1], strings[index + 3]));
                index += 4;
                continue;
            }

            index++;
        }

        return sessions;
    }

    private static List<NitriteSession> LiveSessions(List<string> strings)
    {
        var sessions = new List<NitriteSession>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        while (index < strings.Count)
        {
            if (strings[index] == "name" && index + 2 < strings.Count && strings[index + 1] == "value")
            {
                var text = strings[index + 2];
                var windowEnd = Math.Min(strings.Count, index + 8);
                var hasSource = false;
                var hasCopilot = false;
                for (var cursor = index + 3; cursor < windowEnd; cursor++)
                {
                    if (strings[cursor] == "source")
                    {
                        hasSource = true;
                    }

                    if (strings[cursor] == "copilot")
                    {
                        hasCopilot = true;
                    }
                }

                if (hasSource && hasCopilot)
                {
                    var sessionId = "";
                    var followEnd = Math.Min(strings.Count, index + 24);
                    for (var follow = index + 3; follow < followEnd - 1; follow++)
                    {
                        if (strings[follow] == "conversationId" && Uuid.IsMatch(strings[follow + 1]))
                        {
                            sessionId = strings[follow + 1];
                            break;
                        }
                    }

                    if (sessionId.Length > 0 && seen.Add(sessionId) && text.Trim().Length > 0)
                    {
                        sessions.Add(new NitriteSession
                        {
                            Id = sessionId,
                            Turns = [("user", text)],
                        });
                    }
                }

                index += 3;
                continue;
            }

            index++;
        }

        return sessions;
    }

    private static (Conversation Conversation, List<string> Prompts) VscodeConversation(JsonObject document)
    {
        var conversation = new Conversation();
        var prompts = new List<string>();
        if (document["requests"] is not JsonArray requests)
        {
            return (conversation, prompts);
        }

        foreach (var requestNode in requests)
        {
            if (requestNode is not JsonObject request)
            {
                continue;
            }

            var text = "";
            if (request["message"] is JsonObject message
                && message["text"] is JsonValue textNode
                && textNode.TryGetValue<string>(out var raw))
            {
                text = raw.Trim();
            }

            if (text.Length > 0)
            {
                conversation.Messages.Add(TextMessage("user", text));
                prompts.Add(text);
            }

            if (request["response"] is not JsonArray response)
            {
                continue;
            }

            var content = new JsonArray();
            foreach (var partNode in response)
            {
                if (partNode is not JsonObject part)
                {
                    continue;
                }

                if (part["kind"] is null
                    && part["value"] is JsonValue value
                    && value.TryGetValue<string>(out var body)
                    && body.Trim().Length > 0)
                {
                    content.Add(new JsonObject { ["type"] = "text", ["text"] = body.Trim() });
                    continue;
                }

                if (part["kind"] is JsonValue kind && kind.TryGetValue<string>(out var kindText) && kindText == "toolInvocationSerialized")
                {
                    content.Add(IdeTool(part));
                    if (part["result"] is JsonValue result && result.TryGetValue<string>(out var resultText) && resultText.Trim().Length > 0)
                    {
                        conversation.Messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
                        content = new JsonArray();
                        conversation.Messages.Add(new JsonObject
                        {
                            ["role"] = "tool",
                            ["content"] = new JsonArray
                            {
                                new JsonObject { ["type"] = "tool-result", ["result"] = resultText },
                            },
                        });
                    }
                }
            }

            if (content.Count > 0)
            {
                conversation.Messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
            }
        }

        return (conversation, prompts);
    }

    private static (Conversation Conversation, List<string> Prompts) TurnConversation(List<(string Source, string Text)> turns)
    {
        var conversation = new Conversation();
        var prompts = new List<string>();
        foreach (var (source, text) in turns)
        {
            var cleaned = text.Trim();
            if (cleaned.Length == 0)
            {
                continue;
            }

            if (source == "user")
            {
                conversation.Messages.Add(TextMessage("user", cleaned));
                prompts.Add(cleaned);
            }
            else if (source == "copilot")
            {
                conversation.Messages.Add(TextMessage("assistant", cleaned));
            }
        }

        return (conversation, prompts);
    }

    private static JsonObject IdeTool(JsonObject part)
    {
        var toolId = StringValue(part["toolId"]);
        if (toolId.Length == 0)
        {
            toolId = StringValue(part["toolName"]);
        }

        if (toolId.Length == 0)
        {
            toolId = "tool";
        }

        var args = ToolArgs(part);
        var path = Arg(args, "path");
        return toolId switch
        {
            "copilot_readFile" => ToolCall("Read", new JsonObject { ["path"] = path }),
            "copilot_createFile" => ToolCall("Create", new JsonObject { ["path"] = path }),
            "copilot_findFiles" => ToolCall("Glob", new JsonObject
            {
                ["glob_pattern"] = Arg(args, "glob_pattern") is { Length: > 0 } pattern ? pattern : Arg(args, "pattern"),
            }),
            "run_in_terminal" => ToolCall("Shell", new JsonObject { ["description"] = Arg(args, "description") }),
            "copilot_replaceString" or "copilot_multiReplaceString" => ToolCall("Edit", new JsonObject { ["path"] = path }),
            _ => ToolCall(toolId, new JsonObject()),
        };
    }

    private static JsonObject ToolArgs(JsonObject part)
    {
        foreach (var key in new[] { "parameters", "input", "args" })
        {
            if (part[key] is JsonObject args)
            {
                return args;
            }
        }

        return new JsonObject();
    }

    private static string Arg(JsonObject args, string name)
    {
        if (args[name] is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return "";
    }

    private static string StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static JsonObject ToolCall(string name, JsonObject args) =>
        new()
        {
            ["type"] = "tool-call",
            ["toolName"] = name,
            ["args"] = args,
        };

    private static JsonObject TextMessage(string role, string text) =>
        new()
        {
            ["role"] = role,
            ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } },
        };

    private sealed class NitriteSession
    {
        public string Id { get; set; } = "";

        public List<(string Source, string Text)> Turns { get; set; } = [];

        public long? Modified { get; set; }

        public long? Created { get; set; }

        public string Workspace { get; set; } = "";
    }
}
