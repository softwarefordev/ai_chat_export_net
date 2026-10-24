namespace AiChatExport;

public sealed record PickerResult(string? ChatId, bool DeleteRequested = false, string? FileName = null);

public sealed class PickerInterruptException : Exception;

public static class Picker
{
    public const int TimeWidth = 21;
    public const int WorkspaceCap = 24;
    public const string HideCursor = "\x1b[?25l";
    public const string ShowCursor = "\x1b[?25h";
    public const string BlockCursor = "\x1b[1 q";
    public const string DefaultCursor = "\x1b[0 q";

    public static PickerResult Run(
        IReadOnlyList<Chat> chats,
        string theme,
        IEnumerable<string> keys,
        string launchDir,
        Action<string> write,
        int rows = 20,
        int? width = null,
        string extension = ".html")
    {
        var scope = "all";
        var selected = 0;
        var showId = false;
        Chat? naming = null;
        var draft = "";
        var defaultName = "";

        List<Chat> Visible()
        {
            return scope == "workspace" ? Catalog.FilterWorkspace(chats, launchDir) : chats.ToList();
        }

        void Paint()
        {
            var shown = Visible();
            selected = shown.Count > 0 ? Math.Min(selected, shown.Count - 1) : 0;
            write(RenderFrame(shown, selected, scope, theme, rows, width, showId) + HideCursor);
        }

        void PaintName()
        {
            var shown = Visible();
            var frame = RenderFrame(shown, selected, scope, theme, rows, width, showId);
            write(AppendNameLine(frame, draft, FrameWidth(width)));
        }

        Paint();
        foreach (var key in keys)
        {
            if (naming is not null)
            {
                if (key == "enter")
                {
                    var name = ConfirmFileName(draft, defaultName, extension);
                    return new PickerResult(naming.Id, false, name);
                }

                if (key == "esc")
                {
                    naming = null;
                    Paint();
                    continue;
                }

                if (key == "backspace")
                {
                    if (draft.Length > 0)
                    {
                        draft = draft[..^1];
                    }
                }
                else if (key.Length == 1 && !char.IsControl(key[0]))
                {
                    draft += key;
                }

                PaintName();
                continue;
            }

            var shown = Visible();
            if (key == "up" && shown.Count > 0)
            {
                selected = Math.Max(0, selected - 1);
            }
            else if (key == "down" && shown.Count > 0)
            {
                selected = Math.Min(shown.Count - 1, selected + 1);
            }
            else if (key is "left" or "right")
            {
                scope = scope == "all" ? "workspace" : "all";
                selected = 0;
            }
            else if (key == "backspace")
            {
            }
            else if (key is "i" or "I")
            {
                showId = !showId;
            }
            else if (key == "esc")
            {
                Paint();
                return new PickerResult(null, false);
            }
            else if (key == "enter")
            {
                shown = Visible();
                if (shown.Count == 0)
                {
                    return new PickerResult(null, false);
                }

                naming = shown[selected];
                defaultName = DefaultFileName(naming, extension);
                draft = defaultName;
                PaintName();
                continue;
            }
            else
            {
                continue;
            }

            Paint();
        }

        return new PickerResult(null, false);
    }

    public static string RenderFrame(
        IReadOnlyList<Chat> chats,
        int selected,
        string scope,
        string theme,
        int rows = 20,
        int? width = null,
        bool showId = false)
    {
        var frameWidth = FrameWidth(width);
        var start = 0;
        if (chats.Count > rows)
        {
            start = Math.Min(Math.Max(0, selected - rows / 2), chats.Count - rows);
        }

        var window = chats.Skip(start).Take(rows).ToList();
        var (titleWidth, workspaceWidth) = ColumnWidths(frameWidth, window.Select(chat => chat.WorkspaceName).ToList());
        var rule = new string('-', frameWidth);
        var lines = new List<string> { Header(scope, frameWidth), rule };
        if (window.Count == 0)
        {
            lines.Add("(no chats)");
        }

        for (var offset = 0; offset < window.Count; offset++)
        {
            var chat = window[offset];
            var marker = start + offset == selected ? "> " : "  ";
            lines.Add(
                marker
                + Fit(chat.DisplayTitle, titleWidth)
                + "  "
                + Fit(chat.WorkspaceName, workspaceWidth)
                + "  "
                + FitRight(chat.UpdatedLabel, TimeWidth));
        }

        var count = chats.Count;
        var index = count > 0 ? selected + 1 : 0;
        lines.Add(rule);
        lines.Add($"{index}/{count}  up/down move  left/right scope  Enter names the file  Esc closes  i id");
        lines.Add($"theme: {theme}");
        if (showId && chats.Count > 0 && selected >= 0 && selected < chats.Count)
        {
            lines.Add($"id: {chats[selected].Id}");
        }

        return string.Join("\n", lines) + "\n";
    }

