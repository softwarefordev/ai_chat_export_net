using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AiChatExport;

internal sealed record SessionSource(string Id, string Argument, string FilterLabel, string Column);

internal static class SessionSources
{
    public static readonly SessionSource[] All =
    [
        new("cli", "cursor-cli", "Cursor-CLI", "Cu-CLI"),
        new("ide", "cursor-ide", "Cursor-IDE", "Cu-IDE"),
        new("cp-cli", "copilot-cli", "Copilot-CLI", "Cp-CLI"),
        new("cp-ide", "copilot-ide", "Copilot-IDE", "Cp-IDE"),
    ];

    public static bool KnownArgument(string argument) => All.Any(item => item.Argument == argument);

    public static string Column(string sourceId) =>
        All.FirstOrDefault(item => item.Id == sourceId)?.Column ?? sourceId;

    public static HashSet<string>? Allowed(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return null;
        }

        var chosen = new HashSet<string>();
        foreach (var argument in arguments)
        {
            var match = All.FirstOrDefault(item => item.Argument == argument);
            if (match is not null)
            {
                chosen.Add(match.Id);
            }
        }

        return All.All(item => chosen.Contains(item.Id)) ? null : chosen;
    }
}

internal sealed record Chat(
    string Id,
    string? Title,
    string Cwd,
    string WorkspaceName,
    long UpdatedAtMs,
    bool HasStore,
    string Path,
    string Source = "cli",
    string? TimeLabel = null)
{
    public string DisplayTitle => string.IsNullOrEmpty(Title) ? Id : Title!;

    public string UpdatedLabel => TimeLabel ?? Catalog.FormatTimestamp(UpdatedAtMs);

    public string SourceLabel => SessionSources.Column(Source);
}

internal static class Catalog
{
    public static string FormatTimestamp(long updatedAtMs)
    {
        var moment = DateTimeOffset.FromUnixTimeMilliseconds(updatedAtMs).LocalDateTime;
        return FormatDateTime(moment);
    }

    public static string FormatDateTime(DateTime moment)
    {
        var hour = moment.Hour % 12;
        if (hour == 0)
        {
            hour = 12;
        }

        var suffix = moment.Hour < 12 ? "AM" : "PM";
        var month = moment.ToString("MMM", CultureInfo.InvariantCulture);
        return $"{month} {moment.Day}, {moment.Year} {hour:00}:{moment.Minute:00} {suffix}";
    }

    public static string NormalizeDir(string path)
    {
        return Path.GetFullPath(path);
    }

    public static bool SameDirectory(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(NormalizeDir(left), NormalizeDir(right), comparison);
    }

    public static string WorkspaceFolder(string cwd)
    {
        var name = Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? cwd : name;
    }

