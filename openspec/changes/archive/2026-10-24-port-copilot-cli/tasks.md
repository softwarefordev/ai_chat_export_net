# Tasks

## 1. Registry

- [x] 1.1 Append a source registry row stored id `cp-cli` / argument `copilot-cli` / filter label `Copilot-CLI` / column `Cp-CLI` after the IDE row. Leave the source column six characters wide. Verify a catalog test that a Copilot row's label is `Cp-CLI`, its stored id is `cp-cli`, and agent and IDE labels stay `Cu-CLI` and `Cu-IDE`.

## 2. Catalog

- [x] 2.1 Read `<copilot-root>/session-store.db` read-only into the combined newest-first list. A null copilot root skips Copilot. `Run` passes `~/.copilot` unless `--copilot-root` is set. Keep a Copilot row whose id matches a Cursor session. Title from the summary, then the first user line, then the id. Show a parsed `updated_at` in the catalog's local time form, and show an unparsed value collapsed as stored. Workspace column is the last segment of `cwd`; a missing `cwd` leaves that column empty and This workspace omits the row. A missing root adds no rows and does not fail. A session-state directory with no database row stays out of the list. An unreadable database warns `Unreadable Copilot store: {error}` and the other sessions remain. Update every existing `Run` call that reaches the catalog so it passes `--copilot-root` to a directory that is not the user's `~/.copilot`. Verify catalog tests for a session beside an agent chat, a newer Copilot row above an older chat, the missing root, a custom root, a directory without a row, the shared id, the title, the time, the unparsed time, the workspace, and the unreadable warning.

## 3. Export

- [x] 3.1 Export a `cp-cli` id through the existing HTML and Markdown path. Default text comes from `session-state/<id>/events.jsonl` when that file reads: user message, non-empty assistant text under one Agent heading until the next user message, and no Agent heading for an empty assistant reply. Missing or unreadable events fall back to the turn table; an unreadable log warns `Unreadable Copilot events {id}: {error}`. No user or assistant text reports `Chat has no transcript: {id}` and writes no file. A checkpoint index is not a turn and not a footer line. The database, event log, and checkpoint index stay byte-for-byte unchanged. The page omits the source cell. Verify export tests for the user and assistant text, the empty assistant, the empty session, the missing log, the unreadable log, the ignored index, the unchanged bytes, and that the page contains neither `Cp-CLI` nor `Cu-CLI`.

- [x] 3.2 With `--tools`, caption `view` as `Read` plus the path, `glob` as `Glob` plus the pattern, `powershell` as `Ran` plus the description, `grep` as `Grep` plus the pattern, and `create` as `Create` plus the path, and omit the tool result and the command text. `--verbose` adds those captions, system message text, reasoning text, and the tool result. Encrypted content stays out of the export. Verify an export test for each caption, for a verbose reasoning text and tool result, for the system message, and for omitted encrypted content.

## 4. Picker and command line

- [x] 4.1 Name Copilot-CLI on the source line. Cycle `s` from Cursor-IDE to Copilot-CLI and from Copilot-CLI back to All sources, and reset the selection to the first visible row. Update the existing cycle test that treated the third `s` as a return to All sources. `--source cursor-cli` still advances to Cursor-IDE. Both Cursor flags mark Cursor-CLI and Cursor-IDE, omit Copilot rows, and the next `s` selects All sources. On the file-name line, `s` still appends to the draft. Verify picker tests for the Copilot step, the return to All sources, the two-flag subset, and that the footer still contains `s source`, `i id`, and `Enter names the file`.

- [x] 4.2 Accept `--source copilot-cli` from the registry and `--copilot-root`. An unknown `--source` still fails before the catalog is read and writes no file. A Copilot id passed with `--source cursor-cli` is reported not found and writes no file. The picker opens on Copilot-CLI when that flag is set. Document the source and the root in the README and in help. Verify an export test for the flag, the unknown value with no file, and the excluded id. Verify the README contains `Copilot-CLI`, `Cp-CLI`, and `--copilot-root`, and help contains `copilot-cli`.

## 5. Integration

- [x] 5.1 Run `dotnet test` and verify the suite passes.

## Workflow follow-up

- Implement with `/opsx-apply` after this proposal is reviewed.
- Archive with `/opsx-archive` after implementation is reviewed.