    public static string AppendNameLine(string frame, string draft, int frameWidth)
    {
        var body = frame.EndsWith('\n') ? frame[..^1] : frame;
        var line = $"file: {draft}";
        var room = Math.Max(frameWidth - 1, 0);
        if (line.Length > room)
        {
            line = room == 0 ? "" : line[^room..];
        }

        var text = body + $"\n{line}\ntype to edit   Enter writes   Esc returns\n";
        var row = body.Split('\n').Length + 1;
        var column = line.Length + 1;
        return $"{text}\x1b[{row};{column}H{BlockCursor}{ShowCursor}";
    }

    public static string DefaultFileName(Chat chat, string extension)
    {
        return $"{Stem(chat.DisplayTitle)}{extension}";
    }

    public static string ConfirmFileName(string draft, string fallback, string extension)
    {
        var text = draft.Trim();
        if (text.Length == 0)
        {
            return fallback;
        }

        var suffix = "";
        foreach (var candidate in new[] { ".html", ".md" })
        {
            if (text.EndsWith(candidate, StringComparison.OrdinalIgnoreCase))
            {
                suffix = text[^candidate.Length..];
                text = text[..^candidate.Length];
                break;
            }
        }

        var stem = Stem(text);
        if (stem.Length == 0)
        {
            return fallback;
        }

        return $"{stem}{(suffix.Length > 0 ? suffix : extension)}";
    }

    public static string Stem(string raw)
    {
        var cleaned = new List<char>(raw.Length);
        foreach (var character in raw)
        {
            cleaned.Add(character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' || character < 32
                ? ' '
                : character);
        }

        var stem = string.Join(" ", new string(cleaned.ToArray()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        stem = stem.TrimEnd(' ', '.');
        return stem.Length <= 80 ? stem : stem[..80];
    }

    public static int FrameWidth(int? width)
    {
        if (width is null)
        {
            try
            {
                width = Console.WindowWidth;
            }
            catch (IOException)
            {
                width = 80;
            }
        }

        return Math.Max(width.Value, 1);
    }

    public static void TeardownTerminal(TextWriter? output = null)
    {
        var writer = output ?? Console.Out;
        writer.Write(ShowCursor + DefaultCursor);
        writer.Flush();
    }

    public static string ReadKey()
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            throw new PickerInterruptException();
        }

        return key.Key switch
        {
            ConsoleKey.UpArrow => "up",
            ConsoleKey.DownArrow => "down",
            ConsoleKey.LeftArrow => "left",
            ConsoleKey.RightArrow => "right",
            ConsoleKey.Enter => "enter",
            ConsoleKey.Escape => "esc",
            ConsoleKey.Backspace => "backspace",
            _ when key.KeyChar is 'i' or 'I' => "i",
            _ when key.KeyChar != '\0' && !char.IsControl(key.KeyChar) => key.KeyChar.ToString(),
            _ => "",
        };
    }

    public static int CallWithRestore(Func<int> body, Action teardown, Action? setup = null)
    {
        setup?.Invoke();
        try
        {
            return body();
        }
        finally
        {
            teardown();
        }
    }

    private static (int Title, int Workspace) ColumnWidths(int frameWidth, IReadOnlyList<string> workspaceNames)
    {
        var longest = workspaceNames.Count == 0 ? 0 : workspaceNames.Max(name => name.Length);
        var workspace = Math.Min(WorkspaceCap, longest);
        var title = frameWidth - 2 - 2 - workspace - 2 - TimeWidth;
        if (title < 1)
        {
            workspace = Math.Max(0, frameWidth - 2 - 2 - 1 - 2 - TimeWidth);
            title = Math.Max(0, frameWidth - 2 - 2 - workspace - 2 - TimeWidth);
        }

        return (title, workspace);
    }

    private static string Header(string scope, int frameWidth)
    {
        var left = scope == "all" ? "All chats *    This workspace" : "All chats    This workspace *";
        var right = "Last updated".PadLeft(TimeWidth);
        if (frameWidth <= TimeWidth)
        {
            return right[^frameWidth..];
        }

        var room = frameWidth - TimeWidth;
        if (left.Length > room)
        {
            left = left[..room];
        }

        return left.PadRight(room) + right;
    }

    private static string Fit(string text, int width)
    {
        if (width <= 0)
        {
            return "";
        }

        if (text.Length <= width)
        {
            return text.PadRight(width);
        }

        if (width <= 3)
        {
            return text[..width];
        }

        return text[..(width - 3)] + "...";
    }

    private static string FitRight(string text, int width)
    {
        if (text.Length <= width)
        {
            return text.PadLeft(width);
        }

        if (width <= 3)
        {
            return text[..width];
        }

        return text[..(width - 3)] + "...";
    }
}
