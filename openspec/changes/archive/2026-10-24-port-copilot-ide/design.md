# Design

## Context

See proposal.md for why. `SessionSources.All` drives the column, the filter line, `--source`, and the `s` cycle. `Catalog.LoadSessions` merges the chat catalog, IDE rows, and Copilot CLI rows, then sorts newest first. `Exporter.ExportSession` branches on `Chat.Source`. `Transcript` captions already cover Read, Glob, Shell, Grep, and Create. Copilot event logs are read by `CopilotTranscript`. Version-3 JSON and Nitrite documents stay in their own stores. A current VS Code or Rider chat can keep its transcript in `<copilot-root>/session-state/<id>/events.jsonl`.

## Goals / Non-Goals

**Goals:**

- One new registry row selects VS Code, Rider, and Visual Studio Copilot sessions together.
- Read those stores read-only and export them through the existing transcript path.

**Non-Goals:**

- A separate filter or column per IDE host.
- JetBrains AI Assistant chats, and Copilot background-agent databases.
- A Nitrite package or a Java runtime.
- Showing the source cell on the HTML or Markdown page.
- A package description or contributor-instructions file. This console app has neither.
- Rewriting the main spec's title and Purpose.

## Decisions

### One registry row

Append one row after Copilot CLI:

| stored id | `--source` | filter label | column |
| --- | --- | --- | --- |
| `cp-ide` | `copilot-ide` | `Copilot-IDE` | `Cp-IDE` |

`Cp-IDE` is six characters, so `Picker.SourceWidth` stays 6. The cycle becomes All sources, Cursor-CLI, Cursor-IDE, Copilot-CLI, Copilot-IDE, All sources. `--source` choices come from the registry. `Allowed` still returns null only when every registry id is chosen, so `--source cursor-cli --source cursor-ide` stays a subset and the next `s` returns to All sources.

Alternative: three rows (`cp-vsc`, `cp-rd`, `cp-vs`). That names the host in the column and pushes the source line past 80 columns. The chosen filter is one slot and one cell.

### Roots

Add a Copilot IDE reader beside `Copilot.cs`. `LoadSessions` gains optional VS Code and Copilot IDE roots. Null skips that store. `Run` always passes the resolved roots. Direct `ExportSession` defaults them to null, so tests do not scan the user profile.

`--vscode-root` defaults to the stable Visual Studio Code User directory: `%APPDATA%\Code\User` on Windows, `~/Library/Application Support/Code/User` on macOS, and `~/.config/Code/User` on Linux. `--copilot-ide-root` defaults to the local `github-copilot` directory: `%LOCALAPPDATA%\github-copilot` on Windows, `~/Library/Application Support/github-copilot` on macOS, and `~/.config/github-copilot` on Linux. Use those paths explicitly. `Environment.SpecialFolder` does not match the macOS locations. A missing root adds no rows.

Do not drop a Copilot IDE id that matches a Cursor id or a Copilot CLI id. Append the IDE rows and sort newest first.

The picker load omits a session with no user message and no assistant text. The id lookup includes those empty drafts so the existing no-transcript message can fire.

### VS Code documents

List `workspaceStorage/*/chatSessions/*.{json,jsonl}` and `globalStorage/emptyWindowChatSessions/*.{json,jsonl}` under the VS Code root. A JSON document is the session. A JSON file or JSONL line that is a `kind` 0 object uses its `v` object. A JSONL file uses the first such snapshot and ignores later lines. Read UTF-8 and throw on invalid bytes.

Title is `customTitle` when it has text, otherwise the first line of the first `message.text`, otherwise the id. Time is `lastMessageDate`, otherwise `creationDate`, as epoch milliseconds, shown with `Catalog.FormatTimestamp`. The workspace path is the `folder` file URI in the sibling `workspace.json`; the column is its last segment. A Windows file URI drops the leading slash before the drive letter. An empty-window session has no `workspace.json`, so the column is empty and This workspace omits the row.

`message.text` is the user query. A response part with a string `value` and no `kind` is assistant text. `toolInvocationSerialized` becomes a tool call. Ignore `textEditGroup`, `codeblockUri`, `inlineReference`, `mcpServersStarting`, `undoStop`, `progressTaskSerialized`, and `confirmation`. Do not copy avatar or icon fields.

### Rider and Visual Studio documents

Rider sessions are `rd/chat-agent-sessions/**/copilot-agent-sessions-nitrite.db`. Visual Studio sessions are the same file name under `vs/`. `rd/bg-agent-sessions` stays out of the list. A missing `vs` directory adds no Visual Studio rows.

