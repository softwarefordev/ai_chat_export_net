using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace AiChatExport.Tests;

public static class Support
{
    public static readonly byte[] Png = [.. Store.PngMagic, 0, 0, 0, 0, 0, 0, 0, 0];

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Digest(byte[] data) => Store.Digest(data);

    public static JsonObject Message(string role, JsonNode content)
    {
        return new JsonObject
        {
            ["role"] = role,
            ["content"] = content.DeepClone(),
        };
    }

    public static JsonObject Message(string role, string content) => Message(role, JsonValue.Create(content)!);

    public static JsonObject TextPart(string type, string text)
    {
        return new JsonObject
        {
            ["type"] = type,
            ["text"] = text,
        };
    }

    public static JsonObject ToolCall(string toolName, JsonObject args)
    {
        return new JsonObject
        {
            ["type"] = "tool-call",
            ["toolName"] = toolName,
            ["args"] = args,
        };
    }

    public static (List<JsonObject> Messages, List<string> Prompts) SampleMessages()
    {
        var typed = "Please add a river tool.";
        var messages = new List<JsonObject>
        {
            Message("system", "SYSTEM_PROMPT_TEXT"),
            Message("user", new JsonArray
            {
                TextPart("text", "INJECTED_CONTEXT_BLOCK"),
                TextPart("text", typed),
            }),
            Message("assistant", new JsonArray
            {
                TextPart("reasoning", "REASONING_PRIVATE"),
                TextPart("text", "The river tool lives in tools/river.py."),
                ToolCall("Read", new JsonObject { ["path"] = "tools/river.py" }),
                ToolCall("Glob", new JsonObject { ["glob_pattern"] = "*.py" }),
                ToolCall("Shell", new JsonObject
                {
                    ["command"] = "SECRET_COMMAND_XYZ",
                    ["description"] = "List map tool modules",
                }),
            }),
            Message("tool", new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "tool-result",
                    ["toolName"] = "Shell",
                    ["result"] = "TOOL_RESULT_PAYLOAD",
                },
            }),
        };
        return (messages, [typed]);
    }

    public static string WriteChat(
        string root,
        string chatId,
        string cwd,
        string? title = "Sample",
        long updatedMs = 0,
        IReadOnlyList<JsonObject>? messages = null,
        IReadOnlyList<string>? prompts = null,
        int schema = 1,
        bool withStore = true,
        byte[]? rawMeta = null,
        IReadOnlyList<byte[]>? looseBlobs = null,
        IReadOnlyList<byte[]>? childBlobs = null,
        bool includeTitle = true)
    {
        var chatDir = Path.Combine(root, "workspace", chatId);
        Directory.CreateDirectory(chatDir);
        var metaPath = Path.Combine(chatDir, "meta.json");
        if (rawMeta is not null)
        {
            File.WriteAllBytes(metaPath, rawMeta);
        }
        else
        {
            var payload = new JsonObject
            {
                ["schemaVersion"] = schema,
                ["cwd"] = cwd,
                ["updatedAtMs"] = updatedMs,
                ["hasConversation"] = messages is { Count: > 0 },
            };
            if (includeTitle && title is not null)
            {
                payload["title"] = title;
            }

            File.WriteAllText(metaPath, payload.ToJsonString());
        }

        if (prompts is not null)
        {
            var array = new JsonArray();
            foreach (var prompt in prompts)
            {
                array.Add(prompt);
            }

            File.WriteAllText(Path.Combine(chatDir, "prompt_history.json"), array.ToJsonString());
        }

        if (withStore && messages is not null)
        {
            WriteDb(Path.Combine(chatDir, "store.db"), messages, looseBlobs ?? [], childBlobs ?? []);
        }

        return chatDir;
    }

    public static void WriteDb(
        string path,
        IReadOnlyList<JsonObject> messages,
        IReadOnlyList<byte[]> looseBlobs,
        IReadOnlyList<byte[]> childBlobs)
    {
        var blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var messageIds = new List<string>();
        foreach (var message in messages)
        {
            var data = Encoding.UTF8.GetBytes(message.ToJsonString(Compact));
            var messageId = Digest(data);
            blobs[messageId] = data;
            messageIds.Add(messageId);
        }

        var extraIds = new List<string>();
        foreach (var data in childBlobs)
        {
            var extraId = Digest(data);
            blobs[extraId] = data;
            extraIds.Add(extraId);
        }

        foreach (var data in looseBlobs)
        {
            blobs[Digest(data)] = data;
        }

        var checkpoint = new byte[messageIds.Count * 32];
        for (var index = 0; index < messageIds.Count; index++)
        {
            Convert.FromHexString(messageIds[index]).CopyTo(checkpoint.AsSpan(index * 32));
        }

        var checkpointId = Digest(checkpoint);
        blobs[checkpointId] = checkpoint;
        var rootIds = messageIds.Concat(extraIds).Append(checkpointId).ToList();
        var rootBlob = new byte[rootIds.Count * 32];
        for (var index = 0; index < rootIds.Count; index++)
        {
            Convert.FromHexString(rootIds[index]).CopyTo(rootBlob.AsSpan(index * 32));
        }

        var rootId = Digest(rootBlob);
        blobs[rootId] = rootBlob;
        WriteSqlite(path, blobs, rootId);
    }

    public static Dictionary<string, List<string>> WriteProtobufDb(
        string path,
        IReadOnlyList<(IReadOnlyList<JsonObject> Messages, byte[] Summary)> checkpoints,
        IReadOnlyList<JsonObject> live,
        IReadOnlyList<(int Field, byte[] Data)>? sideBlobs = null,
        IReadOnlyList<byte[]>? unknownLive = null)
    {
        var blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var checkpointIds = new List<string>();
        foreach (var (messages, summary) in checkpoints)
        {
            var parts = new List<byte>();
            foreach (var message in messages)
            {
                var data = Encoding.UTF8.GetBytes(message.ToJsonString(Compact));
                var messageId = Digest(data);
                blobs[messageId] = data;
                parts.AddRange(ProtobufId(1, messageId));
            }

            parts.Add(0x12);
            parts.AddRange(Varint(summary.Length));
            parts.AddRange(summary);
            var checkpoint = parts.ToArray();
            var checkpointId = Digest(checkpoint);
            blobs[checkpointId] = checkpoint;
            checkpointIds.Add(checkpointId);
        }

        var liveIds = new List<string>();
        foreach (var message in live)
        {
            var data = Encoding.UTF8.GetBytes(message.ToJsonString(Compact));
            var messageId = Digest(data);
            blobs[messageId] = data;
            liveIds.Add(messageId);
        }

        var unknownIds = new List<string>();
        foreach (var data in unknownLive ?? [])
        {
            var unknownId = Digest(data);
            blobs[unknownId] = data;
            unknownIds.Add(unknownId);
        }

        var sideIds = new List<string>();
        var sideParts = new List<byte[]>();
        foreach (var (field, data) in sideBlobs ?? [])
        {
            var sideId = Digest(data);
            blobs[sideId] = data;
            sideIds.Add(sideId);
            sideParts.Add(ProtobufId(field, sideId));
        }

        var rootParts = new List<byte>();
        foreach (var blobId in liveIds.Concat(unknownIds))
        {
            rootParts.AddRange(ProtobufId(1, blobId));
        }

        foreach (var part in sideParts)
        {
            rootParts.AddRange(part);
        }

        foreach (var blobId in checkpointIds)
        {
            rootParts.AddRange(ProtobufId(13, blobId));
        }

        var rootBlob = rootParts.ToArray();
        var rootId = Digest(rootBlob);
        blobs[rootId] = rootBlob;
        WriteSqlite(path, blobs, rootId);
        return new Dictionary<string, List<string>>
        {
            ["checkpoint_ids"] = checkpointIds,
            ["side_ids"] = sideIds,
            ["unknown_ids"] = unknownIds,
        };
    }

    private static void WriteSqlite(string path, Dictionary<string, byte[]> blobs, string rootId)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var meta = Convert.ToHexString(Encoding.UTF8.GetBytes(
            new JsonObject { ["latestRootBlobId"] = rootId }.ToJsonString())).ToLowerInvariant();
        var builder = new SqliteConnectionStringBuilder { DataSource = path };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE blobs (id TEXT PRIMARY KEY, data BLOB);
            CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT);
            """;
        command.ExecuteNonQuery();
        foreach (var (id, data) in blobs)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO blobs (id, data) VALUES ($id, $data)";
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$data", data);
            insert.ExecuteNonQuery();
        }

        using var metaInsert = connection.CreateCommand();
        metaInsert.CommandText = "INSERT INTO meta (key, value) VALUES ('0', $value)";
        metaInsert.Parameters.AddWithValue("$value", meta);
        metaInsert.ExecuteNonQuery();
        connection.Close();
        SqliteConnection.ClearPool(connection);
    }

    private static byte[] Varint(int value)
    {
        var output = new List<byte>();
        var remaining = value;
        while (true)
        {
            var bits = remaining & 0x7F;
            remaining >>= 7;
            if (remaining != 0)
            {
                output.Add((byte)(bits | 0x80));
            }
            else
            {
                output.Add((byte)bits);
                return output.ToArray();
            }
        }
    }

    private static byte[] ProtobufId(int fieldNumber, string blobId)
    {
        var tag = (fieldNumber << 3) | 2;
        var raw = Convert.FromHexString(blobId);
        var output = new List<byte>();
        output.AddRange(Varint(tag));
        output.AddRange(Varint(raw.Length));
        output.AddRange(raw);
        return output.ToArray();
    }
}
