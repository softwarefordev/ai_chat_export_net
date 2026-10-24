# Tasks

## 1. Combined catalog

- [x] 1.1 Add `Source` to `Chat`, defaulting to `cli`, and a source registry whose column labels are `Cu-CLI` and `Cu-IDE`. Verify an existing agent-chat fixture still lists that chat as `cli` and a missing projects root adds no IDE rows and does not fail.
- [x] 1.2 Load `<projects-root>/<project>/agent-transcripts/<id>/<id>.jsonl`, using the first user-query line as the title, the file's last-write time as the row time, and `workspacePath` from `.workspace-trusted` as the workspace. Verify a titled transcript sits in the combined newest-first list, an empty query uses the transcript id, a missing workspace path shows the project folder name and is omitted from This workspace, a custom `--projects-root` is the only IDE tree read, and an unreadable JSONL is omitted with a stderr warning that names its id.
- [x] 1.3 Drop an IDE row whose id is already in the chat catalog, then sort by updated time and id. Verify the shared id appears once as `cli`, and a newer IDE file appears above an older agent chat.

## 2. IDE export

- [x] 2.1 Adapt each JSONL `user` or `assistant` line into the message list `TranscriptBuilder` already accepts, mapping `tool_use` to the existing tool-call shape, skipping `turn_ended`, and naming any other line in the footer. Verify two assistant messages separated by `turn_ended` share one Agent heading, a Shell call with a description captions that description and omits the command text when `--tools` is set, and a line of another type is in the footer and absent from the body.
- [x] 2.2 Export an `ide` row from `<id>.jsonl` and keep opening `store.db` for a `cli` row. Verify an IDE id writes one HTML file without opening the picker, the page shows the session title and workspace and contains neither `Cu-CLI` nor `Cu-IDE`, a shared id exports the agent chat's query, the JSONL file and `.workspace-trusted` are unchanged, and `--markdown` still writes Markdown only.

## 3. Picker source column and filter

- [x] 3.1 Draw the six-character source column between the workspace and the time, and print the source line under the scope header. Verify two rows with different title lengths share workspace, source, and time columns, `Cu-CLI` and `Cu-IDE` are the cells, a long title still ends in `...` without moving the source or time columns, and `Last updated` still ends on the time column. Update the column test so the first data row is the line after the source line.
- [x] 3.2 Cycle `s` and `S` through All sources, Cursor-CLI, and Cursor-IDE, reset the selection to the first visible row, and type `s` into the name-line draft. Verify the footer names `s`, a default open marks All sources, one `s` marks Cursor-CLI, another `s` from Cursor-IDE returns to All sources, left and right change workspace scope without changing the source filter, and This workspace plus Cursor-CLI shows only agent chats from the launch directory.
- [x] 3.3 Show an empty list when both filters exclude every row, and leave Enter as a no-op there. Verify Enter on that empty list writes no file.

## 4. Command line

- [x] 4.1 Accept repeatable `--source` values `cursor-cli` and `cursor-ide`, and `--projects-root`, and reject an unknown `--source` before the catalog is read. Verify `--source cursor-cli` opens the picker on Cursor-CLI, both flags together include both stores, `--source cursor-cli` then `s` moves to Cursor-IDE, an agent chat id with `--source cursor-ide` is reported not found and writes no file, and `--source other` writes no file.
- [x] 4.2 Name both stores, the source column, and `--source` in `README.md` and in help. Verify help lists `--projects-root` and `--source`, and the README names `Cu-CLI`, `Cu-IDE`, and `~/.cursor/projects`.

## Workflow follow-up

- Implement with `/opsx-apply` after this proposal is reviewed.
- Archive with `/opsx-archive` after implementation is reviewed.
