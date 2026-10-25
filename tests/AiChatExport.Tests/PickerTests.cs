namespace AiChatExport.Tests;

public class PickerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-chat-export-" + Guid.NewGuid().ToString("n"));
    private readonly string _launch;
    private readonly string _chatsRoot;
    private readonly List<Chat> _chats;

    public PickerTests()
    {
        _launch = Path.Combine(_root, "SoggyMapMaker");
        var other = Path.Combine(_root, "SpiralBench");
        Directory.CreateDirectory(_launch);
        Directory.CreateDirectory(other);
        _chatsRoot = Path.Combine(_root, "chats");
        var moment = new DateTime(2026, 8, 15, 16, 42, 0);
        var newer = new DateTimeOffset(moment).ToUnixTimeMilliseconds();
        var (messages, prompts) = Support.SampleMessages();
        Support.WriteChat(_chatsRoot, "here", _launch, "Fantasy Map Maker", newer, messages, prompts);
        Support.WriteChat(_chatsRoot, "there", other, "Apply Opsx", newer - 1000, messages, prompts);
        _chats = Catalog.Load(_chatsRoot);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private (PickerResult Result, List<string> Frames) Frames(IEnumerable<string> keys, string theme = "terminal-dark", int width = 80, string extension = ".html")
    {
        var frames = new List<string>();
        var result = Picker.Run(_chats, theme, keys, _launch, frames.Add, width: width, extension: extension);
        return (result, frames);
    }

    [Fact]
    public void Columns_share_workspace_and_time_at_an_explicit_width()
    {
        const int width = 72;
        var shortChat = Row("Map", "SoggyMapMaker", "short", new DateTime(2026, 9, 1, 9, 5, 0));
        var longTitle = Row(new string('T', 80), "SpiralBench", "long", new DateTime(2026, 8, 15, 16, 42, 0), "ide");
        var frame = Picker.RenderFrame([longTitle, shortChat], 0, "all", "dark", width: width);
        var lines = frame.Split('\n');
        Assert.Contains("All sources *", lines[1], StringComparison.Ordinal);
        Assert.Equal(new string('-', width), lines[2]);
        Assert.Equal(width, lines[0].Length);
        Assert.Equal("Last updated".PadLeft(Picker.TimeWidth), lines[0][^Picker.TimeWidth..]);
        Assert.StartsWith("> ", lines[3]);
        Assert.StartsWith("  ", lines[4]);
        Assert.EndsWith("...", lines[3].Split("  ", 2)[0]);
        Assert.Equal(longTitle.UpdatedLabel.PadLeft(Picker.TimeWidth), lines[3][^Picker.TimeWidth..]);
        Assert.Equal(shortChat.UpdatedLabel.PadLeft(Picker.TimeWidth), lines[4][^Picker.TimeWidth..]);
        Assert.NotEqual(longTitle.UpdatedLabel.Length, shortChat.UpdatedLabel.Length);
        var workspaceAt = lines[3].IndexOf("SpiralBench", StringComparison.Ordinal);
        Assert.Equal(workspaceAt, lines[4].IndexOf("SoggyMapMaker", StringComparison.Ordinal));
        var sourceAt = lines[3].IndexOf("Cu-IDE", StringComparison.Ordinal);
        Assert.Equal(sourceAt, lines[4].IndexOf("Cu-CLI", StringComparison.Ordinal));
        Assert.True(sourceAt > workspaceAt);

        var wideWorkspace = Row("Title", new string('W', 40), "wide", new DateTime(2026, 8, 15, 16, 42, 0));
        var wide = Picker.RenderFrame([wideWorkspace], 0, "workspace", "dark", width: width);
        var row = wide.Split('\n')[3];
        var beforeTime = Picker.TimeWidth + 2 + Picker.SourceWidth + 2;
        var cell = row[^(beforeTime + 24)..^beforeTime];
        Assert.Equal(new string('W', 21) + "...", cell);
        Assert.Contains("This workspace *", wide.Split('\n')[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Id_toggle_follows_selection_and_hides_for_an_empty_list()
    {
        var (_, frames) = Frames(["esc"]);
        Assert.DoesNotContain("id:", frames[0], StringComparison.Ordinal);
        Assert.Contains("i id", frames[0], StringComparison.Ordinal);

        (_, frames) = Frames(["i"]);
        Assert.DoesNotContain("id:", frames[0], StringComparison.Ordinal);
        Assert.Contains("id: here", frames[^1], StringComparison.Ordinal);

        (_, frames) = Frames(["i", "i"]);
        Assert.DoesNotContain("id:", frames[^1], StringComparison.Ordinal);

        (_, frames) = Frames(["i", "down"]);
        Assert.Contains("id: there", frames[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("id: here", frames[^1], StringComparison.Ordinal);

        var empty = new List<string>();
        Picker.Run([], "dark", ["i"], _launch, empty.Add, width: 80);
        Assert.DoesNotContain("id:", empty[^1], StringComparison.Ordinal);
        Assert.Contains("i id", empty[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Name_line_keeps_the_list_and_uses_a_block_cursor()
    {
        var (result, frames) = Frames(["down", "up", "enter"]);
        Assert.Null(result.ChatId);
        var name = "file: Fantasy Map Maker.html";
        Assert.Contains("> Fantasy Map Maker", frames[^1], StringComparison.Ordinal);
        Assert.Contains("theme: terminal-dark", frames[^1], StringComparison.Ordinal);
        foreach (var themeName in new[] { "terminal-light", "dark-web", "light-web" })
        {
            var (_, themed) = Frames(["enter"], theme: themeName);
            Assert.Contains($"theme: {themeName}", themed[^1], StringComparison.Ordinal);
        }
        Assert.Contains(name, frames[^1], StringComparison.Ordinal);
        Assert.Contains($";{name.Length + 1}H", frames[^1], StringComparison.Ordinal);
        Assert.Contains("\x1b[1 q", frames[^1], StringComparison.Ordinal);
        Assert.Contains("\x1b[?25h", frames[^1], StringComparison.Ordinal);
        Assert.Contains("\x1b[?25l", frames[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\x1b[1 q", frames[0], StringComparison.Ordinal);
        Assert.Contains("type to edit", frames[^1], StringComparison.Ordinal);

        (_, frames) = Frames(["enter", "down"]);
        Assert.Contains("> Fantasy Map Maker", frames[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("> Apply Opsx", frames[^1], StringComparison.Ordinal);

        (result, frames) = Frames(["enter", "esc"]);
        Assert.Null(result.ChatId);
        Assert.DoesNotContain("type to edit", frames[^1], StringComparison.Ordinal);
        Assert.Contains("\x1b[?25l", frames[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("\x1b[1 q", frames[^1], StringComparison.Ordinal);

        var longKeys = new List<string> { "enter" };
        longKeys.AddRange(Enumerable.Repeat("x", 90));
        (_, frames) = Frames(longKeys, width: 80);
        Assert.Contains(";80H", frames[^1], StringComparison.Ordinal);
        Assert.Contains("\x1b[1 q", frames[^1], StringComparison.Ordinal);
        Assert.Contains("> Fantasy Map Maker", frames[^1], StringComparison.Ordinal);

        (result, _) = Frames(["down", "up", "enter", "enter"]);
        Assert.Equal("here", result.ChatId);
        Assert.Equal("Fantasy Map Maker.html", result.FileName);
    }

    [Fact]
    public void Live_writer_paints_the_list_before_the_first_key()
    {
        var output = new StringWriter();
        var visible = false;
        IEnumerable<string> keys()
        {
            var shown = output.ToString();
            visible = shown.Contains("\x1b[H\x1b[2J", StringComparison.Ordinal)
                && shown.Contains("All chats", StringComparison.Ordinal)
                && shown.Contains("Fantasy Map Maker", StringComparison.Ordinal);
            yield return "esc";
        }

        Assert.Equal(
            0,
            Exporter.ExportWithPicker(
                _chats,
                "dark",
                keys(),
                _launch,
                _launch,
                false,
                _chatsRoot,
                output,
                paint: frame => Exporter.WritePickerFrame(output, frame)));
        Assert.True(visible);
    }

    [Fact]
    public void Leaving_the_picker_restores_the_screen_before_the_prompt()
    {
        var output = new StringWriter();
        var code = Picker.CallWithRestore(
            () =>
            {
                Picker.Run(
                    _chats,
                    "dark",
                    ["enter", "enter"],
                    _launch,
                    frame => Exporter.WritePickerFrame(output, frame));
                return 0;
            },
            () => Exporter.LeavePickerUi(output),
            () => output.Write("\x1b[?1049h"));
        output.Write(@"C:\export\Fantasy Map Maker.html" + "\n");

        Assert.Equal(0, code);
        var shown = output.ToString();
        var hint = shown.IndexOf("Esc returns", StringComparison.Ordinal);
        var left = shown.IndexOf("\x1b[?1049l", StringComparison.Ordinal);
        var path = shown.IndexOf(@"C:\export\", StringComparison.Ordinal);
        Assert.True(hint >= 0 && left > hint && path > left);
        Assert.Contains("\x1b[?25h", shown[left..], StringComparison.Ordinal);
        Assert.Contains("\x1b[0 q", shown[left..], StringComparison.Ordinal);
    }

    [Fact]
    public void Edited_name_is_written_and_esc_writes_nothing()
    {
        var edited = Path.Combine(_root, "edited");
        Directory.CreateDirectory(edited);
        var defaultName = "Fantasy Map Maker.html";
        var keys = new List<string> { "enter" };
        keys.AddRange(Enumerable.Repeat("backspace", defaultName.Length));
        keys.AddRange("notes".Select(character => character.ToString()));
        keys.Add("enter");
        Assert.Equal(0, Exporter.ExportWithPicker(_chats, "dark", keys, _launch, edited, false, _chatsRoot, new StringWriter()));
        Assert.True(File.Exists(Path.Combine(edited, "notes.html")));
        Assert.False(File.Exists(Path.Combine(edited, defaultName)));

        var cancelled = Path.Combine(_root, "cancelled");
        Directory.CreateDirectory(cancelled);
        var stdout = new StringWriter();
        Assert.Equal(0, Exporter.ExportWithPicker(_chats, "dark", ["enter", "esc", "esc"], _launch, cancelled, false, _chatsRoot, stdout));
        Assert.Empty(Directory.GetFileSystemEntries(cancelled));
        Assert.Contains("type to edit", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Interrupt_and_teardown_restore_the_cursor()
    {
        var restored = new List<string>();
        var code = Exporter.ExportWithPicker(
            _chats,
            "dark",
            Boom(),
            _launch,
            Path.Combine(_root, "unused"),
            false,
            _chatsRoot,
            new StringWriter(),
            () => restored.Add("setup"),
            () => restored.Add("teardown"));
        Assert.Equal(130, code);
        Assert.Equal(["setup", "teardown"], restored);
        Assert.False(Directory.Exists(Path.Combine(_root, "unused")));

        var cursor = new StringWriter();
        Picker.TeardownTerminal(cursor);
        Assert.Contains("\x1b[?25h", cursor.ToString(), StringComparison.Ordinal);
        Assert.Contains("\x1b[0 q", cursor.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Source_key_cycles_filters_and_types_on_the_name_line()
    {
        var ide = Row("River map", "Other", "ide-1", new DateTime(2026, 8, 15, 16, 40, 0), "ide");
        var cli = Row("Fantasy Map Maker", _launch, "cli-1", new DateTime(2026, 8, 15, 16, 42, 0));
        var copilot = Row("Lake map", "Other", "cp-1", new DateTime(2026, 8, 15, 16, 30, 0), "cp-cli");
        var copilotIde = Row("Shelf map", "Other", "cp-ide-1", new DateTime(2026, 8, 15, 16, 20, 0), "cp-ide");
        var frames = new List<string>();
        Picker.Run([ide, cli, copilot, copilotIde], "terminal-dark", ["s", "S", "s", "s", "s", "esc"], _launch, frames.Add, width: 80);
        Assert.Contains("s source", frames[0], StringComparison.Ordinal);
        Assert.Contains("i id", frames[0], StringComparison.Ordinal);
        Assert.Contains("Enter names the file", frames[0], StringComparison.Ordinal);
        Assert.Contains("All sources *", frames[0], StringComparison.Ordinal);
        Assert.Contains("Copilot-CLI", frames[0], StringComparison.Ordinal);
        Assert.Contains("Copilot-IDE", frames[0], StringComparison.Ordinal);
        Assert.Contains("Cursor-CLI *", frames[1], StringComparison.Ordinal);
        Assert.DoesNotContain("River map", frames[1], StringComparison.Ordinal);
        Assert.Contains("Cursor-IDE *", frames[2], StringComparison.Ordinal);
        Assert.Contains("River map", frames[2], StringComparison.Ordinal);
        Assert.DoesNotContain("Fantasy Map Maker", frames[2], StringComparison.Ordinal);
        Assert.Contains("Copilot-CLI *", frames[3], StringComparison.Ordinal);
        Assert.Contains("Lake map", frames[3], StringComparison.Ordinal);
        Assert.DoesNotContain("River map", frames[3], StringComparison.Ordinal);
        Assert.Contains("Copilot-IDE *", frames[4], StringComparison.Ordinal);
        Assert.Contains("Shelf map", frames[4], StringComparison.Ordinal);
        Assert.DoesNotContain("Lake map", frames[4], StringComparison.Ordinal);
        Assert.Contains("All sources *", frames[5], StringComparison.Ordinal);

        frames.Clear();
        Picker.Run([ide, cli], "terminal-dark", ["right", "s", "esc"], _launch, frames.Add, width: 80);
        Assert.Contains("This workspace *", frames[1], StringComparison.Ordinal);
        Assert.Contains("All sources *", frames[1], StringComparison.Ordinal);
        Assert.Contains("Cursor-CLI *", frames[2], StringComparison.Ordinal);
        Assert.Contains("This workspace *", frames[2], StringComparison.Ordinal);
        Assert.Contains("Fantasy Map Maker", frames[2], StringComparison.Ordinal);
        Assert.DoesNotContain("River map", frames[2], StringComparison.Ordinal);

        frames.Clear();
        Picker.Run([cli], "terminal-dark", ["enter", "s", "esc"], _launch, frames.Add, width: 80);
        Assert.Contains("file: Fantasy Map Maker.htmls", frames[^2], StringComparison.Ordinal);

        var emptyDir = Path.Combine(_root, "empty-out");
        var stdout = new StringWriter();
        var code = Exporter.ExportWithPicker(
            [cli],
            "terminal-dark",
            ["enter"],
            _launch,
            emptyDir,
            false,
            _chatsRoot,
            stdout,
            sources: new HashSet<string> { "ide" });
        Assert.Equal(0, code);
        Assert.Contains("(no chats)", stdout.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(emptyDir));
    }

    private static Chat Row(string title, string workspace, string id, DateTime moment, string source = "cli")
    {
        return new Chat(id, title, workspace, workspace, new DateTimeOffset(moment).ToUnixTimeMilliseconds(), true, ".", source);
    }

    private static IEnumerable<string> Boom()
    {
        throw new PickerInterruptException();
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
