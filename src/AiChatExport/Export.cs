using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiChatExport;

internal sealed class Activity : IDisposable
{
    private static readonly string[] Frames = ["...", "..", "."];
    private readonly TextWriter _stream;
    private readonly bool _isTerminal;
    private readonly TimeSpan _interval;
    private readonly ManualResetEventSlim _stop = new(false);
    private Thread? _thread;
    private bool _shown;
    private bool _disposed;

    public Activity(TextWriter stream, bool isTerminal, TimeSpan? interval = null)
    {
        _stream = stream;
        _isTerminal = isTerminal;
        _interval = interval ?? TimeSpan.FromMilliseconds(200);
    }

    public void Start()
    {
        if (!_isTerminal || _thread is not null)
        {
            return;
        }

        _shown = true;
        _stream.Write("\n" + Frames[0]);
        _stream.Flush();
        _thread = new Thread(Spin) { IsBackground = true };
        _thread.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_shown)
        {
            _stop.Set();
            _thread?.Join();
            _stream.Write("\r" + new string(' ', Frames[0].Length) + "\r");
            _stream.Flush();
            _shown = false;
        }

        _stop.Dispose();
    }

    private void Spin()
    {
        var index = 1;
        while (!_stop.Wait(_interval))
        {
            var frame = Frames[index % Frames.Length];
            index++;
            _stream.Write("\r" + frame.PadRight(Frames[0].Length));
            _stream.Flush();
        }
    }
}

internal static class Exporter
{
    public static string SafeStem(string? title, string chatId)
    {
        var raw = string.IsNullOrWhiteSpace(title) ? chatId : title.Trim();
        if (raw.Length == 0)
        {
            raw = chatId;
        }

        return Picker.Stem(raw) is { Length: > 0 } stem && !Picker.IsReservedStem(stem) ? stem : chatId;
    }

