using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AiChatExport.Tests;

public class CopilotTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));

    public CopilotTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Registry_labels_keep_the_cursor_columns()
    {
        Assert.Equal(["cli", "ide", "cp-cli"], SessionSources.All.Select(item => item.Id).ToList());
        Assert.Equal("Cu-CLI", SessionSources.Column("cli"));
        Assert.Equal("Cu-IDE", SessionSources.Column("ide"));
        var chat = new Chat("cp-1", "Map", "C:/work", "work", 0, true, ".", "cp-cli");
        Assert.Equal("cp-cli", chat.Source);
        Assert.Equal("Cp-CLI", chat.SourceLabel);
        Assert.Equal(6, Picker.SourceWidth);
    }

    [Fact]
    public void Copilot_session_joins_the_newest_first_list()
    {
        var chats = Path.Combine(_root, "chats");
        Support.WriteChat(chats, "cli-chat", _root, "Older chat", 1_000, withStore: false);
        var launch = Path.Combine(_root, "SoggyMapMaker");
        Directory.CreateDirectory(launch);
        var copilot = Path.Combine(_root, "copilot");
        WriteSession(copilot, "cp-chat", "River map", launch, "2026-10-24T22:30:00.000Z", "River map");
        Directory.CreateDirectory(Path.Combine(copilot, "session-state", "orphan"));
        WriteSession(Path.Combine(_root, "other-copilot"), "hidden", "Hidden");

        var sessions = Catalog.LoadSessions(chats, Path.Combine(_root, "missing-projects"), copilotRoot: copilot);
        Assert.Equal(["cp-chat", "cli-chat"], sessions.Select(item => item.Id).ToList());
        var row = sessions[0];
        Assert.Equal("Cp-CLI", row.SourceLabel);
        Assert.Equal("River map", row.Title);
        Assert.Equal("SoggyMapMaker", row.WorkspaceName);
        var expected = Catalog.FormatTimestamp(DateTimeOffset.Parse("2026-10-24T22:30:00.000Z").ToUnixTimeMilliseconds());
        Assert.Equal(expected, row.UpdatedLabel);
        Assert.Equal(["cp-chat"], Catalog.FilterWorkspace(sessions, launch).Select(item => item.Id).ToList());
        Assert.DoesNotContain(sessions, item => item.Id == "orphan");
        Assert.DoesNotContain(sessions, item => item.Id == "hidden");

        var kept = Catalog.LoadSessions(chats, Path.Combine(_root, "missing-projects"), copilotRoot: Path.Combine(_root, "missing-copilot"));
        Assert.Equal(["cli-chat"], kept.Select(item => item.Id).ToList());
    }

    [Fact]
    public void Shared_id_title_time_and_unreadable_store()
    {
        var chats = Path.Combine(_root, "chats");
        Support.WriteChat(chats, "same", _root, "Chat copy", withStore: false);
        var copilot = Path.Combine(_root, "copilot");
        WriteSession(copilot, "same", "Copilot copy");
        WriteSession(copilot, "from-line", "  ", userMessage: "First line\nsecond");
        WriteSession(copilot, "from-id", "", userMessage: "  ");
        WriteSession(copilot, "loose", "Loose", cwd: "", updatedAt: "not a time");

        var sessions = Catalog.LoadSessions(chats, Path.Combine(_root, "missing-projects"), copilotRoot: copilot);
        Assert.Equal(2, sessions.Count(item => item.Id == "same"));
        Assert.Equal("Cp-CLI", sessions.Single(item => item.Id == "same" && item.Source == "cp-cli").SourceLabel);
        Assert.Equal("First line", sessions.Single(item => item.Id == "from-line").Title);
        Assert.Null(sessions.Single(item => item.Id == "from-id").Title);
        Assert.Equal("from-id", sessions.Single(item => item.Id == "from-id").DisplayTitle);
        var loose = sessions.Single(item => item.Id == "loose");
        Assert.Equal("not a time", loose.UpdatedLabel);
        Assert.Equal("", loose.WorkspaceName);
        Assert.Empty(Catalog.FilterWorkspace([loose], _root));

        var bad = Path.Combine(_root, "bad-copilot");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "session-store.db"), "not a database");
        var warnings = new List<string>();
        var remaining = Catalog.LoadSessions(chats, Path.Combine(_root, "missing-projects"), warnings, bad);
        Assert.Contains(remaining, item => item.Id == "cli-chat" || item.Source == "cli");
        Assert.DoesNotContain(remaining, item => item.Source == "cp-cli");
        Assert.Contains(warnings, note => note.StartsWith("Unreadable Copilot store:", StringComparison.Ordinal));
    }

    [Fact]
    public void Copilot_id_writes_event_text_and_leaves_the_store_unchanged()
    {
        var copilot = Path.Combine(_root, "copilot");
        var events = new object[]
        {
            new { type = "system.message", data = new { content = "SYSTEM-ONLY" } },
            new { type = "user.message", data = new { content = "Please add a river." } },
            new { type = "assistant.message", data = new { content = "", reasoningText = "REASON-ONLY", encryptedContent = "SECRET-ENCRYPTED" } },
            new { type = "assistant.message", data = new { content = "The map is ready." } },
            new { type = "user.message", data = new { content = "Add a lake." } },
            new { type = "assistant.message", data = new { content = "The lake is marked." } },
        };
        WriteSession(copilot, "cp-1", "River map", events: events, turns: [("ONLY-IN-TURNS", "ONLY-IN-TURNS-REPLY")], index: "INDEX-MARKER");
        var before = Bytes(copilot, "cp-1");
        var stdout = new StringWriter();
        var code = Exporter.ExportSession(
            Path.Combine(_root, "missing-chats"),
            "cp-1",
            Path.Combine(_root, "out"),
            stdout: stdout,
            copilotRoot: copilot);
        Assert.Equal(0, code);
        var page = File.ReadAllText(Path.Combine(_root, "out", "River map.html"));
        Assert.Contains("Please add a river.", page, StringComparison.Ordinal);
        Assert.Contains("The map is ready.", page, StringComparison.Ordinal);
        Assert.Contains("Add a lake.", page, StringComparison.Ordinal);
        Assert.Contains("The lake is marked.", page, StringComparison.Ordinal);
        Assert.Equal(2, Count(page, "<h2>Agent</h2>"));
        Assert.DoesNotContain("ONLY-IN-TURNS", page, StringComparison.Ordinal);
        Assert.DoesNotContain("SYSTEM-ONLY", page, StringComparison.Ordinal);
        Assert.DoesNotContain("REASON-ONLY", page, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-ENCRYPTED", page, StringComparison.Ordinal);
        Assert.DoesNotContain("INDEX-MARKER", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Cp-CLI", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Cu-CLI", page, StringComparison.Ordinal);
        var after = Bytes(copilot, "cp-1");
        Assert.Equal(before.Database, after.Database);
        Assert.Equal(before.Events, after.Events);
        Assert.Equal(before.Index, after.Index);

        var markdownOut = Path.Combine(_root, "markdown-out");
        Assert.Equal(0, Exporter.ExportSession(Path.Combine(_root, "missing-chats"), "cp-1", markdownOut, writeMarkdown: true, stdout: new StringWriter(), copilotRoot: copilot));
        var markdown = File.ReadAllText(Path.Combine(markdownOut, "River map.md"));
        Assert.Contains("Please add a river.", markdown, StringComparison.Ordinal);
        Assert.Contains("The map is ready.", markdown, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(markdownOut, "River map.html")));
    }

    [Fact]
    public void Empty_assistant_missing_log_and_unreadable_events()
    {
        var copilot = Path.Combine(_root, "copilot");
        WriteSession(copilot, "cp-empty-agent", "Note", events:
        [
            new { type = "user.message", data = new { content = "Just a note." } },
            new { type = "assistant.message", data = new { content = "" } },
        ]);
        var noteOut = Path.Combine(_root, "note-out");
        Assert.Equal(0, Exporter.ExportSession(Path.Combine(_root, "missing-chats"), "cp-empty-agent", noteOut, stdout: new StringWriter(), copilotRoot: copilot));
        var note = File.ReadAllText(Path.Combine(noteOut, "Note.html"));
        Assert.Contains("Just a note.", note, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Agent</h2>", note, StringComparison.Ordinal);

        WriteSession(copilot, "cp-blank", "Blank");
        var blankOut = Path.Combine(_root, "blank-out");
        var blankError = new StringWriter();
        Assert.Equal(1, Exporter.ExportSession(Path.Combine(_root, "missing-chats"), "cp-blank", blankOut, stdout: new StringWriter(), stderr: blankError, copilotRoot: copilot));
        Assert.Contains("Chat has no transcript: cp-blank", blankError.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(blankOut));

        WriteSession(copilot, "cp-turns", "Turns", turns: [("Turn query", "Turn reply")]);
        var turnsOut = Path.Combine(_root, "turns-out");
        Assert.Equal(0, Exporter.ExportSession(Path.Combine(_root, "missing-chats"), "cp-turns", turnsOut, showTools: true, stdout: new StringWriter(), copilotRoot: copilot));
        var turnsPage = File.ReadAllText(Path.Combine(turnsOut, "Turns.html"));
        Assert.Contains("Turn query", turnsPage, StringComparison.Ordinal);
        Assert.Contains("Turn reply", turnsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Read ", turnsPage, StringComparison.Ordinal);

        WriteSession(copilot, "cp-bad-log", "Logged", events: [new { type = "user.message", data = new { content = "ignored" } }], turns: [("Fallback query", "Fallback reply")]);
        File.WriteAllBytes(Path.Combine(copilot, "session-state", "cp-bad-log", "events.jsonl"), [0xFF]);
        var badOut = Path.Combine(_root, "bad-log-out");
        var badError = new StringWriter();
        Assert.Equal(0, Exporter.ExportSession(Path.Combine(_root, "missing-chats"), "cp-bad-log", badOut, stdout: new StringWriter(), stderr: badError, copilotRoot: copilot));
        Assert.Contains("cp-bad-log", badError.ToString(), StringComparison.Ordinal);
        Assert.Contains("Unreadable Copilot events", badError.ToString(), StringComparison.Ordinal);
        var fallback = File.ReadAllText(Path.Combine(badOut, "Logged.html"));
        Assert.Contains("Fallback query", fallback, StringComparison.Ordinal);
        Assert.Contains("Fallback reply", fallback, StringComparison.Ordinal);
    }

    [Fact]
    public void Tools_and_verbose_detail_follow_the_event_log()
    {
        var copilot = Path.Combine(_root, "copilot");
        WriteSession(copilot, "cp-tools", "Tools", events:
        [
            new { type = "system.message", data = new { content = "SYSTEM-ONLY" } },
            new { type = "user.message", data = new { content = "Please add a river." } },
            new { type = "assistant.message", data = new { content = "", reasoningText = "REASON-ONLY", encryptedContent = "SECRET-ENCRYPTED" } },
            new { type = "tool.execution_start", data = new { toolName = "view", arguments = new { path = "maps/river.txt" } } },
            new { type = "tool.execution_complete", data = new { result = "TOOL-RESULT-SECRET" } },
            new { type = "tool.execution_start", data = new { toolName = "glob", arguments = new { pattern = "**/*.map" } } },
            new { type = "tool.execution_start", data = new { toolName = "powershell", arguments = new { description = "List the map files", command = "secret-command-text" } } },
            new { type = "tool.execution_start", data = new { toolName = "grep", arguments = new { pattern = "river" } } },
            new { type = "tool.execution_start", data = new { toolName = "create", arguments = new { path = "maps/lake.txt" } } },
            new { type = "assistant.message", data = new { content = "The map is ready." } },
        ]);
        var toolsOut = Path.Combine(_root, "tools-out");
        Assert.Equal(0, Exporter.ExportSession(Path.Combine(_root, "missing-chats"), "cp-tools", toolsOut, showTools: true, stdout: new StringWriter(), copilotRoot: copilot));
        var toolsPage = File.ReadAllText(Path.Combine(toolsOut, "Tools.html"));
        Assert.Contains("Read maps/river.txt", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Glob **/*.map", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Ran List the map files", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Grep river", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Create maps/lake.txt", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("TOOL-RESULT-SECRET", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-command-text", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("REASON-ONLY", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("SYSTEM-ONLY", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-ENCRYPTED", toolsPage, StringComparison.Ordinal);

        var verboseOut = Path.Combine(_root, "verbose-out");
        Assert.Equal(0, Exporter.ExportSession(Path.Combine(_root, "missing-chats"), "cp-tools", verboseOut, verbose: true, stdout: new StringWriter(), copilotRoot: copilot));
        var verbosePage = File.ReadAllText(Path.Combine(verboseOut, "Tools.html"));
        Assert.Contains("Read maps/river.txt", verbosePage, StringComparison.Ordinal);
        Assert.Contains("REASON-ONLY", verbosePage, StringComparison.Ordinal);
        Assert.Contains("TOOL-RESULT-SECRET", verbosePage, StringComparison.Ordinal);
        Assert.Contains("SYSTEM-ONLY", verbosePage, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-ENCRYPTED", verbosePage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-command-text", verbosePage, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_flag_opens_on_copilot_and_rejects_unknown_values()
    {
        var copilot = Path.Combine(_root, "copilot");
        var launch = Path.Combine(_root, "launch");
        Directory.CreateDirectory(launch);
        WriteSession(copilot, "cp-1", "River map", launch, userMessage: "Please add a river.");
        var chats = Path.Combine(_root, "chats");
        Support.WriteChat(chats, "cli-chat", launch, "Fantasy Map Maker", withStore: false);
        var sessions = Catalog.LoadSessions(chats, Path.Combine(_root, "missing-projects"), copilotRoot: copilot);
        var stdout = new StringWriter();
        var code = Exporter.ExportWithPicker(
            sessions,
            "terminal-dark",
            ["esc"],
            launch,
            Path.Combine(_root, "picker-out"),
            false,
            chats,
            stdout,
            copilotRoot: copilot,
            sources: SessionSources.Allowed(["copilot-cli"]));
        Assert.Equal(0, code);
        Assert.Contains("Copilot-CLI *", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("River map", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Fantasy Map Maker", stdout.ToString(), StringComparison.Ordinal);

        var error = Console.Error;
        var errors = new StringWriter();
        Console.SetError(errors);
        try
        {
            Assert.Equal(1, Exporter.Run([
                "--id", "cp-1",
                "--source", "cursor-cli",
                "--chats-root", chats,
                "--projects-root", Path.Combine(_root, "missing-projects"),
                "--copilot-root", copilot,
                "--out", Path.Combine(_root, "excluded"),
            ]));
            Assert.Equal(2, Exporter.Run([
                "--id", "cp-1",
                "--source", "other",
                "--chats-root", chats,
                "--copilot-root", copilot,
                "--out", Path.Combine(_root, "rejected"),
            ]));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Contains("Chat not found", errors.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root, "excluded")));
        Assert.False(Directory.Exists(Path.Combine(_root, "rejected")));
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static (byte[] Database, byte[] Events, byte[] Index) Bytes(string root, string sessionId)
    {
        var folder = Path.Combine(root, "session-state", sessionId);
        var events = Path.Combine(folder, "events.jsonl");
        var index = Path.Combine(folder, "checkpoints", "index.md");
        return (
            File.ReadAllBytes(Path.Combine(root, "session-store.db")),
            File.Exists(events) ? File.ReadAllBytes(events) : [],
            File.Exists(index) ? File.ReadAllBytes(index) : []);
    }

    private static void WriteSession(
        string root,
        string sessionId,
        string summary,
        string cwd = "",
        string updatedAt = "2026-10-24T22:30:00.000Z",
        string? userMessage = null,
        IReadOnlyList<(string User, string? Assistant)>? turns = null,
        IReadOnlyList<object>? events = null,
        string? index = null)
    {
        Directory.CreateDirectory(root);
        var database = Path.Combine(root, "session-store.db");
        var fresh = !File.Exists(database);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database }.ConnectionString);
        connection.Open();
        if (fresh)
        {
            using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE sessions (id TEXT, cwd TEXT, summary TEXT, updated_at TEXT);
                CREATE TABLE turns (session_id TEXT, turn_index INTEGER, user_message TEXT, assistant_response TEXT);
                """;
            create.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO sessions (id, cwd, summary, updated_at) VALUES ($id, $cwd, $summary, $updated)";
            insert.Parameters.AddWithValue("$id", sessionId);
            insert.Parameters.AddWithValue("$cwd", cwd);
            insert.Parameters.AddWithValue("$summary", summary);
            insert.Parameters.AddWithValue("$updated", updatedAt);
            insert.ExecuteNonQuery();
        }

        var rows = turns?.ToList() ?? [];
        if (userMessage is not null)
        {
            rows.Insert(0, (userMessage, null));
        }

        for (var offset = 0; offset < rows.Count; offset++)
        {
            using var turn = connection.CreateCommand();
            turn.CommandText = "INSERT INTO turns (session_id, turn_index, user_message, assistant_response) VALUES ($id, $index, $user, $assistant)";
            turn.Parameters.AddWithValue("$id", sessionId);
            turn.Parameters.AddWithValue("$index", offset);
            turn.Parameters.AddWithValue("$user", rows[offset].User);
            turn.Parameters.AddWithValue("$assistant", (object?)rows[offset].Assistant ?? DBNull.Value);
            turn.ExecuteNonQuery();
        }

        connection.Close();
        SqliteConnection.ClearPool(connection);

        var folder = Path.Combine(root, "session-state", sessionId);
        Directory.CreateDirectory(folder);
        if (events is not null)
        {
            File.WriteAllText(Path.Combine(folder, "events.jsonl"), string.Join('\n', events.Select(item => JsonSerializer.Serialize(item))) + "\n");
        }

        if (index is not null)
        {
            var checkpoint = Path.Combine(folder, "checkpoints");
            Directory.CreateDirectory(checkpoint);
            File.WriteAllText(Path.Combine(checkpoint, "index.md"), index);
        }
    }
}
