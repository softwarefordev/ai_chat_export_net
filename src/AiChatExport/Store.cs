using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace AiChatExport;

public sealed class Conversation
{
    public List<JsonObject> Messages { get; } = [];

    public List<string> Skipped { get; } = [];

    public List<string> UnlinkedImages { get; } = [];
}

public static class Store
{
    public static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static Conversation Load(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
        };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        using var metaCommand = connection.CreateCommand();
        metaCommand.CommandText = "SELECT value FROM meta LIMIT 1";
        var metaValue = metaCommand.ExecuteScalar() as string;
        if (metaValue is null)
        {
            throw new InvalidDataException("conversation meta is missing");
        }

        var metaJson = Encoding.UTF8.GetString(Convert.FromHexString(metaValue));
        var meta = JsonNode.Parse(metaJson) as JsonObject
            ?? throw new InvalidDataException("conversation meta is missing");
        if (meta["latestRootBlobId"] is not JsonValue rootValue
            || !rootValue.TryGetValue<string>(out var rootId)
            || rootId is null)
        {
            throw new KeyNotFoundException("latestRootBlobId");
        }

        var blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var blobCommand = connection.CreateCommand();
        blobCommand.CommandText = "SELECT id, data FROM blobs ORDER BY rowid";
        using var reader = blobCommand.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var data = (byte[])reader.GetValue(1);
            blobs[id] = data;
        }

        if (!blobs.TryGetValue(rootId, out var root))
        {
            throw new InvalidDataException("root blob is missing");
        }

        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var blobId in blobs.Keys)
        {
            known[blobId] = blobId;
        }

        var conversation = new Conversation();
        if (IsAlignedIdList(root, blobs))
        {
            LoadRaw(conversation, root, blobs, known, rootId);
        }
        else
        {
            var layout = ProtobufLayout(root, blobs);
            if (layout is null)
            {
                LoadRaw(conversation, root, blobs, known, rootId);
            }
            else
            {
                LoadProtobuf(conversation, blobs, rootId, layout.Value.Live, layout.Value.Checkpoints);
            }
        }

        connection.Close();
        SqliteConnection.ClearPool(connection);
        return conversation;
    }

    private static void LoadRaw(
        Conversation conversation,
        byte[] root,
        Dictionary<string, byte[]> blobs,
        Dictionary<string, string> known,
        string rootId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var children = new List<(string Id, byte[]? Data)>();
        foreach (var childId in OrderedIds(root, known))
        {
            if (seen.Contains(childId) || childId == rootId)
            {
                continue;
            }

            seen.Add(childId);
            blobs.TryGetValue(childId, out var data);
            children.Add((childId, data));
        }

        var pending = new List<(string Id, byte[]? Data)>();
        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (childId, data) in children)
        {
            var message = RoleMessage(data);
            if (message is null)
            {
                pending.Add((childId, data));
                continue;
            }

            conversation.Messages.Add(message);
            messageIds.Add(childId);
        }

        foreach (var (childId, data) in pending)
        {
            if (data is null)
            {
                conversation.Skipped.Add(childId);
            }
            else if (IsRepeatIndex(data, messageIds) || CheckpointMessageIds(data, blobs) is not null)
            {
                continue;
            }
            else if (data.AsSpan().StartsWith(PngMagic))
            {
                conversation.UnlinkedImages.Add(childId);
            }
            else
            {
                conversation.Skipped.Add(childId);
            }
        }

        AddUnlinkedImages(conversation, blobs, seen);
    }

    private static void LoadProtobuf(
        Conversation conversation,
        Dictionary<string, byte[]> blobs,
        string rootId,
        List<string> liveIds,
        List<string> checkpointIds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var checkpointId in checkpointIds)
        {
            blobs.TryGetValue(checkpointId, out var checkpoint);
            foreach (var messageId in CheckpointMessageIds(checkpoint, blobs) ?? [])
            {
                AppendMessage(conversation, blobs, messageId, seen);
            }
        }

        foreach (var childId in liveIds)
        {
            if (childId == rootId)
            {
                continue;
            }

            blobs.TryGetValue(childId, out var data);
            if (AppendMessage(conversation, blobs, childId, seen))
            {
                continue;
            }

            if (data is not null && CheckpointMessageIds(data, blobs) is not null)
            {
                continue;
            }

            if (data is not null && data.AsSpan().StartsWith(PngMagic))
            {
                conversation.UnlinkedImages.Add(childId);
                continue;
            }

            conversation.Skipped.Add(childId);
        }

        var covered = new HashSet<string>(seen, StringComparer.Ordinal);
        covered.UnionWith(checkpointIds);
        covered.UnionWith(liveIds);
        AddUnlinkedImages(conversation, blobs, covered);
    }

    private static bool AppendMessage(
        Conversation conversation,
        Dictionary<string, byte[]> blobs,
        string messageId,
        HashSet<string> seen)
    {
        if (seen.Contains(messageId))
        {
            return true;
        }

        blobs.TryGetValue(messageId, out var data);
        var message = RoleMessage(data);
        if (message is null)
        {
            return false;
        }

        seen.Add(messageId);
        conversation.Messages.Add(message);
        return true;
    }

    private static void AddUnlinkedImages(
        Conversation conversation,
        Dictionary<string, byte[]> blobs,
        HashSet<string> seen)
    {
        var referenced = new HashSet<string>(seen, StringComparer.Ordinal);
        foreach (var message in conversation.Messages)
        {
            var encoded = message.ToJsonString();
            foreach (var blobId in blobs.Keys)
            {
                if (encoded.Contains(blobId, StringComparison.Ordinal))
                {
                    referenced.Add(blobId);
                }
            }
        }

        foreach (var (blobId, data) in blobs)
        {
            if (!data.AsSpan().StartsWith(PngMagic))
            {
                continue;
            }

            if (referenced.Contains(blobId) || seen.Contains(blobId))
            {
                continue;
            }

            conversation.UnlinkedImages.Add(blobId);
        }
    }

    private static bool IsAlignedIdList(byte[] blob, Dictionary<string, byte[]> blobs)
    {
        if (blob.Length < 32 || blob.Length % 32 != 0)
        {
            return false;
        }

        for (var index = 0; index < blob.Length; index += 32)
        {
            if (!blobs.ContainsKey(Convert.ToHexString(blob, index, 32).ToLowerInvariant()))
            {
                return false;
            }
        }

        return true;
    }

    private static (List<string> Live, List<string> Checkpoints)? ProtobufLayout(
        byte[] root,
        Dictionary<string, byte[]> blobs)
    {
        var fields = ProtobufFields(root);
        if (fields is null)
        {
            return null;
        }

        var live = new List<string>();
        var checkpoints = new List<string>();
        foreach (var (fieldNumber, value) in fields)
        {
            if (value is not byte[] bytes || bytes.Length != 32)
            {
                continue;
            }

            var blobId = Convert.ToHexString(bytes).ToLowerInvariant();
            if (!blobs.ContainsKey(blobId))
            {
                continue;
            }

            if (fieldNumber == 1)
            {
                live.Add(blobId);
            }
            else if (CheckpointMessageIds(blobs[blobId], blobs) is not null)
            {
                checkpoints.Add(blobId);
            }
        }

        if (!live.Any(blobId => RoleMessage(blobs.GetValueOrDefault(blobId)) is not null))
        {
            return null;
        }

        return (live, checkpoints);
    }

    private static List<string>? CheckpointMessageIds(byte[]? data, Dictionary<string, byte[]> blobs)
    {
        if (data is null || data.Length == 0 || RoleMessage(data) is not null)
        {
            return null;
        }

        if (data.Length % 32 == 0)
        {
            var rawIds = new List<string>();
            for (var index = 0; index < data.Length; index += 32)
            {
                rawIds.Add(Convert.ToHexString(data, index, 32).ToLowerInvariant());
            }

            if (rawIds.Count > 0 && rawIds.All(blobId => RoleMessage(blobs.GetValueOrDefault(blobId)) is not null))
            {
                return rawIds;
            }
        }

        var fields = ProtobufFields(data);
        if (fields is null)
        {
            return null;
        }

        var messageIds = new List<string>();
        foreach (var (fieldNumber, value) in fields)
        {
            if (fieldNumber == 1 && value is byte[] bytes && bytes.Length == 32)
            {
                var blobId = Convert.ToHexString(bytes).ToLowerInvariant();
                if (blobs.ContainsKey(blobId))
                {
                    messageIds.Add(blobId);
                }
            }
        }

        if (messageIds.Count == 0
            || messageIds.Any(blobId => RoleMessage(blobs.GetValueOrDefault(blobId)) is null))
        {
            return null;
        }

        return messageIds;
    }

    private static List<(int Field, object Value)>? ProtobufFields(byte[] data)
    {
        if (data.Length == 0)
        {
            return null;
        }

        var fields = new List<(int Field, object Value)>();
        var index = 0;
        try
        {
            while (index < data.Length)
            {
                var (key, next) = ReadVarint(data, index);
                index = next;
                var fieldNumber = (int)(key >> 3);
                var wire = (int)(key & 7);
                if (fieldNumber == 0 || fieldNumber > 200)
                {
                    return null;
                }

                if (wire == 0)
                {
                    (var value, index) = ReadVarint(data, index);
                    fields.Add((fieldNumber, value));
                }
                else if (wire == 2)
                {
                    (var length, var after) = ReadVarint(data, index);
                    index = after;
                    if (length < 0 || index + (int)length > data.Length)
                    {
                        return null;
                    }

                    var slice = data[index..(index + (int)length)];
                    fields.Add((fieldNumber, slice));
                    index += (int)length;
                }
                else if (wire == 1)
                {
                    if (index + 8 > data.Length)
                    {
                        return null;
                    }

                    index += 8;
                }
                else if (wire == 5)
                {
                    if (index + 4 > data.Length)
                    {
                        return null;
                    }

                    index += 4;
                }
                else
                {
                    return null;
                }
            }
        }
        catch (InvalidDataException)
        {
            return null;
        }

        if (index != data.Length)
        {
            return null;
        }

        return fields;
    }

    private static (ulong Value, int Index) ReadVarint(byte[] data, int index)
    {
        ulong value = 0;
        var shift = 0;
        while (index < data.Length && shift <= 63)
        {
            var current = data[index];
            index++;
            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return (value, index);
            }

            shift += 7;
        }

        throw new InvalidDataException("bad varint");
    }

    private static List<string> OrderedIds(byte[] blob, Dictionary<string, string> known)
    {
        var byBytes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in known.Keys)
        {
            byBytes[id] = id;
        }

        var found = new List<string>();
        var index = 0;
        var limit = blob.Length - 31;
        while (index < limit)
        {
            var hex = Convert.ToHexString(blob, index, 32).ToLowerInvariant();
            if (!byBytes.TryGetValue(hex, out var blobId))
            {
                index++;
                continue;
            }

            found.Add(blobId);
            index += 32;
        }

        return found;
    }

    private static bool IsRepeatIndex(byte[] data, HashSet<string> messageIds)
    {
        if (messageIds.Count == 0 || data.Length % 32 != 0)
        {
            return false;
        }

        for (var offset = 0; offset < data.Length; offset += 32)
        {
            if (!messageIds.Contains(Convert.ToHexString(data, offset, 32).ToLowerInvariant()))
            {
                return false;
            }
        }

        return true;
    }

    public static JsonObject? RoleMessage(byte[]? data)
    {
        if (data is null || data.Length == 0 || (data[0] != (byte)'{' && data[0] != (byte)'['))
        {
            return null;
        }

        try
        {
            var payload = JsonNode.Parse(data);
            if (payload is JsonObject obj
                && obj["role"] is JsonValue role
                && role.TryGetValue<string>(out _))
            {
                return obj;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public static string Digest(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }
}