    public static int ExportSession(
        string chatsRoot,
        string chatId,
        string outDir,
        string theme = "terminal-dark",
        bool verbose = false,
        bool showTools = false,
        bool showDetail = false,
        bool writeMarkdown = false,
        string? fileName = null,
        string? outputPath = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        TimeSpan? activityInterval = null,
        bool? stderrIsTerminal = null,
        string? projectsRoot = null,
        IReadOnlySet<string>? sources = null,
        string? copilotRoot = null)
    {
        var output = stdout ?? Console.Out;
        var error = stderr ?? Console.Error;
        var showActivity = stderrIsTerminal ?? (ReferenceEquals(error, Console.Error) && !Console.IsErrorRedirected);
        if (outputPath is not null)
        {
            var namedError = NamedOutputError(outputPath);
            if (namedError is not null)
            {
                error.Write(namedError + "\n");
                return 1;
            }
        }

        var warnings = new List<string>();
        List<Chat> chats;
        using (var spin = new Activity(error, showActivity, activityInterval))
        {
            spin.Start();
            chats = Catalog.LoadSessions(chatsRoot, projectsRoot, warnings, copilotRoot);
            if (sources is not null)
            {
                chats = chats.Where(chat => sources.Contains(chat.Source)).ToList();
            }
        }

        foreach (var note in warnings)
        {
            error.Write(note + "\n");
        }

        string? failure = null;
        string? htmlPath = null;
        string? markdownPath = null;
        using (var spin = new Activity(error, showActivity, activityInterval))
        {
            spin.Start();
            var chat = chats.FirstOrDefault(item => item.Id == chatId);
            if (chat is null)
            {
                failure = $"Chat not found: {chatId}";
            }
            else if (!chat.HasStore)
            {
                failure = $"Chat has no transcript: {chatId}";
            }
            else
            {
                try
                {
                    Conversation conversation;
                    IReadOnlyList<string> prompts;
                    if (chat.Source == "cp-cli")
                    {
                        var loaded = CopilotTranscript.Load(chat.Path);
                        conversation = loaded.Conversation;
                        prompts = loaded.Prompts;
                        if (loaded.Warning is not null)
                        {
                            error.Write(loaded.Warning + "\n");
                        }
                    }
                    else if (chat.Source == "ide")
                    {
                        conversation = IdeTranscript.Load(chat.Path);
                        prompts = [];
                    }
                    else
                    {
                        conversation = Store.Load(Path.Combine(chat.Path, "store.db"));
                        prompts = Prompts(chat.Path);
                    }
                    if (conversation.Messages.Count == 0)
                    {
                        failure = $"Chat has no transcript: {chatId}";
                    }
                    else
                    {
                        var transcript = TranscriptBuilder.Build(
                            conversation.Messages,
                            prompts,
                            verbose,
                            showTools,
                            showDetail,
                            conversation.Skipped,
                            conversation.UnlinkedImages);
                        string target;
                        if (outputPath is not null)
                        {
                            target = outputPath;
                        }
                        else
                        {
                            var stemName = fileName ?? $"{SafeStem(chat.Title, chat.Id)}{(writeMarkdown ? ".md" : ".html")}";
                            Directory.CreateDirectory(outDir);
                            target = Path.Combine(outDir, stemName);
                        }

                        var parent = Path.GetDirectoryName(Path.GetFullPath(target));
                        if (!string.IsNullOrEmpty(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }

                        var document = writeMarkdown
                            ? MarkdownRenderer.Render(transcript, chat.DisplayTitle, chat.WorkspaceName, chat.UpdatedLabel)
                            : HtmlRenderer.Render(transcript, chat.DisplayTitle, chat.WorkspaceName, chat.UpdatedLabel, theme);
                        File.WriteAllText(target, document, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                        if (writeMarkdown)
                        {
                            markdownPath = target;
                        }
                        else
                        {
                            htmlPath = target;
                        }
                    }
                }
                catch (Exception exc) when (exc is IOException or InvalidDataException or KeyNotFoundException or JsonException or FormatException or Microsoft.Data.Sqlite.SqliteException)
                {
                    failure = $"Chat has no transcript: {chatId} ({exc.Message})";
                }
            }
        }

        if (failure is not null)
        {
            error.Write(failure + "\n");
            return 1;
        }

        var written = writeMarkdown ? markdownPath : htmlPath;
        output.Write(Path.GetFullPath(written!) + "\n");
        return 0;
    }

    public static int ExportWithPicker(
        IReadOnlyList<Chat> chats,
        string theme,
        IEnumerable<string> keys,
        string launchDir,
        string outDir,
        bool verbose,
        string chatsRoot,
        TextWriter stdout,
        Action? setup = null,
        Action? teardown = null,
        bool showTools = false,
        bool showDetail = false,
        bool writeMarkdown = false,
        Action<string>? paint = null,
        string? projectsRoot = null,
        IReadOnlySet<string>? sources = null,
        string? copilotRoot = null)
    {
        int Body()
        {
            try
            {
                var frames = new List<string>();
                var result = Picker.Run(
                    chats,
                    theme,
                    keys,
                    launchDir,
                    paint ?? frames.Add,
                    extension: writeMarkdown ? ".md" : ".html",
                    sources: sources);
                if (paint is null)
                {
                    stdout.Write(string.Join("", frames));
                }
                if (result.ChatId is null)
                {
                    return 0;
                }

                return ExportSession(
                    chatsRoot,
                    result.ChatId,
                    outDir,
                    theme,
                    verbose,
                    showTools,
                    showDetail,
                    writeMarkdown,
                    result.FileName,
                    stdout: stdout,
                    projectsRoot: projectsRoot,
                    sources: sources,
                    copilotRoot: copilotRoot);
            }
            catch (PickerInterruptException)
            {
                return 130;
            }
        }

        return Picker.CallWithRestore(Body, teardown ?? (() => { }), setup);
    }

    public static int Run(string[]? argv = null)
    {
        argv ??= Environment.GetCommandLineArgs().Skip(1).ToArray();
        string? chatId = null;
        var theme = "terminal-dark";
        var tools = false;
        var verbose = false;
        var detail = false;
        var markdown = false;
        string? outDir = null;
        string? name = null;
        string? chatsRoot = null;
        string? projectsRoot = null;
        string? copilotRoot = null;
        var sourceArgs = new List<string>();

        for (var index = 0; index < argv.Length; index++)
        {
            var arg = argv[index];
            string? Value()
            {
                var eq = arg.IndexOf('=');
                if (eq >= 0)
                {
                    return arg[(eq + 1)..];
                }

                if (index + 1 >= argv.Length)
                {
                    Console.Error.WriteLine($"argument missing: {arg}");
                    return null;
                }

                return argv[++index];
            }

            if (arg is "-h" or "--help")
            {
                Console.Out.Write(HelpText);
                return 0;
            }

            if (arg == "--tools")
            {
                tools = true;
            }
            else if (arg == "--verbose")
            {
                verbose = true;
            }
            else if (arg == "--detail")
            {
                detail = true;
            }
            else if (arg == "--markdown")
            {
                markdown = true;
            }
            else if (arg == "--id" || arg.StartsWith("--id=", StringComparison.Ordinal))
            {
                chatId = Value();
                if (chatId is null)
                {
                    return 2;
                }
            }
            else if (arg == "--theme" || arg.StartsWith("--theme=", StringComparison.Ordinal))
            {
                theme = Value() ?? "";
                if (!HtmlRenderer.ThemeNames.Contains(theme))
                {
                    Console.Error.WriteLine("invalid theme");
                    return 2;
                }
            }
            else if (arg == "--out" || arg.StartsWith("--out=", StringComparison.Ordinal))
            {
                outDir = Value();
                if (outDir is null)
                {
                    return 2;
                }
            }
            else if (arg == "--name" || arg.StartsWith("--name=", StringComparison.Ordinal))
            {
                name = Value();
                if (name is null)
                {
                    return 2;
                }
            }
            else if (arg == "--chats-root" || arg.StartsWith("--chats-root=", StringComparison.Ordinal))
            {
                chatsRoot = Value();
                if (chatsRoot is null)
                {
                    return 2;
                }
            }
            else if (arg == "--projects-root" || arg.StartsWith("--projects-root=", StringComparison.Ordinal))
            {
                projectsRoot = Value();
                if (projectsRoot is null)
                {
                    return 2;
                }
            }
            else if (arg == "--copilot-root" || arg.StartsWith("--copilot-root=", StringComparison.Ordinal))
            {
                copilotRoot = Value();
                if (copilotRoot is null)
                {
                    return 2;
                }
            }
            else if (arg == "--source" || arg.StartsWith("--source=", StringComparison.Ordinal))
            {
                var source = Value();
                if (source is null || !SessionSources.KnownArgument(source))
                {
                    Console.Error.WriteLine("invalid source");
                    return 2;
                }

                sourceArgs.Add(source);
            }
            else
            {
                Console.Error.WriteLine($"unrecognized argument: {arg}");
                return 2;
            }
        }

        if (name is not null && string.IsNullOrEmpty(chatId))
        {
            Console.Error.WriteLine("`--name` requires `--id`.");
            return 1;
        }

        if (name is not null && outDir is not null)
        {
            Console.Error.WriteLine("Pass either --name or --out.");
            return 1;
        }

        chatsRoot ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "chats");
        projectsRoot ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "projects");
        copilotRoot ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");
        outDir ??= Directory.GetCurrentDirectory();
        var sources = SessionSources.Allowed(sourceArgs);
        if (!string.IsNullOrEmpty(chatId))
        {
            return ExportSession(
                chatsRoot,
                chatId,
                outDir,
                theme,
                verbose,
                tools,
                detail,
                markdown,
                outputPath: name,
                projectsRoot: projectsRoot,
                sources: sources,
                copilotRoot: copilotRoot);
        }

        var warnings = new List<string>();
        var chats = Catalog.LoadSessions(chatsRoot, projectsRoot, warnings, copilotRoot);
        foreach (var note in warnings)
        {
            Console.Error.WriteLine(note);
        }

        Console.TreatControlCAsInput = true;
        EnableVirtualTerminal();
        try
        {
            PickerResult? picked = null;
            var code = Picker.CallWithRestore(
                () =>
                {
                    try
                    {
                        picked = Picker.Run(
                            chats,
                            theme,
                            ReadKeys(),
                            Directory.GetCurrentDirectory(),
                            frame => WritePickerFrame(Console.Out, frame),
                            extension: markdown ? ".md" : ".html",
                            sources: sources);
                        return 0;
                    }
                    catch (PickerInterruptException)
                    {
                        return 130;
                    }
                },
                () => LeavePickerUi(Console.Out),
                EnterPickerUi);
            if (code != 0 || picked is null || picked.ChatId is null)
            {
                return code;
            }

            return ExportSession(
                chatsRoot,
                picked.ChatId,
                outDir,
                theme,
                verbose,
                tools,
                detail,
                markdown,
                picked.FileName,
                projectsRoot: projectsRoot,
                sources: sources,
                copilotRoot: copilotRoot);
        }
        finally
        {
            Console.TreatControlCAsInput = false;
        }
    }

