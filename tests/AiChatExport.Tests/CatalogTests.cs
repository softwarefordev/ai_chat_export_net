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
}
