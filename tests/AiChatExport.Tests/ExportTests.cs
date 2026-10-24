namespace AiChatExport.Tests;

public class ExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));
    private readonly string _chats;
    private readonly string _out;
    private readonly List<System.Text.Json.Nodes.JsonObject> _messages;
    private readonly List<string> _prompts;

    public ExportTests()
    {
        _chats = Path.Combine(_root, "chats");
        _out = Path.Combine(_root, "out");
        Directory.CreateDirectory(_out);
        (_messages, _prompts) = Support.SampleMessages();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Chat(string chatId = "chat-1", string? title = "Fantasy Map Maker", bool includeTitle = true, bool withStore = true)
    {
        Support.WriteChat(_chats, chatId, _root, title, 1_000, _messages, _prompts, includeTitle: includeTitle, withStore: withStore);
    }

    [Fact]
    public void Writes_html_and_leaves_an_existing_markdown_file()
    {
        Chat();
        var htmlPath = Path.Combine(_out, "Fantasy Map Maker.html");
        var markdownPath = Path.Combine(_out, "Fantasy Map Maker.md");
        var neighbor = Path.Combine(_out, "notes.txt");
        File.WriteAllText(htmlPath, "OLD_HTML");
        File.WriteAllText(markdownPath, "OLD_MD");
        File.WriteAllText(neighbor, "keep-me");
        var stdout = new StringWriter();
        var code = Exporter.ExportSession(_chats, "chat-1", _out, stdout: stdout);
        Assert.Equal(0, code);
        Assert.Contains(Path.GetFullPath(htmlPath), stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFullPath(markdownPath), stdout.ToString(), StringComparison.Ordinal);
        var html = File.ReadAllText(htmlPath);
        Assert.Contains("Please add a river tool.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("OLD_HTML", html, StringComparison.Ordinal);
        Assert.Equal("OLD_MD", File.ReadAllText(markdownPath));
        Assert.Equal("keep-me", File.ReadAllText(neighbor));
        Assert.Contains("#071207", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Former_theme_names_are_rejected_and_write_nothing()
    {
        var error = Console.Error;
        Console.SetError(new StringWriter());
        try
        {
            Assert.Equal(2, Exporter.Run(["--theme", "dark", "--id", "chat-1", "--out", _out, "--chats-root", _chats]));
            Assert.Equal(2, Exporter.Run(["--theme", "light", "--id", "chat-1", "--out", _out, "--chats-root", _chats]));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Empty(Directory.GetFileSystemEntries(_out));
    }

    [Fact]
    public void Markdown_flag_writes_markdown_and_leaves_html()
    {
        Chat();
        var htmlPath = Path.Combine(_out, "Fantasy Map Maker.html");
        var markdownPath = Path.Combine(_out, "Fantasy Map Maker.md");
        File.WriteAllText(htmlPath, "OLD_HTML");
        File.WriteAllText(markdownPath, "OLD_MD");
        var stdout = new StringWriter();
        var code = Exporter.ExportSession(_chats, "chat-1", _out, writeMarkdown: true, stdout: stdout);
        Assert.Equal(0, code);
        Assert.DoesNotContain(Path.GetFullPath(htmlPath), stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(markdownPath), stdout.ToString(), StringComparison.Ordinal);
        Assert.Equal("OLD_HTML", File.ReadAllText(htmlPath));
        var markdown = File.ReadAllText(markdownPath);
        Assert.Contains("Please add a river tool.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("#161616", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("#f7f7f4", markdown, StringComparison.Ordinal);
        var cliOut = Path.Combine(_out, "cli");
        Assert.Equal(0, Exporter.Run(["--id", "chat-1", "--chats-root", _chats, "--projects-root", Path.Combine(_root, "no-projects"), "--copilot-root", Path.Combine(_root, "no-copilot"), "--out", cliOut, "--markdown"]));
        Assert.True(File.Exists(Path.Combine(cliOut, "Fantasy Map Maker.md")));
        Assert.False(File.Exists(Path.Combine(cliOut, "Fantasy Map Maker.html")));
    }

    [Fact]
    public void Name_conflicts_with_out_and_requires_id()
    {
        Chat();
        var target = Path.Combine(_root, "blocked.html");
        var stderr = new StringWriter();
        var previous = Console.Error;
        Console.SetError(stderr);
        try
        {
            var code = Exporter.Run(["--id", "chat-1", "--chats-root", Path.Combine(_root, "missing"), "--name", target, "--out", _out]);
            Assert.Equal(1, code);
        }
        finally
        {
            Console.SetError(previous);
        }

        Assert.Contains("--name", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Chat not found", stderr.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(target));

        stderr = new StringWriter();
        Console.SetError(stderr);
        try
        {
            var code = Exporter.Run(["--chats-root", Path.Combine(_root, "missing"), "--name", Path.Combine(_root, "picker-name.html"), "--out", _out]);
            Assert.Equal(1, code);
        }
        finally
        {
            Console.SetError(previous);
        }

        Assert.Contains("`--name` requires `--id`.", stderr.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_root, "picker-name.html")));
    }

    [Fact]
    public void Name_writes_that_path_and_untitled_chat_uses_id()
    {
        Chat();
        var target = Path.Combine(_root, "nested", "deep", "page.html");
        var stdout = new StringWriter();
        Assert.Equal(0, Exporter.ExportSession(_chats, "chat-1", _out, outputPath: target, stdout: stdout));
        Assert.True(File.Exists(target));
        Assert.Contains(Path.GetFullPath(target), stdout.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_out, "Fantasy Map Maker.html")));

        Chat("id-only", title: null, includeTitle: false);
        Assert.Equal(0, Exporter.ExportSession(_chats, "id-only", _out, stdout: new StringWriter()));
        Assert.True(File.Exists(Path.Combine(_out, "id-only.html")));
        Assert.Equal("Map Maker", Exporter.SafeStem("Map: \"Maker\"", "id"));
    }

    [Fact]
    public void Missing_conversation_and_unknown_id_write_nothing()
    {
        Support.WriteChat(_chats, "empty", _root, "Empty", withStore: false);
        var stderr = new StringWriter();
        Assert.Equal(1, Exporter.ExportSession(_chats, "empty", _out, stderr: stderr));
        Assert.Contains("no transcript", stderr.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_out));

        stderr = new StringWriter();
        Assert.Equal(1, Exporter.ExportSession(_chats, "missing", _out, stderr: stderr));
        Assert.Contains("Chat not found", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Terminal_shows_dots_and_a_pipe_stays_quiet()
    {
        Chat();
        var stdout = new StringWriter();
        var terminal = new StringWriter();
        Assert.Equal(0, Exporter.ExportSession(_chats, "chat-1", _out, stdout: stdout, stderr: terminal, stderrIsTerminal: true));
        Assert.Contains(Path.GetFullPath(Path.Combine(_out, "Fantasy Map Maker.html")), stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("...", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("...", terminal.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Fantasy Map Maker.html", terminal.ToString(), StringComparison.Ordinal);

        var quiet = new StringWriter();
        Assert.Equal(0, Exporter.ExportSession(_chats, "chat-1", _out, stdout: new StringWriter(), stderr: quiet));
        Assert.DoesNotContain("...", quiet.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Open_stretch_advances_through_each_frame()
    {
        var terminal = new StringWriter();
        using (var activity = new Activity(terminal, isTerminal: true, TimeSpan.FromMilliseconds(30)))
        {
            activity.Start();
            activity.Start();
            Thread.Sleep(160);
        }

        var text = terminal.ToString();
        var first = text.IndexOf("...", StringComparison.Ordinal);
        Assert.True(first >= 0);
        var second = text.IndexOf("\r..", first + 3, StringComparison.Ordinal);
        Assert.True(second > first);
        var third = text.IndexOf("\r.", second + 3, StringComparison.Ordinal);
        Assert.True(third > second);
    }

    [Fact]
    public void Missing_chat_shows_dots_before_the_error_and_warnings_stay_whole()
    {
        var terminal = new StringWriter();
        Assert.Equal(1, Exporter.ExportSession(_chats, "missing", _out, stdout: new StringWriter(), stderr: terminal, stderrIsTerminal: true));
        var text = terminal.ToString();
        var error = "Chat not found: missing";
        Assert.True(text.IndexOf("...", StringComparison.Ordinal) < text.IndexOf(error, StringComparison.Ordinal));
        Assert.Contains("\r   \r" + error + "\n", text, StringComparison.Ordinal);

        Support.WriteChat(_chats, "bad", _root, "Bad", schema: 2, messages: _messages, prompts: _prompts);
        Chat();
        terminal = new StringWriter();
        Assert.Equal(0, Exporter.ExportSession(_chats, "chat-1", _out, stdout: new StringWriter(), stderr: terminal, stderrIsTerminal: true));
        text = terminal.ToString();
        var warning = "Unsupported schemaVersion 2 for bad";
        var warnAt = text.IndexOf(warning, StringComparison.Ordinal);
        Assert.True(warnAt >= 0);
        Assert.Contains("\r   \r" + warning + "\n", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("...", StringComparison.Ordinal) < warnAt);
        Assert.Contains("...", text[(warnAt + warning.Length)..], StringComparison.Ordinal);
    }

    [Fact]
    public void Reserved_device_stems_use_the_chat_id_and_named_con_writes_nothing()
    {
        var notes = new Chat("abc", "Notes", "c", "w", 0, true, "p");
        Assert.Equal("Notes.html", Picker.ConfirmFileName("PRN", Picker.DefaultFileName(notes, ".html"), ".html"));
        var con = new Chat("abc", "CON", "c", "w", 0, true, "p");
        Assert.Equal("abc.html", Picker.DefaultFileName(con, ".html"));
        Assert.Equal("abc.html", Picker.ConfirmFileName("AUX", Picker.DefaultFileName(con, ".html"), ".html"));
        Assert.Equal("abc", Exporter.SafeStem("CON", "abc"));
        Assert.Equal("abc", Exporter.SafeStem("com1", "abc"));
        Assert.Equal("Console notes", Exporter.SafeStem("Console notes", "abc"));

        Chat("abc", title: "CON");
        var conDir = Path.Combine(_out, "con");
        Assert.Equal(0, Exporter.ExportSession(_chats, "abc", conDir, stdout: new StringWriter()));
        Assert.True(File.Exists(Path.Combine(conDir, "abc.html")));
        Assert.False(File.Exists(Path.Combine(conDir, "CON.html")));

        Chat("com", title: "com1");
        var comDir = Path.Combine(_out, "com");
        Assert.Equal(0, Exporter.ExportSession(_chats, "com", comDir, stdout: new StringWriter()));
        Assert.True(File.Exists(Path.Combine(comDir, "com.html")));

        Chat("plain", title: "Console notes");
        var plainDir = Path.Combine(_out, "plain");
        Assert.Equal(0, Exporter.ExportSession(_chats, "plain", plainDir, stdout: new StringWriter()));
        Assert.True(File.Exists(Path.Combine(plainDir, "Console notes.html")));

        Chat();
        var reserved = Path.Combine(_root, "CON.html");
        var stderr = new StringWriter();
        Assert.Equal(1, Exporter.ExportSession(_chats, "chat-1", _out, outputPath: reserved, stderr: stderr));
        Assert.False(File.Exists(reserved));
        Assert.Contains("reserved device name", stderr.ToString(), StringComparison.Ordinal);
        var ordinary = Path.Combine(_root, "ordinary.html");
        Assert.Equal(0, Exporter.ExportSession(_chats, "chat-1", _out, outputPath: ordinary, stdout: new StringWriter()));
        Assert.True(File.Exists(ordinary));
    }

    [Fact]
    public void Impossible_turn_time_and_odd_json_still_write_the_file()
    {
        var stamp = "Friday, Oct 32, 2026, 11:13 AM (UTC-7)";
        var query = "Hello there";
        var messages = new List<System.Text.Json.Nodes.JsonObject>
        {
            new()
            {
                ["role"] = 4,
                ["content"] = "SECRET_ROLE",
            },
            Support.Message("assistant", new System.Text.Json.Nodes.JsonArray
            {
                new System.Text.Json.Nodes.JsonObject
                {
                    ["type"] = 1,
                    ["text"] = "HIDDEN_NUMERIC",
                },
                Support.TextPart("text", "Visible reply"),
            }),
            Support.Message("user", $"<timestamp>{stamp}</timestamp>\n<user_query>{query}</user_query>"),
        };
        Support.WriteChat(_chats, "odd", _root, "Odd Day", messages: messages, prompts: [query]);
        var dir = Path.Combine(_out, "odd");
        Assert.Equal(0, Exporter.ExportSession(_chats, "odd", dir, stdout: new StringWriter()));
        var html = File.ReadAllText(Path.Combine(dir, "Odd Day.html"));
        Assert.Contains(stamp, html, StringComparison.Ordinal);
        Assert.Contains("Visible reply", html, StringComparison.Ordinal);
        Assert.Contains(query, html, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_ROLE", html, StringComparison.Ordinal);
        Assert.DoesNotContain("HIDDEN_NUMERIC", html, StringComparison.Ordinal);
        var nav = html.Split("<article>", 2)[0];
        Assert.Contains(stamp, nav, StringComparison.Ordinal);
    }

    [Fact]
    public void Unbounded_root_blob_reports_no_transcript()
    {
        var dir = Support.WriteChat(_chats, "huge", _root, "Huge", withStore: false);
        Support.WriteRootBlob(Path.Combine(dir, "store.db"), [0x0A, 0x80, 0x80, 0x80, 0x80, 0x08]);
        var stderr = new StringWriter();
        Assert.Equal(1, Exporter.ExportSession(_chats, "huge", _out, stderr: stderr));
        Assert.Contains("no transcript", stderr.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_out));
    }

    [Fact]
    public void Theme_names_match_help_and_each_theme_exports()
    {
        var listed = string.Join(",", HtmlRenderer.ThemeNames);
        Assert.Contains("{" + listed + "}", Exporter.HelpText, StringComparison.Ordinal);
        Chat();
        foreach (var theme in HtmlRenderer.ThemeNames)
        {
            var dir = Path.Combine(_out, theme);
            Assert.Equal(0, Exporter.Run(["--id", "chat-1", "--chats-root", _chats, "--projects-root", Path.Combine(_root, "no-projects"), "--copilot-root", Path.Combine(_root, "no-copilot"), "--out", dir, "--theme", theme]));
            Assert.True(File.Exists(Path.Combine(dir, "Fantasy Map Maker.html")));
        }

        var error = Console.Error;
        Console.SetError(new StringWriter());
        try
        {
            Assert.Equal(2, Exporter.Run(["--theme", "nope", "--id", "chat-1", "--out", Path.Combine(_out, "nope"), "--chats-root", _chats]));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.False(Directory.Exists(Path.Combine(_out, "nope")));
    }

    [Fact]
    public void Ide_id_writes_html_and_leaves_the_transcript_unchanged()
    {
        var projects = Path.Combine(_root, "projects");
        var launch = Path.Combine(_root, "SoggyMapMaker");
        Directory.CreateDirectory(launch);
        var (jsonl, trusted) = WriteIde(projects, "room", "River map", "The river is drawn.", launch);
        var jsonlBytes = File.ReadAllBytes(jsonl);
        var trustedBytes = File.ReadAllBytes(trusted);
        var stdout = new StringWriter();
        var code = Exporter.Run([
            "--id", "room",
            "--chats-root", Path.Combine(_root, "missing-chats"),
            "--projects-root", projects,
            "--out", _out,
        ]);
        Assert.Equal(0, code);
        var htmlPath = Path.Combine(_out, "River map.html");
        Assert.True(File.Exists(htmlPath));
        Assert.False(File.Exists(Path.Combine(_out, "River map.md")));
        var html = File.ReadAllText(htmlPath);
        Assert.Contains("River map", html, StringComparison.Ordinal);
        Assert.Contains("SoggyMapMaker", html, StringComparison.Ordinal);
        Assert.Contains("The river is drawn.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Cu-CLI", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Cu-IDE", html, StringComparison.Ordinal);
        Assert.Equal(jsonlBytes, File.ReadAllBytes(jsonl));
        Assert.Equal(trustedBytes, File.ReadAllBytes(trusted));
        _ = stdout;

        var markdownOut = Path.Combine(_out, "md");
        Assert.Equal(0, Exporter.Run([
            "--id", "room",
            "--chats-root", Path.Combine(_root, "missing-chats"),
            "--projects-root", projects,
            "--out", markdownOut,
            "--markdown",
        ]));
        Assert.True(File.Exists(Path.Combine(markdownOut, "River map.md")));
        Assert.False(File.Exists(Path.Combine(markdownOut, "River map.html")));
    }

    [Fact]
    public void Shared_id_exports_the_agent_chat()
    {
        var messages = new List<System.Text.Json.Nodes.JsonObject>
        {
            Support.Message("user", "<user_query>\nAGENT_QUERY_TEXT\n</user_query>"),
            Support.Message("assistant", "agent reply"),
        };
        Support.WriteChat(_chats, "shared", _root, "Agent query", 1_000, messages, []);
        WriteIde(Path.Combine(_root, "projects"), "shared", "IDE_QUERY_TEXT", "ide reply", _root);
        var stdout = new StringWriter();
        var code = Exporter.ExportSession(
            _chats,
            "shared",
            _out,
            stdout: stdout,
            projectsRoot: Path.Combine(_root, "projects"));
        Assert.Equal(0, code);
        var html = File.ReadAllText(Path.Combine(_out, "Agent query.html"));
        Assert.Contains("AGENT_QUERY_TEXT", html, StringComparison.Ordinal);
        Assert.DoesNotContain("IDE_QUERY_TEXT", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ide reply", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_flag_filters_the_picker_and_rejects_unknown_values()
    {
        Chat();
        var projects = Path.Combine(_root, "projects");
        WriteIde(projects, "room", "River map", "The river is drawn.", _root);
        var sessions = Catalog.LoadSessions(_chats, projects);
        sessions.Add(new Chat("cp", "Lake map", Path.Combine(_root, "elsewhere"), "elsewhere", 1, true, "p", "cp-cli"));
        var stdout = new StringWriter();
        var code = Exporter.ExportWithPicker(
            sessions,
            "terminal-dark",
            ["esc"],
            _root,
            _out,
            false,
            _chats,
            stdout,
            projectsRoot: projects,
            sources: SessionSources.Allowed(["cursor-cli"]));
        Assert.Equal(0, code);
        Assert.Contains("Cursor-CLI *", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Fantasy Map Maker", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("River map", stdout.ToString(), StringComparison.Ordinal);

        stdout = new StringWriter();
        code = Exporter.ExportWithPicker(
            sessions,
            "terminal-dark",
            ["esc"],
            _root,
            _out,
            false,
            _chats,
            stdout,
            projectsRoot: projects,
            sources: SessionSources.Allowed(["cursor-cli", "cursor-ide"]));
        Assert.Equal(0, code);
        var both = stdout.ToString();
        Assert.Contains("Cursor-CLI *", both, StringComparison.Ordinal);
        Assert.Contains("Cursor-IDE *", both, StringComparison.Ordinal);
        Assert.DoesNotContain("All sources *", both, StringComparison.Ordinal);
        Assert.DoesNotContain("Lake map", both, StringComparison.Ordinal);
        Assert.Contains("Fantasy Map Maker", both, StringComparison.Ordinal);
        Assert.Contains("River map", both, StringComparison.Ordinal);

        stdout = new StringWriter();
        Exporter.ExportWithPicker(
            sessions,
            "terminal-dark",
            ["s", "esc"],
            _root,
            _out,
            false,
            _chats,
            stdout,
            projectsRoot: projects,
            sources: SessionSources.Allowed(["cursor-cli", "cursor-ide"]));
        Assert.Contains("All sources *", stdout.ToString(), StringComparison.Ordinal);

        stdout = new StringWriter();
        Exporter.ExportWithPicker(
            sessions,
            "terminal-dark",
            ["s", "esc"],
            _root,
            _out,
            false,
            _chats,
            stdout,
            projectsRoot: projects,
            sources: SessionSources.Allowed(["cursor-cli"]));
        var moved = stdout.ToString();
        Assert.Contains("Cursor-IDE *", moved, StringComparison.Ordinal);
        Assert.Contains("River map", moved, StringComparison.Ordinal);

        var error = Console.Error;
        var errors = new StringWriter();
        Console.SetError(errors);
        try
        {
            Assert.Equal(1, Exporter.Run([
                "--id", "chat-1",
                "--source", "cursor-ide",
                "--chats-root", _chats,
                "--projects-root", projects,
                "--copilot-root", Path.Combine(_root, "no-copilot"),
                "--out", Path.Combine(_out, "filtered"),
            ]));
            Assert.Equal(2, Exporter.Run([
                "--id", "chat-1",
                "--source", "other",
                "--chats-root", _chats,
                "--projects-root", projects,
                "--out", Path.Combine(_out, "rejected"),
            ]));
        }
        finally
        {
            Console.SetError(error);
        }

        Assert.Contains("Chat not found", errors.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_out, "filtered")));
        Assert.False(Directory.Exists(Path.Combine(_out, "rejected")));
    }

    private static (string Jsonl, string Trusted) WriteIde(
        string projectsRoot,
        string transcriptId,
        string query,
        string reply,
        string workspace)
    {
        var project = Path.Combine(projectsRoot, "demo");
        Directory.CreateDirectory(project);
        var trusted = Path.Combine(project, ".workspace-trusted");
        File.WriteAllText(trusted, System.Text.Json.JsonSerializer.Serialize(new { workspacePath = workspace }));
        var folder = Path.Combine(project, "agent-transcripts", transcriptId);
        Directory.CreateDirectory(folder);
        var jsonl = Path.Combine(folder, transcriptId + ".jsonl");
        var lines = new[]
        {
            System.Text.Json.JsonSerializer.Serialize(new
            {
                role = "user",
                message = new { content = new[] { new { type = "text", text = $"<user_query>\n{query}\n</user_query>" } } },
            }),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                role = "assistant",
                message = new { content = new[] { new { type = "text", text = reply } } },
            }),
        };
        File.WriteAllText(jsonl, string.Join('\n', lines) + "\n");
        return (jsonl, trusted);
    }
}
