# Design

## Context

See proposal.md for why. `Catalog.Load` walks `~/.cursor/chats` and returns a `Chat` with no source. `Picker.RenderFrame` draws three columns, and `ColumnWidths` gives the title whatever remains after the workspace and the 21-character time column. The first data row in the column test is `lines[2]`. `Exporter.ExportSession` always opens `store.db` beside the chat directory and passes those messages to `TranscriptBuilder`. An IDE session is a different file:

```
<projects-root>/<project-key>/
    .workspace-trusted                         workspacePath
    agent-transcripts/<id>/<id>.jsonl
```

A JSONL line is a `user` or `assistant` object whose `message.content` is a list of `text` and `tool_use` parts, or a `turn_ended` object. User text still wraps the query in `user_query` and the time in `timestamp`. The file has no tool results and no reasoning parts.

## Goals / Non-Goals

**Goals:**

- Merge both catalogs into one newest-first list and render a six-character source column.
- Adapt a JSONL file into the message list `TranscriptBuilder` already accepts.
- Apply the source filter and the workspace scope together.

**Non-Goals:**

- Reading `compaction/` or any file that is not `<id>/<id>.jsonl`.
- Putting the source cell on the HTML page or in the Markdown file.
- Changing themes, the navigator, or the turn filter.
- Reopening the image allowlist or the reserved-name rules already in this solution.

## Decisions

### One `Chat` with a source

Add `Source` to `Chat`, `"cli"` or `"ide"`, defaulting to `"cli"` so existing constructors stay valid. The picker prints `Cu-CLI` or `Cu-IDE`. `Path` stays the directory that holds the conversation: the chat directory, or the `<id>` directory that contains `<id>.jsonl`.

Alternative: a second row type. The picker, name line, and export already take a `Chat`, so a field keeps one list.

### One source registry

A small table, in order:

| stored id | `--source` | filter label | column |
| --- | --- | --- | --- |
| `cli` | `cursor-cli` | `Cursor-CLI` | `Cu-CLI` |
| `ide` | `cursor-ide` | `Cursor-IDE` | `Cu-IDE` |

The cycle, the flag values, and the filter line read the table. A later store adds one row.

Alternative: store `cursor-cli` on `Chat`. The export branch compares `"cli"` and `"ide"`. The public names can change without rewriting those ids.

### Merge, then drop shared ids

Load the chat catalog as it works today. Load IDE rows from `--projects-root`, default `~/.cursor/projects`. Drop an IDE row whose id is already in the chat catalog, then sort by updated time and id descending. A missing projects root yields no IDE rows and no error.

Alternative: list both copies. The chat store is the fuller transcript, and the spec says the shared id appears once as `Cu-CLI`.

### Filter state is the allowed set

`null` means every source. A set means those stored ids. The workspace filter runs first, then the source filter. Left and right change only the workspace scope. `s` cycles All sources, then `cli`, then `ide`, then back to All sources, and resets the selection to the first visible row. `S` does the same on the list. On the file-name line both characters stay typed input.

Repeating both `--source` values is the full set, so the line marks All sources and the first `s` selects Cursor-CLI. An unknown value fails in `Exporter.Run` before the catalog is read. Export and the picker both see the filtered catalog, so an id of another source is the existing `Chat not found` path.

### Title from the first query, time from mtime

Scan the JSONL only until the first user query. The title is that query's first line, or the id when the line is empty. The row time is the file's last-write time in Unix milliseconds, formatted with `Catalog.FormatTimestamp`. The export still reads the whole file.

Alternative: use the last `timestamp` tag as the list time. That reads every line of a large transcript during the catalog scan.

### Workspace from `.workspace-trusted`

Read `workspacePath` from `<project-key>/.workspace-trusted`. The column shows that path's last segment, and This workspace compares the full path the way it already compares a chat's `Cwd`. A missing or unreadable path leaves `Cwd` empty and uses the project folder name. Empty `Cwd` already fails the workspace comparison.

### Adapt lines, then call the existing builder

Each `user` or `assistant` line becomes one message. `text` parts stay text. A `tool_use` part becomes a `tool-call` with `toolName` from `name` and `args` from `input`, so the existing captions apply. The prompts list is the inner `user_query` strings. `turn_ended` is skipped and is not a footer line. Any other line, including a line that is not JSON, is skipped as `line N` (1-based) with the existing footer text for unrecognized data. An `ide` row reads `<id>.jsonl`. A `cli` row still opens `store.db`.

Alternative: teach `TranscriptBuilder` about JSONL. The builder stays store-agnostic, and the adapter is the only new parse.

### Source column in the width math

`SourceWidth` is 6. Column order is title, workspace, source, time, with two spaces between columns. The title width loses those six characters and one gap. The header still ends with `Last updated` and has no source heading. `render_frame` prints the source line after the scope header and before the first rule, so the first data row moves from `lines[2]` to `lines[3]`.

## Risks / Trade-offs

- [The JSONL layout is undocumented and can change] → Read only the path above, warn with the id, and keep the fixture shape in tests.
- [A shared id prefers the chat even when the JSONL is newer] → Specified. The chat store carries tool results the JSONL does not.
- [`--verbose` on an IDE-only session has no raw tool output] → Do not invent any. Captions still appear when `--tools` or `--verbose` is set.
- [A large transcript is opened twice, once for the title and once for export] → The title scan stops at the first user query.
- [Picker tests assume the workspace cell sits against the time column] → Update that slice in the same change. The `Last updated` header assertion stays valid.
- [The source line shifts every frame's row numbers] → Update the column test's row indexes. The bottom rule for a two-row frame moves with the extra line.

## Migration Plan

No stored data moves. Revert the change to restore a chats-only list.