    public const string HelpText =
        """
        usage: ai-chat-export [--id ID] [--theme {terminal-dark,terminal-light,dark-web,light-web}] [--tools] [--verbose] [--detail] [--markdown] [--out DIR] [--name PATH] [--chats-root DIR] [--projects-root DIR] [--copilot-root DIR] [--source {cursor-cli,cursor-ide,copilot-cli}]

        Export a Cursor agent session or a Copilot CLI session to HTML. Pass --markdown to write Markdown instead. The picker lists Cursor agent chats, Cursor IDE transcripts, and Copilot CLI sessions. In the picker, Enter asks for a file name. The title file name is the default. s switches the source filter.

        options:
          --id ID             Chat id to export. Omit to open the picker.
          --theme {terminal-dark,terminal-light,dark-web,light-web}
                              HTML theme written into the file (default: terminal-dark).
          --tools             Include one caption per tool call. Omit raw tool output.
          --verbose           Include system text, injected context, reasoning, tool captions, and raw tool output.
          --detail            Include the conversation summary and Cursor command blocks after the user query.
          --markdown          Write a Markdown file instead of the HTML page.
          --out DIR           Directory for the export files.
          --name PATH         File path to write when --id is set, including the directory and extension. Replaces --out. Requires --id.
          --chats-root DIR    Agent chat catalog. Defaults to ~/.cursor/chats.
          --projects-root DIR Cursor IDE projects root. Defaults to ~/.cursor/projects.
          --copilot-root DIR  Copilot CLI root. Defaults to ~/.copilot.
          --source {cursor-cli,cursor-ide,copilot-cli}
                              Limit the list to one store. Repeat to include more. The picker column shows Cu-CLI, Cu-IDE, or Cp-CLI.
          -h, --help          show this help message and exit

        """;

