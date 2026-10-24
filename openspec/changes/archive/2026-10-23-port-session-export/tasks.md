# Tasks

## 1. Solution

- [x] 1.1 Add `AiChatExport.sln`, `src/AiChatExport` (`net10.0`, assembly name `ai-chat-export`), and `tests/AiChatExport.Tests` (xUnit) referencing it. Verify `dotnet build` and `dotnet test` succeed.
- [x] 1.2 Add `Microsoft.Data.Sqlite` to the console project only. Verify `dotnet build` restores and the test project does not add another application dependency.

## 2. Catalog

- [x] 2.1 Port catalog loading from `ai_chat_export/catalog.py`: schema version 1, title or id, workspace folder name, newest first, unreadable metadata and unsupported schema reported and omitted, a chat with no `store.db` still listed. Format times in the local timezone with invariant English months. Verify tests for a titled chat, a missing title, `Aug 15, 2026 04:42 PM`, newest first, unsupported schema, unreadable metadata, and metadata without a store.
- [x] 2.2 Port All chats and This workspace, case-insensitive on Windows and case-sensitive elsewhere. Verify the launch-directory test and the letter-case test.

## 3. Store

- [x] 3.1 Port `tests/support.py` `write_chat` and `write_db`, and the read-only raw id-list loader from `ai_chat_export/store.py`. Verify a synthetic store returns messages in order without duplicating the checkpoint, and that loading leaves the file bytes unchanged.
- [x] 3.2 Port `write_protobuf_db` and the wire walk (aligned ids, then the hand-rolled walk, then scanning). Verify protobuf checkpoints precede the live tail once, and the footer names skipped blobs and unlinked images while omitting checkpoint and side blobs that are not images.

## 4. Transcript

- [x] 4.1 Port the default transcript from `ai_chat_export/transcript.py`: `user_query` inner text, a plain prompt when there are no wrapper elements, omitted summary and command blocks, and one Agent heading per stretch. Verify the query, plain-prompt, summary-only, and agent-stretch tests.
- [x] 4.2 Port `--tools`, `--verbose`, `--detail`, tool captions, and catalog-style turn times. Verify captions omit the command text (including a command with no description), detail and verbose show a summary or command block once, and a recorded turn time renders as `Oct 23, 2026 11:13 AM` on the User and Agent sections.

## 5. Renderers

- [x] 5.1 Port `ai_chat_export/render_html.py`, including both palettes, User and Agent panels, the 30rem navigator, assistant paragraphs, lists, fenced code, inline code, and embedded turn images. Verify the HTML cases in `tests/test_render.py`, including no script and no external address.
- [x] 5.2 Port `ai_chat_export/render_md.py`. Verify a Markdown export contains the same visible turns as HTML and does not contain the theme colors.

## 6. Export command

- [x] 6.1 Port argument handling and `export_session` from `ai_chat_export/__main__.py`: `--id`, `--theme`, `--tools`, `--verbose`, `--detail`, `--markdown`, `--out`, `--name`, `--chats-root`, title file stems, one file per export, and the stderr activity cycle. Verify the cases in `tests/test_export.py`, including Markdown leaving an existing HTML file unchanged, `--name` conflicting with `--out`, and a quiet cycle when stderr is not a terminal.
- [x] 6.2 Write help whose usage line names `ai-chat-export`, and a README titled `ai-chat-export` that documents the flags and the one-file export. Verify `dotnet run --project src/AiChatExport -- --help` exits 0 and prints `usage: ai-chat-export`, and that the README title and `--markdown` wording match that help.

## 7. Picker

- [x] 7.1 Port picker frame rendering from `ai_chat_export/picker.py` (columns, scope, theme footer, `i` toggle) as a function of keys and a frame writer. Verify the column-alignment test and the id-toggle test.
- [x] 7.2 Port the file-name line, block cursor (`\x1b[1 q` in the cell after the draft), hidden cursor on the list, and restore (`\x1b[?25h\x1b[0 q`) on Esc and Ctrl+C. Wire `Console.ReadKey` only at the program edge. Verify the name-line tests and the cursor-restore tests.
- [x] 7.3 Document picker keys in the README: Up and Down move, Left and Right switch scope, Enter asks for a file name, Esc closes, and `i` shows the chat id. Verify the README contains those keys.

## 8. Integration

- [x] 8.1 Run the full suite and one export through the built executable. Verify `dotnet test` passes, and that the built executable (`ai-chat-export.exe` on Windows) given a synthetic chat id writes one HTML file and prints that path.

## Workflow follow-up

- Implement with `/opsx-apply`. Archive only after that work is reviewed.
