# Design

## Context

See proposal.md for why. `SessionSources.All` already drives the column, the filter line, `--source`, and the `s` cycle. `Catalog.LoadSessions` merges agent chats with IDE rows, drops an IDE id that is already a chat id, and sorts newest first. A missing projects root adds no IDE rows. `ExportSession` opens `store.db` for `cli` and `<id>.jsonl` for `ide`. `TranscriptBuilder` already turns user text, assistant text, `tool-call` captions, reasoning parts, system messages, and verbose `tool` results into the page. `Caption` knows `Read`, `Glob`, and `Shell`. `Chat.UpdatedLabel` always formats `UpdatedAtMs`.

## Goals / Non-Goals

**Goals:**

- One new registry row drives the Copilot column, filter label, `--source` value, and the next `s` step.
- Read the Copilot index read-only and export it through `TranscriptBuilder`.

**Non-Goals:**

- VS Code Copilot Chat.
- Renaming stored ids `cli` and `ide`.
- Showing the source cell on the HTML or Markdown page.
- Decrypting `encryptedContent` or reading `reasoningOpaque`.
- The GitHub Copilot workflow files already in this repo.

## Decisions

### Registry row

Append one row after the IDE row in `SessionSources.All`:

| stored id | `--source` | filter label | column |
| --- | --- | --- | --- |
| `cp-cli` | `copilot-cli` | `Copilot-CLI` | `Cp-CLI` |

`Cp-CLI` is six characters, so `Picker.SourceWidth` stays 6. `CycleSources` and `SourceLine` already walk `SessionSources.All`, so the cycle becomes All sources, Cursor-CLI, Cursor-IDE, Copilot-CLI, All sources. `Allowed` returns null only when every registry id is chosen, so `--source cursor-cli --source cursor-ide` stays a subset and the next `s` returns to All sources.

Alternative: stored id `copilot`. A later VS Code row would then need a name that does not sound like the whole product. `cp-cli` leaves that name free.

### Catalog from the database, detail from the event log

Add a Copilot reader. `LoadSessions` gains a copilot root. Null skips Copilot, matching a missing projects root. `Run` always passes the resolved root, default `~/.copilot`, overridable with `--copilot-root`. Direct `ExportSession` calls keep the root null unless the caller passes one, so existing tests do not open the user's store. Every `Run` path that reaches the catalog passes `--copilot-root` to a directory that is not `~/.copilot`.

Open `<copilot-root>/session-store.db` with `SqliteOpenMode.ReadOnly`, then `SqliteConnection.ClearPool`, the same pattern the chat store uses so a test can delete the file. List `sessions` (`id`, `cwd`, `summary`, `updated_at`). Ignore `session-state` directories that have no session row. Do not drop a Copilot id that matches a Cursor id. The IDE-versus-chat dedupe stays as it is. Append Copilot rows before the newest-first sort.

Title is `summary` when it has text, otherwise the first line of the first turn's `user_message`, otherwise the id. `updated_at` is parsed as UTC (`YYYY-MM-DDTHH:MM:SS.sssZ`) into `UpdatedAtMs` and shown with `FormatTimestamp`. An unparsed value is whitespace-collapsed and stored as an optional label on `Chat`. `UpdatedLabel` returns that label when it is set, and `UpdatedAtMs` is 0 so the row sorts last. The workspace column is the last segment of `cwd`. An empty `cwd` leaves the column empty, and `FilterWorkspace` already omits an empty path. A missing root adds no rows. An unreadable database warns `Unreadable Copilot store: {error}` and leaves the other sessions in the list.

Alternative: title and transcript only from `events.jsonl`. The database already has the summary, cwd, and updated time, and the running Python exporter uses it for the list.

### Transcript adapter

`ExportSession` loads a `cp-cli` chat from `session-state/<id>/events.jsonl` when that file can be read. Walk it in order and emit the message shape `TranscriptBuilder` already accepts:

- `user.message` content becomes a user text part. Plain text is the query. Ignore `transformedContent`.
- A non-empty `assistant.message` content becomes assistant text. Empty assistant events add no text, so that turn has no Agent heading.
- `reasoningText` becomes a `reasoning` part on that assistant message. `encryptedContent` and `reasoningOpaque` are dropped.
- `system.message` becomes a system message, which the transcript shows only for `--verbose`.
- `tool.execution_start` becomes a `tool-call` mapped onto captions: `view` plus `path` is `Read`; `glob` plus `pattern` is `Glob` with `glob_pattern`; `powershell` plus `description` is `Shell` and the command argument is not copied. `grep` plus `pattern` and `create` plus `path` need two caption arms, `Grep` and `Create`, beside the existing `Read`, `Glob`, and `Shell` arms. A missing description, path, or pattern uses the fallback the current caption uses.
- `tool.execution_complete` `result` becomes a following `tool` message with a `tool-result` part. `TranscriptBuilder` shows that only for `--verbose`. `--tools` therefore captions the call and omits the result.

`checkpoints/index.md` is not read. Other event types are ignored and are not footer lines.

When the event log is missing, build the same user and assistant text from `turns` ordered by `turn_index`. `--tools` then has no captions. When the log exists but cannot be read, warn `Unreadable Copilot events {id}: {error}` and use that turn fallback. A session with no user text and no assistant text uses the existing `Chat has no transcript: {id}` path and writes no file.

Alternative: always export the `turns` columns and attach tools beside them. The event log is what places each tool inside the stretch.

### Tests stay off the real store

Copilot tests build a temporary root with a small database, an event log, and a checkpoint index. Fixture text must not contain `http`. The page omits `Cp-CLI`. The picker test that treats the third `s` as a return to All sources moves that return to the fourth step. The both-flags test that expects All sources for `--source cursor-cli --source cursor-ide` expects those two names marked, Copilot omitted, and the next `s` on All sources.

## Risks / Trade-offs

- [Grep argument keys differ between calls (`path` and `paths`)] → The caption reads `pattern` and does not depend on the path key.
- [The source line is wider by `Copilot-CLI`] → Leave it one unwrapped line. At 80 columns the four names still fit. A narrower terminal can clip it, which is the same rule the line already follows.
- [A future column label longer than six characters truncates] → `Cp-CLI` fits. Widening stays a later change.
- [Event assistant text and `assistant_response` could diverge] → When the log is readable, the page uses the log so tools stay in order. The turn table is the fallback.
- [Opening the database writable would touch the WAL] → `SqliteOpenMode.ReadOnly` avoids that. The byte check covers the database, the event log, and the checkpoint index.

## Migration Plan

No stored data moves. Revert the change to drop the registry row, the reader, and `--copilot-root`. Cursor rows and the two-source cycle return.
