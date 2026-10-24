using System.Text.Json;

namespace AiChatExport.Tests;

public class CatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));

    public CatalogTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Timestamp_shape()
    {
        var moment = new DateTime(2026, 8, 15, 16, 42, 0);
        Assert.Equal("Aug 15, 2026 04:42 PM", Catalog.FormatDateTime(moment));
    }

    [Fact]
    public void Titled_chat_workspace_and_time()
    {
        var moment = new DateTime(2026, 8, 15, 16, 42, 0);
        var cwd = Path.Combine(_root, "SoggyMapMaker");
        Directory.CreateDirectory(cwd);
        var updated = new DateTimeOffset(moment).ToUnixTimeMilliseconds();
        Support.WriteChat(Path.Combine(_root, "chats"), "chat-new", cwd, "Fantasy Map Maker", updated, withStore: false);
        var chats = Catalog.Load(Path.Combine(_root, "chats"));
        Assert.Single(chats);
        Assert.Equal("Fantasy Map Maker", chats[0].DisplayTitle);
        Assert.Equal("SoggyMapMaker", chats[0].WorkspaceName);
        Assert.Equal("Aug 15, 2026 04:42 PM", chats[0].UpdatedLabel);
    }

    [Fact]
    public void Missing_title_uses_id()
    {
        Support.WriteChat(Path.Combine(_root, "chats"), "abc-123", _root, includeTitle: false, withStore: false);
        var chats = Catalog.Load(Path.Combine(_root, "chats"));
        Assert.Equal("abc-123", chats[0].DisplayTitle);
    }

    [Fact]
    public void Newest_first()
    {
        var baseDir = Path.Combine(_root, "chats");
        Support.WriteChat(baseDir, "older", _root, "Older", 10, withStore: false);
        Support.WriteChat(baseDir, "newer", _root, "Newer", 20, withStore: false);
        var titles = Catalog.Load(baseDir).Select(chat => chat.DisplayTitle).ToList();
        Assert.Equal(["Newer", "Older"], titles);
    }

    [Fact]
    public void Unsupported_schema_is_omitted()
    {
        Support.WriteChat(Path.Combine(_root, "chats"), "future", _root, schema: 2, withStore: false);
        var warnings = new List<string>();
        var chats = Catalog.Load(Path.Combine(_root, "chats"), warnings);
        Assert.Empty(chats);
        Assert.Contains(warnings, note => note.Contains("Unsupported schemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void Unreadable_metadata_is_omitted()
    {
        Support.WriteChat(Path.Combine(_root, "chats"), "broken", _root, rawMeta: "{not json"u8.ToArray(), withStore: false);
        var warnings = new List<string>();
        var chats = Catalog.Load(Path.Combine(_root, "chats"), warnings);
        Assert.Empty(chats);
        Assert.Contains(warnings, note => note.Contains("Unreadable metadata", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_store_stays_listable()
    {
        Support.WriteChat(Path.Combine(_root, "chats"), "meta-only", _root, "Notes", withStore: false);
        var chats = Catalog.Load(Path.Combine(_root, "chats"));
        Assert.False(chats[0].HasStore);
        Assert.Equal("Notes", chats[0].DisplayTitle);
    }

    [Fact]
    public void This_workspace_keeps_launch_directory()
    {
        var launch = Path.Combine(_root, "launch");
        var other = Path.Combine(_root, "other");
        Directory.CreateDirectory(launch);
        Directory.CreateDirectory(other);
        var baseDir = Path.Combine(_root, "chats");
        Support.WriteChat(baseDir, "here", launch, "Here", withStore: false);
        Support.WriteChat(baseDir, "there", other, "There", withStore: false);
        var kept = Catalog.FilterWorkspace(Catalog.Load(baseDir), launch);
        Assert.Equal(["Here"], kept.Select(chat => chat.DisplayTitle).ToList());
    }

    [Fact]
    public void Windows_workspace_match_is_case_insensitive()
    {
        var launch = Path.Combine(_root, "LaunchDir");
        Directory.CreateDirectory(launch);
        var swapped = new string(launch.Select(character => char.IsUpper(character) ? char.ToLowerInvariant(character) : char.ToUpperInvariant(character)).ToArray());
        var baseDir = Path.Combine(_root, "chats");
        Support.WriteChat(baseDir, "here", swapped, "Here", withStore: false);
        var kept = Catalog.FilterWorkspace(Catalog.Load(baseDir), launch);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(["here"], kept.Select(chat => chat.Id).ToList());
        }
        else
        {
            Assert.Empty(kept);
        }
    }

    [Fact]
    public void Agent_chat_stays_cli_and_a_missing_projects_root_adds_nothing()
    {
        Support.WriteChat(Path.Combine(_root, "chats"), "abc-123", _root, "Notes", withStore: false);
        var warnings = new List<string>();
        var chats = Catalog.LoadSessions(Path.Combine(_root, "chats"), Path.Combine(_root, "missing-projects"), warnings);
        Assert.Single(chats);
        Assert.Equal("cli", chats[0].Source);
        Assert.Equal("Cu-CLI", chats[0].SourceLabel);
        Assert.Equal("Cu-IDE", SessionSources.All.Single(item => item.Id == "ide").Column);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Ide_title_time_workspace_and_projects_root()
    {
        var moment = new DateTime(2026, 8, 15, 16, 42, 0);
        var launch = Path.Combine(_root, "SoggyMapMaker");
        Directory.CreateDirectory(launch);
        var projects = Path.Combine(_root, "projects");
        WriteTranscript(projects, "demo", "titled", ["River map\nsecond line"], launch, moment);
        WriteTranscript(projects, "loose-folder", "loose", ["River map"]);
        WriteTranscript(projects, "plain", "untitled", [], rawLines:
        [
            """{"role":"user","message":{"content":[{"type":"text","text":"<timestamp>Friday</timestamp>"}]}}""",
        ]);
        var other = Path.Combine(_root, "other-projects");
        WriteTranscript(other, "elsewhere", "hidden", ["Not this tree"]);
        var broken = Path.Combine(projects, "demo", "agent-transcripts", "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllBytes(Path.Combine(broken, "broken.jsonl"), [0xFF]);

        var warnings = new List<string>();
        var chats = Catalog.LoadSessions(Path.Combine(_root, "missing-chats"), projects, warnings);
        var titled = chats.Single(chat => chat.Id == "titled");
        Assert.Equal("River map", titled.DisplayTitle);
        Assert.Equal("ide", titled.Source);
        Assert.Equal("Aug 15, 2026 04:42 PM", titled.UpdatedLabel);
        Assert.Equal("SoggyMapMaker", titled.WorkspaceName);
        Assert.Equal(["titled"], Catalog.FilterWorkspace(chats, launch).Select(chat => chat.Id).ToList());

        var loose = chats.Single(chat => chat.Id == "loose");
        Assert.Equal("loose-folder", loose.WorkspaceName);
        Assert.Empty(Catalog.FilterWorkspace([loose], _root));

        Assert.Equal("untitled", chats.Single(chat => chat.Id == "untitled").DisplayTitle);
        Assert.Null(chats.Single(chat => chat.Id == "untitled").Title);
        Assert.DoesNotContain(chats, chat => chat.Id == "hidden");
        Assert.DoesNotContain(chats, chat => chat.Id == "broken");
        Assert.Contains(warnings, note => note.Contains("broken", StringComparison.Ordinal));
    }

    [Fact]
    public void Shared_id_keeps_the_agent_chat_and_newer_ide_sorts_first()
    {
        var chatsRoot = Path.Combine(_root, "chats");
        Support.WriteChat(chatsRoot, "shared", _root, "Agent query", 1_000, withStore: false);
        Support.WriteChat(chatsRoot, "older-chat", _root, "Older chat", 1_000, withStore: false);
        var projects = Path.Combine(_root, "projects");
        WriteTranscript(projects, "demo", "shared", ["Ide copy"], mtime: DateTime.Now);
        var newer = DateTime.Now.AddMinutes(5);
        WriteTranscript(projects, "demo", "fresh-ide", ["Fresh ide"], mtime: newer);

        var chats = Catalog.LoadSessions(chatsRoot, projects);
        Assert.Single(chats, chat => chat.Id == "shared");
        Assert.Equal("cli", chats.Single(chat => chat.Id == "shared").Source);
        Assert.Equal("Agent query", chats.Single(chat => chat.Id == "shared").DisplayTitle);
        Assert.Equal("fresh-ide", chats[0].Id);
    }

    private static void WriteTranscript(
        string projectsRoot,
        string project,
        string transcriptId,
        IReadOnlyList<string> queries,
        string? workspace = null,
        DateTime? mtime = null,
        IReadOnlyList<string>? rawLines = null)
    {
        var projectDir = Path.Combine(projectsRoot, project);
        Directory.CreateDirectory(projectDir);
        if (workspace is not null)
        {
            File.WriteAllText(
                Path.Combine(projectDir, ".workspace-trusted"),
                JsonSerializer.Serialize(new { workspacePath = workspace }));
        }

        var folder = Path.Combine(projectDir, "agent-transcripts", transcriptId);
        Directory.CreateDirectory(folder);
        var lines = rawLines ?? queries.Select(query =>
            JsonSerializer.Serialize(new
            {
                role = "user",
                message = new
                {
                    content = new[]
                    {
                        new { type = "text", text = $"<user_query>\n{query}\n</user_query>" },
                    },
                },
            }));
        var path = Path.Combine(folder, transcriptId + ".jsonl");
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        if (mtime is not null)
        {
            File.SetLastWriteTime(path, mtime.Value);
        }
    }
}