Read the file with `File.ReadAllBytes`. Scan Java `TC_STRING` values (`0x74` plus a big-endian 16-bit length, UTF-8, length 1 through 10000, printable or containing a newline). Do not open the file for write. Two document shapes are sessions. The first groups `sessionId`, timestamps, `source`, and `value`: a `source` of `user` is the user message and a `source` of `copilot` is assistant text, the title is the first user value's first line otherwise the id, the time is `modifiedAt` otherwise `createdAt`, and the workspace column is the last segment of `dirName` or `path` (`path` wins when both are present). The second groups `name`, `value`, the following text, `source` plus `copilot`, and `conversationId` plus a UUID. When that UUID has an event log, the log is the transcript and the row is one `Cp-IDE` entry.

Alternative: shell out to Java or add a Nitrite package. The port stays on the BCL, so the scanner is limited to the fields above.

### Event logs

`globalStorage/agent-host.db` is a read-only SQLite file. Call `SqliteConnection.ClearPool` after closing it so a test can delete the file. A `chats_v2` row with `provider_data.sdkSessionId` points at `session-state/<id>/events.jsonl` under the Copilot CLI root. `metadata.data.summary` is the title when it has text. A `tombstoned` row stays out of the list. A missing database adds no rows. An unreadable database warns `Unreadable Copilot IDE session agent-host: {error}`.

After the IDE rows are merged, any other event log with a user or assistant message whose id is not already listed becomes one `Cp-CLI` row. The title is the first user line. The time is the last event timestamp, shown with the catalog's local time form; when that stamp does not parse, use the log's last-write time. The workspace column is the last segment of `session.start` `context.cwd`. An id claimed by a VS Code agent-host link or a Rider conversation id is not also listed as that unlisted row. A session-state directory with no such log stays out, which is the remaining case of the current "directory without a database session" scenario.

A directory path exports through the existing Copilot event reader. The event log and the Nitrite file stay byte-for-byte unchanged.

### Tool captions

Map a Copilot IDE tool id onto the names `caption` already knows, then call it. Add an `Edit` arm beside `Create`:

| tool id | caption name | argument |
| --- | --- | --- |
| `copilot_readFile` | `Read` | path |
| `copilot_createFile` | `Create` | path |
| `copilot_findFiles` | `Glob` | pattern as `glob_pattern` |
| `run_in_terminal` | `Shell` | description only |
| `copilot_replaceString`, `copilot_multiReplaceString` | `Edit` | path |

Any other tool id is its own caption, without argument values. `--tools` shows the caption and omits the result and the command text. `--verbose` adds the result text and still omits the command text. The HTML Agent heading stays `<h2>Agent</h2>`.

### Tests and docs

Every `Exporter.Run` path that reaches the catalog passes `--vscode-root` and `--copilot-ide-root` to directories that are not the user's VS Code or `github-copilot` folders. Fixtures are temporary JSON, JSONL, and byte-built Nitrite files. Fixture text must not contain `http`. Compare file bytes with `Assert.Equal` on each `byte[]`, not on a tuple of arrays. The page omits `Cp-IDE`.

Update the picker cycle test so the return to All sources is the step after Copilot-IDE. The both-Cursor-flags test omits a Copilot IDE row as well as a Copilot CLI row.

README opening and picker lines name Cursor and Copilot, including `Copilot-IDE`, `Cp-IDE`, `--vscode-root`, and `--copilot-ide-root`. Keep `Enter asks for a file name`, `HTML is the default.`, `Up and Down`, `Left and Right`, `Esc closes`, the markdown sentence, and the existing `Cu-CLI`, `Cu-IDE`, and `Cp-CLI` phrases. Help keeps `usage: ai-chat-export` and lists `copilot-ide`.

## Risks / Trade-offs

- [The Nitrite scanner depends on Java string order] → Tests build the `TC_STRING` sequence the reader expects, including `name`, `value`, `source`, `copilot`, and `conversationId`. A future Copilot build that moves fields without those names will warn and omit that file.
- [A current IDE chat can live only in an event log] → The agent-host link or the Rider conversation id claims it as `Cp-IDE`. A log with neither link still appears as `Cp-CLI`.
- [Visual Studio's `vs` layout is inferred from `rd`, and this machine has no IDE hive] → Search for the same chat database name under `vs`. A missing directory adds no rows.
- [JSONL mutation lines can hold draft input] → The transcript comes from the snapshot. Empty snapshots stay out of the picker.
- [The source line is wider by `Copilot-IDE`] → Leave it one unwrapped line. At 80 columns the five names still fit.
- [Reading Nitrite by opening it in a Java store would write the file] → `File.ReadAllBytes` avoids that. The byte check covers the session file.

## Migration Plan

No stored data moves. Revert the change to drop the registry row, the reader, and the two root flags. The cycle returns to Copilot-CLI and then All sources.
