namespace AiChatExport;

internal sealed record PickerResult(string? ChatId, string? FileName = null);

internal sealed class PickerInterruptException : Exception;

internal static class Picker
{
    public const int TimeWidth = 21;
    public const int WorkspaceCap = 24;
    public const int SourceWidth = 6;
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
        string extension = ".html",
        IReadOnlySet<string>? sources = null)
    {
        var scope = "all";
        var selected = 0;
        var showId = false;
        Chat? naming = null;
        var draft = "";
        var defaultName = "";
        HashSet<string>? allowed = sources is null ? null : new HashSet<string>(sources);

        List<Chat> Visible()
        {
            var rowsForScope = scope == "workspace" ? Catalog.FilterWorkspace(chats, launchDir) : chats.ToList();
            return allowed is null ? rowsForScope : rowsForScope.Where(chat => allowed.Contains(chat.Source)).ToList();
        }

        void Paint()
        {
            var shown = Visible();
            selected = shown.Count > 0 ? Math.Min(selected, shown.Count - 1) : 0;
            write(RenderFrame(shown, selected, scope, theme, rows, width, showId, allowed) + HideCursor);
        }

        void PaintName()
        {
            var shown = Visible();
            var frame = RenderFrame(shown, selected, scope, theme, rows, width, showId, allowed);
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
                    return new PickerResult(naming.Id, name);
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
            else if (key is "s" or "S")
            {
                allowed = CycleSources(allowed);
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
                return new PickerResult(null);
            }
            else if (key == "enter")
            {
                shown = Visible();
                if (shown.Count == 0)
                {
                    return new PickerResult(null);
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

        return new PickerResult(null);
    }

    public static string RenderFrame(
        IReadOnlyList<Chat> chats,
        int selected,
        string scope,
        string theme,
        int rows = 20,
        int? width = null,
        bool showId = false,
        IReadOnlySet<string>? sources = null)
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
        var lines = new List<string> { Header(scope, frameWidth), SourceLine(sources), rule };
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
                + Fit(chat.SourceLabel, SourceWidth)
                + "  "
                + FitRight(chat.UpdatedLabel, TimeWidth));
        }

        var count = chats.Count;
        var index = count > 0 ? selected + 1 : 0;
        lines.Add(rule);
        lines.Add($"{index}/{count}  up/down move  left/right scope  Enter names the file  Esc closes  i id  s source");
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
        var stem = Stem(chat.DisplayTitle);
        if (stem.Length == 0 || IsReservedStem(stem))
        {
            stem = chat.Id;
        }

        return $"{stem}{extension}";
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
        if (stem.Length == 0 || IsReservedStem(stem))
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

    private static readonly HashSet<string> ReservedDeviceNames = CreateReservedDeviceNames();

    public static bool IsReservedStem(string stem)
    {
        return ReservedDeviceNames.Contains(stem);
    }

    private static HashSet<string> CreateReservedDeviceNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON",
            "PRN",
            "AUX",
            "NUL",
        };
        for (var number = 1; number <= 9; number++)
        {
            names.Add("COM" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            names.Add("LPT" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return names;
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
        var title = frameWidth - 2 - 2 - workspace - 2 - SourceWidth - 2 - TimeWidth;
        if (title < 1)
        {
            workspace = Math.Max(0, frameWidth - 2 - 2 - 1 - 2 - SourceWidth - 2 - TimeWidth);
            title = Math.Max(0, frameWidth - 2 - 2 - workspace - 2 - SourceWidth - 2 - TimeWidth);
        }

        return (title, workspace);
    }

    private static HashSet<string>? CycleSources(IReadOnlySet<string>? allowed)
    {
        var ids = SessionSources.All.Select(item => item.Id).ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        if (allowed is null || ids.All(allowed.Contains))
        {
            return [ids[0]];
        }

        if (allowed.Count == 1)
        {
            var index = ids.IndexOf(allowed.Single());
            if (index >= 0)
            {
                return index + 1 < ids.Count ? [ids[index + 1]] : null;
            }
        }

        return null;
    }

    private static string SourceLine(IReadOnlySet<string>? sources)
    {
        var every = sources is null || SessionSources.All.All(item => sources.Contains(item.Id));
        var parts = new List<string> { every ? "All sources *" : "All sources" };
        foreach (var item in SessionSources.All)
        {
            var marked = !every && sources is not null && sources.Contains(item.Id);
            parts.Add(marked ? $"{item.FilterLabel} *" : item.FilterLabel);
        }

        return string.Join("    ", parts);
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
