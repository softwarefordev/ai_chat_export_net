# Proposal

## Why

The Python `ai-chat-export` on `main` (https://github.com/softwarefordev/ai_chat_export) lists Cursor IDE transcripts beside agent chats and can limit that list to one store. This solution still reads only `~/.cursor/chats`, so an IDE session never appears in the picker or in `--id`.

## What Changes

- Add IDE transcripts to the picker and to `--id`, in one list with agent chats, newest first. A missing projects root adds no IDE rows and does not fail the command.
- Read each transcript from `<projects-root>/<project>/agent-transcripts/<id>/<id>.jsonl`. `--projects-root` defaults to `~/.cursor/projects`.
- Show a six-character source column between the workspace and the time: `Cu-CLI` for an agent chat and `Cu-IDE` for an IDE transcript. An id in both stores is listed once, from the agent chat, as `Cu-CLI`.
- An IDE row's title is the first line of its first user query, or the transcript id when that line is empty. Its time is the JSONL file's modification time. Its workspace comes from `workspacePath` in the project's `.workspace-trusted` file; without that path the column shows the project folder name and This workspace omits the row.
- Export an IDE id through the same HTML or Markdown path. `turn_ended` adds no section. Any other unrecognized line is named in the footer. Tool calls use the existing caption rules. The written page omits the source cell. The JSONL file and `.workspace-trusted` stay unchanged.
- Add a source line under the workspace scope. `s` cycles All sources, Cursor-CLI, and Cursor-IDE. Left and right still switch workspace scope, and both filters apply together. On the file-name line, `s` is typed into the draft.
- Add repeatable `--source`, accepting `cursor-cli` and `cursor-ide`. It sets the filter the picker opens on. An unknown value is rejected before the catalog is read. An id outside the filter is not found. An empty filtered list writes nothing on Enter.

Assumption: match the running Python program after the source filter, not the earlier three-character `CLI` and `IDE` cells. The image allowlist, reserved device names, and other review fixes already in this solution stay as they are. GitHub Copilot prompt files in the Python repo are not part of the exporter and are left out. `--markdown` still writes Markdown only.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `session-export`: The catalog includes IDE transcripts, the picker shows a Cursor source column and a source filter, and an IDE id exports through the same transcript path.

## Impact

- `src/AiChatExport/Catalog.cs` gains a source on each chat and merges the IDE tree.
- A new JSONL reader feeds `TranscriptBuilder` for IDE rows. `Export.cs` adds `--projects-root` and `--source` and chooses the reader from the row's source.
- `src/AiChatExport/Picker.cs` adds the source column, the source line, and the `s` key. Column-width tests move the first data row down by one line.
- `README.md` names both stores, the source column, and `--source`.
- HTML themes, the navigator filter, and the Markdown renderer stay as they are. The Python repository is not edited.
