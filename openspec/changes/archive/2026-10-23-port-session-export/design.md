# Design

## Context

This repo has OpenSpec scaffolding and no application code. The behavior to match is the Python CLI in `C:\src\cursor_pretty_export`: catalog, `store.db` reader, transcript filter, HTML and Markdown renderers, picker, and the `--id` export path. See `proposal.md` for why, and `specs/session-export/spec.md` for the contract. The Python durable spec lags that program on the command name and on `--markdown`; this design follows the delta spec.

## Goals / Non-Goals

**Goals:**

- A `net10.0` console app whose help text names `ai-chat-export`, buildable with `dotnet build` and runnable with `dotnet run`.
- The same flags, files, picker keys, and HTML as the Python CLI.
- Seams the Python tests already isolate, so those cases can be ported without a live Cursor install.
- One package dependency.

**Non-Goals:**

- A global `dotnet tool`, a single-file publish, or a RID-specific installer.
- Editing the Python repo.
- A generated protobuf model, a console widget library, a Markdown library, or an XML parser.

## Decisions

### One console project and one test project

`AiChatExport.sln` at the repo root. `src/AiChatExport` is an executable targeting `net10.0` with assembly name `ai-chat-export`, so the built file is `ai-chat-export` (`ai-chat-export.exe` on Windows). `tests/AiChatExport.Tests` is xUnit and references that project.

Folders inside the console project follow the Python modules: catalog, store, transcript, HTML render, Markdown render, picker, and the command entry. They stay in one assembly. A class library plus a thin host would only add a project boundary the Python package does not have.

### Hand-rolled arguments

Flags match the Python CLI: `--id`, `--theme`, `--tools`, `--verbose`, `--detail`, `--markdown`, `--out`, `--name`, `--chats-root`. Help's usage line is `ai-chat-export`. `--name` without `--id`, and `--name` together with `--out`, fail before any read.

`System.CommandLine` would be a second dependency for nine flags. A small parser keeps `Microsoft.Data.Sqlite` as the only package.

### Port the blob reader as a heuristic

`store.db` is SQLite. Open it read-only (`Mode=ReadOnly`). The `meta` value is hex-encoded JSON with `latestRootBlobId`. Blobs are either a concatenation of 32-byte ids, a protobuf-shaped message with no `.proto`, or a JSON object whose `role` is a string. Port `store.py`: try aligned ids, try the hand-rolled wire walk, and fall back to scanning. Return "not this shape" by failing the walk, the same way the Python functions return `None`. Unrecognized blobs and PNGs that are not part of a visible turn are named in the footer.

`Google.Protobuf` and `protobuf-net` need a schema. A generated parser would reject blobs this walker accepts by falling through.

### Picker stays a function over keys and frames

`run_picker` takes the catalog, the theme, a key source, the launch directory, and a frame writer. Tests feed keys and capture frames. Terminal setup sits at the program edge only.

Keys come from `Console.ReadKey(intercept: true)` on Windows and Unix. The frame is redrawn with ANSI (`\x1b[H\x1b[2J`). The chat list appends hide-cursor (`\x1b[?25l`). The name line places a blinking block (`\x1b[1 q`) in the cell after the visible draft and shows the cursor (`\x1b[?25h`). Leaving the picker writes show-cursor and the default shape (`\x1b[?25h\x1b[0 q`), including Esc and Ctrl+C. Column widths stay the Python constants: time width 21, workspace cap 24.

Spectre.Console can draw a menu. It does not give the aligned columns, the name line that keeps the list on screen, or the block cursor in the next cell.

### Renderers are string builders

Port `render_html.py` and `render_md.py`, including the dark and light palettes (`#161616` / `#f7f7f4` and the panel colors beside them). Assistant text becomes paragraphs, lists, fenced code, and inline code with the same rules as `_blocks`. An image on a visible turn is a `data:` URL in the file. No script and no external address.

A Markdown library would change escaping and list markup. The Python renderer is the oracle.

Query, summary, and command-block extraction stays string matching on `user_query`, `conversation_summary`, and `cursor_commands`, as in `transcript.py`. Those fragments are not an XML document.

### English catalog times

Format catalog and turn times with the invariant culture so the month is `Aug`, `Sep`, or `Oct` on any machine. The clock is the local timezone. Navigator times are `YY-MM-DD hh:mm` in 24-hour form. An unparsed turn time is kept as stored.

### Tests construct synthetic stores

Port `tests/support.py` (`write_chat`, `write_db`, `write_protobuf_db`) and the cases in the six Python test modules. The export function accepts stdout, stderr, and the activity interval, as `export_session` does, so the `...` / `..` / `.` cycle can be asserted without a terminal. `isatty` still gates the cycle: a non-terminal stderr writes nothing.

### Activity cycle

A background loop writes `\r...`, `\r.. `, and `\r.  ` on a terminal stderr from the start of quiet work until the result. Clear that line before a path, an error, or a catalog warning. A warning is a full line, then the cycle resumes. Two stretches exist in the Python tool (catalog load, then the export); keep both.

## Risks / Trade-offs

- [A real Cursor store uses a blob shape the synthetic fixtures never build] → Port the walker and the footer behavior from `store.py` rather than re-deriving the format. Skipped blobs stay visible in the footer.
- [Legacy console hosts ignore the block-cursor sequence] → Write the same ANSI the Python tool writes. Windows Terminal and the VS Code terminal honor it. The picker tests assert the sequences in the frame, not a pixel.
- [The activity loop races a path or an error] → Stop and join the loop, then clear the line, then write the message. The Python context manager is the order.
- [`Microsoft.Data.Sqlite` brings a native SQLite binary] → A framework-dependent build uses the asset for the current machine. Single-file publish is out of scope, so RID packaging is not part of this change.
- [Invariant English months differ from a Python process whose locale is not English] → The spec scenarios require English abbreviations. That is the intended output.

## Migration Plan

Nothing is migrated. The Python CLI stays where it is and remains the reference. Rollback is deleting the solution, the two projects, and the README added here. No chat store is written by either tool.

## Open Questions

None. Entry point, target framework, and the Markdown-only export are settled in the proposal and the delta spec.