    public static List<Chat> Load(string root, IList<string>? warnings = null)
    {
        var notes = warnings ?? new List<string>();
        var chats = new List<Chat>();
        if (!Directory.Exists(root))
        {
            return chats;
        }

        foreach (var projectDir in Directory.EnumerateDirectories(root))
        {
            foreach (var chatDir in Directory.EnumerateDirectories(projectDir))
            {
                var metaPath = Path.Combine(chatDir, "meta.json");
                if (!File.Exists(metaPath))
                {
                    continue;
                }

                var name = Path.GetFileName(chatDir);
                JsonElement payload;
                try
                {
                    var text = File.ReadAllText(metaPath);
                    using var document = JsonDocument.Parse(text);
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        notes.Add($"Unreadable metadata for {name}: not an object");
                        continue;
                    }

                    payload = document.RootElement.Clone();
                }
                catch (Exception exc) when (exc is IOException or JsonException or DecoderFallbackException)
                {
                    notes.Add($"Unreadable metadata for {name}: {exc.Message}");
                    continue;
                }

                if (!payload.TryGetProperty("schemaVersion", out var versionNode)
                    || versionNode.ValueKind != JsonValueKind.Number
                    || !versionNode.TryGetInt32(out var version)
                    || version != 1)
                {
                    var shown = versionNode.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? "None"
                        : versionNode.ToString();
                    notes.Add($"Unsupported schemaVersion {shown} for {name}");
                    continue;
                }

                var cwd = "";
                if (payload.TryGetProperty("cwd", out var cwdNode) && cwdNode.ValueKind != JsonValueKind.Null)
                {
                    cwd = cwdNode.ValueKind == JsonValueKind.String
                        ? cwdNode.GetString() ?? ""
                        : cwdNode.ToString();
                }

                string? title = null;
                if (payload.TryGetProperty("title", out var titleNode)
                    && titleNode.ValueKind == JsonValueKind.String)
                {
                    var raw = titleNode.GetString();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        title = raw;
                    }
                }

                long updatedMs = 0;
                if (payload.TryGetProperty("updatedAtMs", out var updatedNode))
                {
                    if (updatedNode.ValueKind == JsonValueKind.Number && updatedNode.TryGetInt64(out var parsed))
                    {
                        updatedMs = parsed;
                    }
                }

                chats.Add(new Chat(
                    name,
                    title,
                    cwd,
                    string.IsNullOrEmpty(cwd) ? "" : WorkspaceFolder(cwd),
                    updatedMs,
                    File.Exists(Path.Combine(chatDir, "store.db")),
                    chatDir));
            }
        }

        SortNewest(chats);
        return chats;
    }

    public static List<Chat> LoadSessions(
        string chatsRoot,
        string? projectsRoot,
        IList<string>? warnings = null,
        string? copilotRoot = null,
        string? vscodeRoot = null,
        string? copilotIdeRoot = null,
        bool includeEmpty = false)
    {
        var notes = warnings ?? new List<string>();
        var chats = Load(chatsRoot, notes);
        if (!string.IsNullOrEmpty(projectsRoot) && Directory.Exists(projectsRoot))
        {
            var known = chats.Select(chat => chat.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var row in LoadIde(projectsRoot, notes))
            {
                if (!known.Contains(row.Id))
                {
                    chats.Add(row);
                }
            }
        }

        if (!string.IsNullOrEmpty(copilotRoot))
        {
            chats.AddRange(CopilotCatalog.Load(copilotRoot, notes));
        }

        if (!string.IsNullOrEmpty(vscodeRoot))
        {
            chats.AddRange(CopilotIde.LoadVscode(vscodeRoot, notes, includeEmpty));
        }

        if (!string.IsNullOrEmpty(copilotIdeRoot))
        {
            chats.AddRange(CopilotIde.LoadNitrite(copilotIdeRoot, notes, includeEmpty, copilotRoot));
        }

        if (!string.IsNullOrEmpty(vscodeRoot) && !string.IsNullOrEmpty(copilotRoot))
        {
            chats.AddRange(CopilotIde.LoadAgentHost(vscodeRoot, copilotRoot, notes, includeEmpty));
        }

        if (!string.IsNullOrEmpty(copilotRoot))
        {
            var known = chats.Select(chat => chat.Id).ToHashSet(StringComparer.Ordinal);
            chats.AddRange(CopilotCatalog.LoadUnlisted(copilotRoot, known, notes));
        }

        SortNewest(chats);
        return chats;
    }

    private static List<Chat> LoadIde(string root, IList<string> notes)
    {
        var rows = new List<Chat>();
        foreach (var projectDir in Directory.EnumerateDirectories(root))
        {
            var transcripts = Path.Combine(projectDir, "agent-transcripts");
            if (!Directory.Exists(transcripts))
            {
                continue;
            }

            foreach (var idDir in Directory.EnumerateDirectories(transcripts))
            {
                var id = Path.GetFileName(idDir);
                var jsonl = Path.Combine(idDir, id + ".jsonl");
                if (!File.Exists(jsonl))
                {
                    continue;
                }

                try
                {
                    var title = IdeTranscript.FirstQueryLine(jsonl);
                    var updated = new DateTimeOffset(File.GetLastWriteTimeUtc(jsonl)).ToUnixTimeMilliseconds();
                    var (cwd, workspace) = IdeTranscript.Workspace(projectDir);
                    rows.Add(new Chat(id, title, cwd, workspace, updated, true, idDir, "ide"));
                }
                catch (Exception exc) when (exc is IOException or UnauthorizedAccessException or JsonException or DecoderFallbackException or ArgumentException)
                {
                    notes.Add($"Unreadable IDE transcript {id}: {exc.Message}");
                }
            }
        }

        return rows;
    }

    private static void SortNewest(List<Chat> chats)
    {
        chats.Sort(static (left, right) =>
        {
            var byTime = right.UpdatedAtMs.CompareTo(left.UpdatedAtMs);
            return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
        });
    }

    public static List<Chat> FilterWorkspace(IEnumerable<Chat> chats, string launchDir)
    {
        return chats.Where(chat => SameDirectory(chat.Cwd, launchDir)).ToList();
    }
}
