using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace AiChatExport.Tests;

public class CopilotIdeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));

    public CopilotIdeTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Vscode_session_joins_the_newest_first_list()
    {
        var chats = Path.Combine(_root, "chats");
        Support.WriteChat(chats, "cli-chat", _root, "Older chat", 1_000, withStore: false);
        var launch = Path.Combine(_root, "SoggyMapMaker");
        Directory.CreateDirectory(launch);
        var moment = DateTimeOffset.Parse("2026-10-24T15:45:00Z");
        var code = Path.Combine(_root, "code-user");
        WriteVscode(code, "vsc-chat", "River map", "River map", "The river is mapped.", new Uri(launch).AbsoluteUri, moment.ToUnixTimeMilliseconds());
        var draftDir = Path.Combine(code, "workspaceStorage", "hash", "chatSessions");
        File.WriteAllText(Path.Combine(draftDir, "empty.json"), """{"version":3,"sessionId":"empty","requests":[]}""");

        var sessions = Catalog.LoadSessions(chats, Path.Combine(_root, "missing-projects"), copilotRoot: Path.Combine(_root, "missing-copilot"), vscodeRoot: code);
        Assert.Equal(["vsc-chat", "cli-chat"], sessions.Select(item => item.Id).ToList());
        var row = sessions[0];
        Assert.Equal("cp-ide", row.Source);
        Assert.Equal("Cp-IDE", row.SourceLabel);
        Assert.Equal("River map", row.DisplayTitle);
        Assert.Equal("SoggyMapMaker", row.WorkspaceName);
        Assert.Equal(Catalog.FormatTimestamp(moment.ToUnixTimeMilliseconds()), row.UpdatedLabel);
        Assert.Equal(["vsc-chat"], Catalog.FilterWorkspace(sessions, launch).Select(item => item.Id).ToList());
        Assert.DoesNotContain(sessions, item => item.Id == "empty");

        var kept = Catalog.LoadSessions(chats, null, vscodeRoot: Path.Combine(_root, "missing-vscode"));
        Assert.Equal(["cli-chat"], kept.Select(item => item.Id).ToList());
    }

    [Fact]
    public void Vscode_custom_root_empty_window_shared_id_title_time_and_unreadable()
    {
        WriteVscode(Path.Combine(_root, "one"), "from-one", "One", "One", "Ready.");
        WriteVscode(Path.Combine(_root, "two"), "from-two", "Two", "Two", "Ready.");
        var custom = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, vscodeRoot: Path.Combine(_root, "two"));
        Assert.Equal(["from-two"], custom.Select(item => item.Id).ToList());

        WriteVscode(Path.Combine(_root, "loose-root"), "loose", "Loose", "Loose map", "Ready.", emptyWindow: true);
        var loose = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, vscodeRoot: Path.Combine(_root, "loose-root"));
        Assert.Equal("", loose[0].WorkspaceName);
        Assert.Empty(Catalog.FilterWorkspace(loose, _root));

        var chats = Path.Combine(_root, "chats");
        Support.WriteChat(chats, "shared", _root, "Agent", 5_000, withStore: false);
        WriteVscode(Path.Combine(_root, "shared-root"), "shared", "Copilot", "Copilot", "Ready.", updatedMs: 1_000);
        var shared = Catalog.LoadSessions(chats, null, vscodeRoot: Path.Combine(_root, "shared-root"));
        Assert.Equal(["cli", "cp-ide"], shared.Where(item => item.Id == "shared").Select(item => item.Source).Order().ToList());
        Assert.Equal("Cp-IDE", shared.Single(item => item.Id == "shared" && item.Source == "cp-ide").SourceLabel);

        WriteVscode(Path.Combine(_root, "titles"), "from-line", "   ", "First line\nsecond", "Ready.");
        WriteVscode(Path.Combine(_root, "titles"), "from-id", "", "", "Only the assistant.");
        var titles = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, vscodeRoot: Path.Combine(_root, "titles")).ToDictionary(item => item.Id);
        Assert.Equal("First line", titles["from-line"].DisplayTitle);
        Assert.Null(titles["from-id"].Title);
        Assert.Equal("from-id", titles["from-id"].DisplayTitle);

        var created = DateTimeOffset.Parse("2026-08-01T09:05:00Z").ToUnixTimeMilliseconds();
        WriteVscode(Path.Combine(_root, "created"), "created-only", "Created", "Created", "Ready.", updatedMs: null, createdMs: created);
        var timed = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, vscodeRoot: Path.Combine(_root, "created"));
        Assert.Equal(Catalog.FormatTimestamp(created), timed[0].UpdatedLabel);

        Support.WriteChat(chats, "cli-chat", _root, "Kept", 1_000, withStore: false);
        var brokenDir = Path.Combine(_root, "broken-root", "workspaceStorage", "hash", "chatSessions");
        Directory.CreateDirectory(brokenDir);
        File.WriteAllBytes(Path.Combine(brokenDir, "broken.json"), [0xFF]);
        var warnings = new List<string>();
        var remaining = Catalog.LoadSessions(chats, null, warnings, vscodeRoot: Path.Combine(_root, "broken-root"));
        Assert.Contains(remaining, item => item.Id == "cli-chat");
        Assert.DoesNotContain(remaining, item => item.Id == "broken");
        Assert.Contains(warnings, note => note.StartsWith("Unreadable Copilot IDE session broken:", StringComparison.Ordinal));

        var snapDir = Path.Combine(_root, "snap-root", "workspaceStorage", "hash", "chatSessions");
        Directory.CreateDirectory(snapDir);
        var snapshot = new JsonObject
        {
            ["kind"] = 0,
            ["v"] = new JsonObject
            {
                ["version"] = 3,
                ["sessionId"] = "snap",
                ["customTitle"] = "Snapshot",
                ["creationDate"] = 1_000,
                ["requests"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["message"] = new JsonObject { ["text"] = "From the snapshot" },
                        ["response"] = new JsonArray { new JsonObject { ["value"] = "Ready." } },
                    },
                },
            },
        };
        var mutation = new JsonObject { ["kind"] = 1, ["v"] = "draft only" };
        File.WriteAllText(Path.Combine(snapDir, "snap.jsonl"), snapshot.ToJsonString() + "\n" + mutation.ToJsonString() + "\n");
        var snapped = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, vscodeRoot: Path.Combine(_root, "snap-root"));
        Assert.Equal("Snapshot", snapped[0].DisplayTitle);
    }

    [Fact]
    public void Rider_and_visual_studio_sessions_join_and_leave_bytes()
    {
        var chats = Path.Combine(_root, "chats");
        Support.WriteChat(chats, "cli-chat", _root, "Older chat", 1_000, withStore: false);
        var launch = Path.Combine(_root, "SoggyMapMaker");
        Directory.CreateDirectory(launch);
        var moment = DateTimeOffset.Parse("2026-10-24T16:05:00Z").ToUnixTimeMilliseconds();
        var ide = Path.Combine(_root, "github-copilot");
        var database = WriteNitrite(ide, "rd", "rider-1", "River map", "The river is mapped.", moment, launch);
        var background = WriteNitrite(ide, "rd", "bg-secret", "Hidden", "Hidden.", background: true);
        var before = File.ReadAllBytes(database);
        var backgroundBefore = File.ReadAllBytes(background);
        var sessions = Catalog.LoadSessions(chats, null, copilotIdeRoot: ide);
        Assert.Equal(["rider-1", "cli-chat"], sessions.Select(item => item.Id).ToList());
        Assert.Equal("Cp-IDE", sessions[0].SourceLabel);
        Assert.Equal("SoggyMapMaker", sessions[0].WorkspaceName);
        Assert.Equal(Catalog.FormatTimestamp(moment), sessions[0].UpdatedLabel);
        Assert.Equal(before, File.ReadAllBytes(database));
        Assert.Equal(backgroundBefore, File.ReadAllBytes(background));
        Assert.DoesNotContain(sessions, item => item.Id == "bg-secret");

        var visual = WriteNitrite(Path.Combine(_root, "vs-root"), "vs", "vs-1", "Studio map", "Ready.");
        var listed = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, copilotIdeRoot: Path.Combine(_root, "vs-root"));
        Assert.Equal(["vs-1"], listed.Select(item => item.Id).ToList());
        Assert.Equal("Cp-IDE", listed[0].SourceLabel);
        _ = visual;

        var riderOnly = Path.Combine(_root, "rider-only");
        WriteNitrite(riderOnly, "rd", "kept", "Kept map", "Ready.");
        var withoutVs = Catalog.LoadSessions(chats, null, copilotIdeRoot: riderOnly);
        Assert.Contains(withoutVs, item => item.Id == "kept");
        Assert.Contains(withoutVs, item => item.Id == "cli-chat");

        WriteNitrite(Path.Combine(_root, "left-ide"), "rd", "from-left", "Left", "Ready.");
        WriteNitrite(Path.Combine(_root, "right-ide"), "rd", "from-right", "Right", "Ready.");
        var chosen = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, copilotIdeRoot: Path.Combine(_root, "right-ide"));
        Assert.Equal(["from-right"], chosen.Select(item => item.Id).ToList());

        var missing = Catalog.LoadSessions(chats, null, copilotIdeRoot: Path.Combine(_root, "missing-ide"));
        Assert.Contains(missing, item => item.Id == "cli-chat");
    }

    [Fact]
    public void Agent_host_claims_the_event_log_and_an_unlisted_log_stays_cli()
    {
        var launch = Path.Combine(_root, "cursor_pretty_export");
        Directory.CreateDirectory(launch);
        var sessionId = "a70534c0-1f79-4ddf-bb83-9d63bf2ccb83";
        var copilot = Path.Combine(_root, "copilot");
        WriteEventLog(copilot, sessionId, launch, "Please add a river.", "The map is ready.");
        var database = Path.Combine(_root, "code-user", "globalStorage", "agent-host.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        using (var connection = new SqliteConnection($"Data Source={database}"))
        {
            connection.Open();
            using var create = connection.CreateCommand();
            create.CommandText = "CREATE TABLE chats_v2 (provider_data TEXT, metadata TEXT, tombstoned INTEGER)";
            create.ExecuteNonQuery();
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO chats_v2 (provider_data, metadata, tombstoned) VALUES ($p, $m, $t)";
            insert.Parameters.AddWithValue("$p", JsonSerializer.Serialize(new { sdkSessionId = sessionId }));
            insert.Parameters.AddWithValue("$m", JsonSerializer.Serialize(new { data = new { summary = "Repository summary" } }));
            insert.Parameters.AddWithValue("$t", 0);
            insert.ExecuteNonQuery();
            insert.Parameters.Clear();
            insert.Parameters.AddWithValue("$p", JsonSerializer.Serialize(new { sdkSessionId = "14d8b0da-4000-4895-b6c5-1632d39ae490" }));
            insert.Parameters.AddWithValue("$m", "{}");
            insert.Parameters.AddWithValue("$t", 1);
            insert.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
        var sessions = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, copilotRoot: copilot, vscodeRoot: Path.Combine(_root, "code-user"), copilotIdeRoot: Path.Combine(_root, "no-copilot-ide"));
        Assert.Equal([sessionId], sessions.Select(item => item.Id).ToList());
        Assert.Equal("cp-ide", sessions[0].Source);
        Assert.Equal("Repository summary", sessions[0].DisplayTitle);
        Assert.Equal("cursor_pretty_export", sessions[0].WorkspaceName);

        var orphanRoot = Path.Combine(_root, "orphan-copilot");
        Directory.CreateDirectory(Path.Combine(orphanRoot, "session-state", "orphan"));
        WriteEventLog(orphanRoot, "logged", launch, "Please add a river.", "The map is ready.");
        var unlisted = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, copilotRoot: orphanRoot, vscodeRoot: Path.Combine(_root, "no-vscode"));
        Assert.Equal(["logged"], unlisted.Select(item => item.Id).ToList());
        Assert.Equal("Cp-CLI", unlisted[0].SourceLabel);
        Assert.DoesNotContain(unlisted, item => item.Id == "orphan");
    }

    [Fact]
    public void Rider_event_log_is_one_ide_row_and_exports_the_log()
    {
        var launch = Path.Combine(_root, "ShelfHarbor");
        Directory.CreateDirectory(launch);
        var sessionId = "b70534c0-1f79-4ddf-bb83-9d63bf2ccb83";
        var copilot = Path.Combine(_root, "copilot");
        WriteEventLog(copilot, sessionId, launch, "Please add a river.", "The map is ready.");
        var ide = Path.Combine(_root, "github-copilot");
        var nitrite = WriteLiveNitrite(ide, sessionId, "Please add a river.");
        var before = File.ReadAllBytes(nitrite);
        var sessions = Catalog.LoadSessions(Path.Combine(_root, "missing"), null, copilotRoot: copilot, vscodeRoot: Path.Combine(_root, "no-vscode"), copilotIdeRoot: ide);
        Assert.Equal([sessionId], sessions.Select(item => item.Id).ToList());
        Assert.Equal("cp-ide", sessions[0].Source);
        Assert.Equal("ShelfHarbor", sessions[0].WorkspaceName);

        var stdout = new StringWriter();
        var code = Exporter.ExportSession(
            Path.Combine(_root, "missing"),
            sessionId,
            Path.Combine(_root, "out"),
            stdout: stdout,
            copilotRoot: copilot,
            vscodeRoot: Path.Combine(_root, "no-vscode"),
            copilotIdeRoot: ide);
        Assert.Equal(0, code);
        var page = File.ReadAllText(Directory.GetFiles(Path.Combine(_root, "out"))[0]);
        Assert.Contains("Please add a river.", page, StringComparison.Ordinal);
        Assert.Contains("The map is ready.", page, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(nitrite));
    }

    [Fact]
    public void Export_writes_turn_text_and_leaves_the_session_file()
    {
        var root = Path.Combine(_root, "vscode");
        var path = WriteVscodeDocument(root, "vs-1", new JsonObject
        {
            ["version"] = 3,
            ["sessionId"] = "vs-1",
            ["customTitle"] = "River map",
            ["requesterAvatarIconUri"] = "icon:requester-avatar",
            ["responderAvatarIconUri"] = "icon:responder-avatar",
            ["requests"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["text"] = "Please add a river." },
                    ["response"] = new JsonArray { new JsonObject { ["value"] = "The map is ready." } },
                },
            },
        });
        var before = File.ReadAllBytes(path);
        var stdout = new StringWriter();
        var code = Exporter.ExportSession(Path.Combine(_root, "missing"), "vs-1", Path.Combine(_root, "out"), stdout: stdout, vscodeRoot: root);
        Assert.Equal(0, code);
        var page = File.ReadAllText(Path.Combine(_root, "out", "River map.html"));
        Assert.Contains("Please add a river.", page, StringComparison.Ordinal);
        Assert.Contains("The map is ready.", page, StringComparison.Ordinal);
        Assert.Contains("<h2>Agent</h2>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Cp-IDE", page, StringComparison.Ordinal);
        Assert.DoesNotContain("icon:requester-avatar", page, StringComparison.Ordinal);
        Assert.DoesNotContain("icon:responder-avatar", page, StringComparison.Ordinal);
        Assert.DoesNotContain("http", page, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(Path.Combine(_root, "out", "River map.md")));
    }

    [Fact]
    public void Empty_assistant_and_empty_session()
    {
        var root = Path.Combine(_root, "vscode");
        WriteVscodeDocument(root, "vs-quiet", new JsonObject
        {
            ["version"] = 3,
            ["sessionId"] = "vs-quiet",
            ["customTitle"] = "Quiet",
            ["requests"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["text"] = "Please add a river." },
                    ["response"] = new JsonArray { new JsonObject { ["value"] = "" } },
                },
            },
        });
        var code = Exporter.ExportSession(Path.Combine(_root, "missing"), "vs-quiet", Path.Combine(_root, "quiet"), vscodeRoot: root);
        Assert.Equal(0, code);
        var page = File.ReadAllText(Path.Combine(_root, "quiet", "Quiet.html"));
        Assert.Contains("Please add a river.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Agent</h2>", page, StringComparison.Ordinal);

        WriteVscodeDocument(root, "vs-blank", new JsonObject
        {
            ["version"] = 3,
            ["sessionId"] = "vs-blank",
            ["customTitle"] = "Blank",
            ["requests"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["text"] = "" },
                    ["response"] = new JsonArray { new JsonObject { ["value"] = "" } },
                },
            },
        });
        var errors = new StringWriter();
        var blank = Exporter.ExportSession(Path.Combine(_root, "missing"), "vs-blank", Path.Combine(_root, "blank"), stderr: errors, vscodeRoot: root);
        Assert.Equal(1, blank);
        Assert.Contains("Chat has no transcript: vs-blank", errors.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root, "blank")));
    }

    [Fact]
    public void Tools_and_verbose_captions_omit_command_text()
    {
        var root = Path.Combine(_root, "vscode");
        WriteVscodeDocument(root, "vs-tools", new JsonObject
        {
            ["version"] = 3,
            ["sessionId"] = "vs-tools",
            ["customTitle"] = "Tools",
            ["requests"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["text"] = "Please add a river." },
                    ["response"] = new JsonArray
                    {
                        new JsonObject { ["value"] = "The map is ready." },
                        new JsonObject { ["kind"] = "textEditGroup", ["value"] = "SKIP-TEXT-EDIT" },
                        new JsonObject { ["kind"] = "inlineReference", ["value"] = "SKIP-INLINE" },
                        new JsonObject { ["kind"] = "codeblockUri", ["value"] = "SKIP-CODEBLOCK" },
                        new JsonObject { ["kind"] = "mcpServersStarting" },
                        new JsonObject { ["kind"] = "undoStop" },
                        new JsonObject { ["kind"] = "progressTaskSerialized" },
                        new JsonObject { ["kind"] = "confirmation" },
                        new JsonObject
                        {
                            ["kind"] = "toolInvocationSerialized",
                            ["toolId"] = "copilot_readFile",
                            ["parameters"] = new JsonObject { ["path"] = "maps/river.txt" },
                            ["result"] = "TOOL-RESULT-BODY",
                        },
                        new JsonObject
                        {
                            ["kind"] = "toolInvocationSerialized",
                            ["toolId"] = "copilot_createFile",
                            ["parameters"] = new JsonObject { ["path"] = "maps/lake.txt" },
                        },
                        new JsonObject
                        {
                            ["kind"] = "toolInvocationSerialized",
                            ["toolId"] = "copilot_findFiles",
                            ["parameters"] = new JsonObject { ["pattern"] = "*.map" },
                        },
                        new JsonObject
                        {
                            ["kind"] = "toolInvocationSerialized",
                            ["toolId"] = "run_in_terminal",
                            ["parameters"] = new JsonObject { ["description"] = "Draw the river", ["command"] = "secret-command-text" },
                        },
                        new JsonObject
                        {
                            ["kind"] = "toolInvocationSerialized",
                            ["toolId"] = "copilot_replaceString",
                            ["parameters"] = new JsonObject { ["path"] = "maps/coast.txt" },
                        },
                        new JsonObject
                        {
                            ["kind"] = "toolInvocationSerialized",
                            ["toolId"] = "copilot_multiReplaceString",
                            ["parameters"] = new JsonObject { ["path"] = "maps/ridge.txt" },
                        },
                        new JsonObject
                        {
                            ["kind"] = "toolInvocationSerialized",
                            ["toolId"] = "copilot_listDirectory",
                            ["parameters"] = new JsonObject { ["path"] = "maps/secret-arg" },
                        },
                    },
                },
            },
        });

        var toolsCode = Exporter.ExportSession(Path.Combine(_root, "missing"), "vs-tools", Path.Combine(_root, "tools-out"), showTools: true, vscodeRoot: root);
        var toolsPage = File.ReadAllText(Path.Combine(_root, "tools-out", "Tools.html"));
        Assert.Equal(0, toolsCode);
        Assert.Contains("Read maps/river.txt", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Create maps/lake.txt", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Glob *.map", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Ran Draw the river", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Edit maps/coast.txt", toolsPage, StringComparison.Ordinal);
        Assert.Contains("Edit maps/ridge.txt", toolsPage, StringComparison.Ordinal);
        Assert.Contains("copilot_listDirectory", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("maps/secret-arg", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("TOOL-RESULT-BODY", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-command-text", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("SKIP-TEXT-EDIT", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("SKIP-INLINE", toolsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("SKIP-CODEBLOCK", toolsPage, StringComparison.Ordinal);

        var verboseCode = Exporter.ExportSession(Path.Combine(_root, "missing"), "vs-tools", Path.Combine(_root, "verbose-out"), verbose: true, vscodeRoot: root);
        var verbosePage = File.ReadAllText(Path.Combine(_root, "verbose-out", "Tools.html"));
        Assert.Equal(0, verboseCode);
        Assert.Contains("Read maps/river.txt", verbosePage, StringComparison.Ordinal);
        Assert.Contains("TOOL-RESULT-BODY", verbosePage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-command-text", verbosePage, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_flag_opens_on_copilot_ide_and_rejects_unknown_values()
    {
        var root = Path.Combine(_root, "vscode");
        WriteVscode(root, "vs-1", "River map", "Please add a river.", "The map is ready.");
        var chats = Path.Combine(_root, "chats");
        Directory.CreateDirectory(chats);
        var sessions = Catalog.LoadSessions(chats, Path.Combine(_root, "projects"), copilotRoot: Path.Combine(_root, "no-copilot"), vscodeRoot: root, copilotIdeRoot: Path.Combine(_root, "no-copilot-ide"));
        var stdout = new StringWriter();
        var code = Exporter.ExportWithPicker(
            sessions,
            "terminal-dark",
            ["esc"],
            _root,
            Path.Combine(_root, "picker-out"),
            false,
            chats,
            stdout,
            vscodeRoot: root,
            sources: SessionSources.Allowed(["copilot-ide"]));
        Assert.Equal(0, code);
        Assert.Contains("Copilot-IDE *", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("River map", stdout.ToString(), StringComparison.Ordinal);

        var error = Console.Error;
        var excluded = new StringWriter();
        Console.SetError(excluded);
        try
        {
            Assert.Equal(1, Exporter.Run([
                "--id", "vs-1",
                "--source", "cursor-cli",
                "--chats-root", chats,
                "--projects-root", Path.Combine(_root, "projects"),
                "--copilot-root", Path.Combine(_root, "no-copilot"),
                "--vscode-root", root,
                "--copilot-ide-root", Path.Combine(_root, "no-copilot-ide"),
                "--out", Path.Combine(_root, "excluded"),
            ]));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Contains("Chat not found", excluded.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root, "excluded")));

        var rejected = new StringWriter();
        Console.SetError(rejected);
        try
        {
            Assert.Equal(2, Exporter.Run([
                "--id", "vs-1",
                "--source", "other",
                "--chats-root", chats,
                "--vscode-root", root,
                "--out", Path.Combine(_root, "rejected"),
            ]));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.DoesNotContain("Chat not found", rejected.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root, "rejected")));
    }

    private static void WriteVscode(
        string root,
        string sessionId,
        string title,
        string userText,
        string assistant,
        string? folderUri = null,
        long? updatedMs = 2_000,
        long? createdMs = 1_000,
        bool emptyWindow = false)
    {
        string folder;
        if (emptyWindow)
        {
            folder = Path.Combine(root, "globalStorage", "emptyWindowChatSessions");
        }
        else
        {
            folder = Path.Combine(root, "workspaceStorage", "hash", "chatSessions");
            if (folderUri is not null)
            {
                var workspaceJson = Path.GetFullPath(Path.Combine(folder, "..", "workspace.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(workspaceJson)!);
                File.WriteAllText(workspaceJson, JsonSerializer.Serialize(new { folder = folderUri }));
            }
        }

        Directory.CreateDirectory(folder);
        var document = new JsonObject
        {
            ["version"] = 3,
            ["sessionId"] = sessionId,
            ["customTitle"] = title,
            ["creationDate"] = createdMs,
            ["requests"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["text"] = userText },
                    ["response"] = assistant.Length == 0
                        ? new JsonArray()
                        : new JsonArray { new JsonObject { ["value"] = assistant } },
                },
            },
        };
        if (updatedMs is not null)
        {
            document["lastMessageDate"] = updatedMs;
        }

        File.WriteAllText(Path.Combine(folder, sessionId + ".json"), document.ToJsonString());
    }

    private static string WriteVscodeDocument(string root, string sessionId, JsonObject document)
    {
        var folder = Path.Combine(root, "workspaceStorage", "hash", "chatSessions");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, sessionId + ".json");
        File.WriteAllText(path, document.ToJsonString());
        return path;
    }

    private static string WriteNitrite(
        string root,
        string host,
        string sessionId,
        string userText,
        string assistant,
        long? modifiedMs = null,
        string workspace = "",
        bool background = false)
    {
        var folder = background
            ? Path.Combine(root, host, "bg-agent-sessions", "hostid")
            : Path.Combine(root, host, "chat-agent-sessions", "hostid");
        Directory.CreateDirectory(folder);
        var chunks = new List<byte[]>();
        chunks.Add("H:2,bloc"u8.ToArray());
        chunks.Add(JavaString("sessionId"));
        chunks.Add(JavaString(sessionId));
        if (modifiedMs is not null)
        {
            chunks.Add(JavaString("modifiedAt"));
            chunks.Add(JavaString(modifiedMs.Value.ToString()));
        }

        if (workspace.Length > 0)
        {
            chunks.Add(JavaString("path"));
            chunks.Add(JavaString(workspace));
        }

        if (userText.Length > 0)
        {
            chunks.Add(JavaString("source"));
            chunks.Add(JavaString("user"));
            chunks.Add(JavaString("value"));
            chunks.Add(JavaString(userText));
        }

        if (assistant.Length > 0)
        {
            chunks.Add(JavaString("source"));
            chunks.Add(JavaString("copilot"));
            chunks.Add(JavaString("value"));
            chunks.Add(JavaString(assistant));
        }

        var path = Path.Combine(folder, "copilot-agent-sessions-nitrite.db");
        File.WriteAllBytes(path, chunks.SelectMany(chunk => chunk).ToArray());
        return path;
    }

    private static string WriteLiveNitrite(string root, string sessionId, string userText)
    {
        var folder = Path.Combine(root, "rd", "chat-agent-sessions", "hostid");
        Directory.CreateDirectory(folder);
        var chunks = new[]
        {
            "H:2,bloc"u8.ToArray(),
            JavaString(sessionId),
            JavaString("name"),
            JavaString("value"),
            JavaString(userText),
            JavaString("source"),
            JavaString("copilot"),
            JavaString("copilotcli/default"),
            JavaString("user"),
            JavaString("person"),
            JavaString("conversationId"),
            JavaString(sessionId),
            JavaString("schemaVersion"),
        };
        var path = Path.Combine(folder, "copilot-agent-sessions-nitrite.db");
        File.WriteAllBytes(path, chunks.SelectMany(chunk => chunk).ToArray());
        return path;
    }

    private static void WriteEventLog(string root, string sessionId, string cwd, string userText, string assistant)
    {
        var folder = Path.Combine(root, "session-state", sessionId);
        Directory.CreateDirectory(folder);
        var lines = new object[]
        {
            new { type = "session.start", timestamp = "2026-10-24T22:49:34.229Z", data = new { context = new { cwd } } },
            new { type = "user.message", timestamp = "2026-10-24T22:49:40.000Z", data = new { content = userText } },
            new { type = "assistant.message", timestamp = "2026-10-24T23:50:45.368Z", data = new { content = assistant } },
        };
        File.WriteAllText(Path.Combine(folder, "events.jsonl"), string.Join('\n', lines.Select(line => JsonSerializer.Serialize(line))) + "\n");
    }

    private static byte[] JavaString(string text)
    {
        var raw = Encoding.UTF8.GetBytes(text);
        var bytes = new byte[3 + raw.Length];
        bytes[0] = 0x74;
        bytes[1] = (byte)(raw.Length >> 8);
        bytes[2] = (byte)raw.Length;
        raw.CopyTo(bytes, 3);
        return bytes;
    }
}