    public static void WritePickerFrame(TextWriter output, string frame)
    {
        output.Write("\x1b[H\x1b[2J");
        output.Write(frame);
        output.Flush();
    }

    public static void EnterPickerUi()
    {
        Console.Out.Write("\x1b[?1049h");
        Console.Out.Flush();
    }

    public static void LeavePickerUi(TextWriter output)
    {
        output.Write("\x1b[?1049l" + Picker.ShowCursor + Picker.DefaultCursor);
        output.Flush();
    }

    private static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = VirtualTerminal.GetStdHandle(-11);
        if (handle == 0 || handle == -1 || !VirtualTerminal.GetConsoleMode(handle, out var mode))
        {
            return;
        }

        VirtualTerminal.SetConsoleMode(handle, mode | 0x0004);
    }

    private static IEnumerable<string> ReadKeys()
    {
        while (true)
        {
            var key = Picker.ReadKey();
            if (key.Length > 0)
            {
                yield return key;
            }
        }
    }

    private static string? NamedOutputError(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name) || name is "." or "..")
        {
            return $"Output path has no file name: {path}";
        }

        if (Directory.Exists(path))
        {
            return $"Output path is a directory: {path}";
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        if (Picker.IsReservedStem(stem))
        {
            return $"Output path is a reserved device name: {path}";
        }

        return null;
    }

    private static List<string> Prompts(string chatDir)
    {
        var path = Path.Combine(chatDir, "prompt_history.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var payload = JsonNode.Parse(File.ReadAllText(path));
            if (payload is not JsonArray array)
            {
                return [];
            }

            return array
                .Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : null)
                .Where(item => item is not null)
                .Cast<string>()
                .ToList();
        }
        catch (Exception exc) when (exc is IOException or JsonException)
        {
            return [];
        }
    }
}
