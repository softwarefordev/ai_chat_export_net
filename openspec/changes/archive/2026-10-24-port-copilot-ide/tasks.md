# Tasks

## 1. Registry

- [x] 1.1 Append a source registry row stored id `cp-ide` / argument `copilot-ide` / filter label `Copilot-IDE` / column `Cp-IDE` after the Copilot CLI row. Leave `Picker.SourceWidth` at 6. Verify a catalog test that a Copilot IDE row's label is `Cp-IDE`, its stored id is `cp-ide`, and the existing labels stay `Cu-CLI`, `Cu-IDE`, and `Cp-CLI`.

## 2. VS Code catalog

- [x] 2.1 Read VS Code version-3 chat sessions from `<vscode-root>/workspaceStorage/*/chatSessions` and `<vscode-root>/globalStorage/emptyWindowChatSessions` into the combined newest-first list. A null VS Code root skips those rows. `Run` passes the platform Visual Studio Code User directory (`%APPDATA%\Code\User`, `~/Library/Application Support/Code/User`, or `~/.config/Code/User`) unless `--vscode-root` is set. Use a JSON document as the session. A JSON or JSONL `kind` 0 object uses its `v` field, and a JSONL file uses the first snapshot while ignoring later lines. Omit a session with no user text and no assistant text from the picker. The id lookup still finds that empty draft. Keep a row whose id matches a Cursor session. Title from `customTitle`, then the first user line, then the id. Time from `lastMessageDate`, otherwise `creationDate`, in the catalog's local time form. Workspace column is the last segment of the sibling `workspace.json` folder URI; a missing path leaves that column empty and This workspace omits the row. A missing root adds no rows and does not fail. An unreadable session warns `Unreadable Copilot IDE session {id}: {error}` and the other sessions remain. Update every existing `Exporter.Run` call that reaches the catalog so it passes `--vscode-root` to a directory that is not the user's VS Code directory. Verify catalog tests for a session beside an agent chat, a newer row above an older chat, the missing root, a custom root, an empty draft omitted, a JSONL snapshot, an empty-window session omitted from This workspace, the shared id, the title, the time, the workspace, and the unreadable warning.

## 3. Rider and Visual Studio catalog

- [x] 3.1 Read Rider sessions from `<copilot-ide-root>/rd/chat-agent-sessions/**/copilot-agent-sessions-nitrite.db` and Visual Studio sessions from the same file name under `vs/`, by scanning Java `TC_STRING` values in the file bytes. A null Copilot IDE root skips those rows. `Run` passes the platform local `github-copilot` directory (`%LOCALAPPDATA%\github-copilot`, `~/Library/Application Support/github-copilot`, or `~/.config/github-copilot`) unless `--copilot-ide-root` is set. Omit background-agent databases. A missing root or a missing `vs` directory adds no rows and does not fail. Title from the first user turn, otherwise the id. Time from `modifiedAt`, otherwise `createdAt`. Workspace column is the last segment of the worktree directory or path. Update every existing `Exporter.Run` call that reaches the catalog so it passes `--copilot-ide-root` to a directory that is not the user's `github-copilot` directory. Verify catalog tests for a Rider session beside an agent chat, a Visual Studio session, a missing `vs` directory, a custom root, an omitted background-agent database, and that the session file bytes are unchanged after listing.

## 4. Event logs

- [x] 4.1 Read a VS Code agent-host chat whose `sdkSessionId` names a Copilot event log with a user or assistant message. Open `agent-host.db` read-only and call `SqliteConnection.ClearPool` after closing it. List the chat once as `Cp-IDE`. Use the agent-host summary as the title when that summary has text. Omit a tombstoned chat. A missing database adds no rows and does not fail. An unreadable database warns `Unreadable Copilot IDE session agent-host: {error}`. Verify a catalog test for the linked log, the summary title, the workspace, and the omitted tombstoned chat.

- [x] 4.2 Read a Rider Nitrite document grouped as `name`, `value`, text, `source` `copilot`, and `conversationId`. When that id has an event log, list one `Cp-IDE` row and export the log's user and assistant text. Do not also list that id as `Cp-CLI`. Leave the Nitrite file bytes unchanged. Verify a catalog test for the single row, the workspace, and the exported text.

- [x] 4.3 List a Copilot event log that has a user or assistant message and is not already a catalog row as one `Cp-CLI` row. A session-state directory with no such log stays out of the list. Verify a catalog test for a log that has no session-database row, and keep the existing test that a directory with no event log stays out.

## 5. Export

- [x] 5.1 Export a `cp-ide` id through the existing HTML and Markdown path. Show user text and assistant text, with one `<h2>Agent</h2>` until the next user message and no Agent heading for an empty assistant reply. A session with no user or assistant text reports `Chat has no transcript: {id}` and writes no file. Omit avatar addresses, icon addresses, text-edit parts, inline references, code-block addresses, and progress markers, and omit the source cell. Leave the session file byte-for-byte unchanged. Fixture text must not contain `http`. Compare unchanged bytes with `Assert.Equal` on each `byte[]`. Verify export tests for the user and assistant text, the empty assistant, the empty session, the omitted address, the omitted extra parts, the unchanged bytes, and that the page contains neither `Cp-IDE` nor `http`.

- [x] 5.2 Add an `Edit` caption beside `Create`. With `--tools`, caption a file read as `Read` plus the path, a file create as `Create` plus the path, a file search as `Glob` plus the pattern, a terminal command as `Ran` plus the description, and a file edit as `Edit` plus the path. Omit the tool result and the command text. An unknown tool id is the caption, without argument values. `--verbose` adds the caption and the tool result and still omits the command text. Verify an export test for each caption, the unknown tool id, and the verbose tool result.

## 6. Picker and documentation

- [x] 6.1 Name Copilot-IDE on the source line. Cycle `s` from Copilot-CLI to Copilot-IDE and from Copilot-IDE back to All sources, and reset the selection to the first visible row. Update the existing cycle test that treated the Copilot-CLI step as the return to All sources. `--source cursor-cli` still advances to Cursor-IDE. Both Cursor flags mark Cursor-CLI and Cursor-IDE, omit Copilot CLI and Copilot IDE rows, and the next `s` selects All sources. On the file-name line, `s` still appends to the draft. Verify picker tests for the Copilot-IDE step, the return to All sources, the two-flag subset, and that the footer still contains `s source`, `i id`, and `Enter names the file`.

- [x] 6.2 Accept `--source copilot-ide`. An unknown `--source` still fails before the catalog is read and writes no file. A Copilot IDE id passed with `--source cursor-cli` is `Chat not found` and writes no file. The picker opens on Copilot-IDE when that flag is set. Update the README so it names Cursor and Copilot, `Copilot-IDE`, `Cp-IDE`, `copilot-ide`, `--vscode-root`, and `--copilot-ide-root`, and still contains `Enter asks for a file name`, `HTML is the default.`, `Up and Down`, `Left and Right`, `Esc closes`, the markdown sentence, `Cu-CLI`, `Cu-IDE`, and `Cp-CLI`. Help keeps `usage: ai-chat-export` and lists `copilot-ide`. Verify an export test for the flag, the unknown value with no `Chat not found` and no file, and the excluded id. Verify the README and help assertions above.

## 7. Integration

- [x] 7.1 Run `dotnet test` and verify the suite passes.

## Workflow follow-up

- Archive the change after the project's review requirements are satisfied.
