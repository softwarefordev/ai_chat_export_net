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
        Assert.Equal(0, Exporter.Run(["--id", "chat-1", "--chats-root", _chats, "--out", cliOut, "--markdown"]));
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
            Assert.Equal(0, Exporter.Run(["--id", "chat-1", "--chats-root", _chats, "--out", dir, "--theme", theme]));
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
}
