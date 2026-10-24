# Tasks

## 1. Safe image embedding

- [x] 1.1 Add one helper that accepts only `image/png`, `image/jpeg`, `image/gif`, and `image/webp` (ordinal-ignore-case, no parameters) and a standard base64 payload with no whitespace, treating a missing media type as `image/png`. Use it from `ImageParts`, `HtmlRenderer`, and `MarkdownRenderer`, HTML-encode the accepted media type and payload, and omit a rejection from both bodies with no footer line. Verify tests cover an `image/png` payload, `IMAGE/PNG`, a missing media type, and one each of JPEG, GIF, and WebP; a payload containing `"` and a media type `image/svg+xml` are absent from HTML and Markdown; the HTML page contains no `<script` and no external address; and the existing `AAAA` PNG fixture still embeds as `data:image/png;base64,AAAA`.

## 2. Reserved device file names

- [x] 2.1 After `Picker.Stem` cleanup, treat `CON`, `PRN`, `AUX`, `NUL`, `COM1` through `COM9`, and `LPT1` through `LPT9` as reserved, ordinal-ignore-case. Replace a reserved default stem with the chat id in `SafeStem` and `DefaultFileName`, and replace a reserved typed stem with the default file name in `ConfirmFileName`. Verify a title `CON` and a title `com1` use the chat id, `Console notes` stays `Console notes`, a typed `PRN` on title `Notes` writes the title file name, and a typed `AUX` on title `CON` uses the chat id.
- [x] 2.2 When `--name` has a reserved file stem, report an error, write no file, and exit unsuccessfully. Do not invent a substitute path. Verify `--name` ending in `CON.html` does that, and an ordinary `--name` path still writes.

## 3. Parsers that must finish the export

- [x] 3.1 When `FormatTurnTime` matches the stamp shape but the date is impossible, return the whitespace-normalized stamp instead of throwing. Leave `CompactNavTime` to echo an unparsed stamp. Verify a user turn recorded as `Friday, Oct 32, 2026, 11:13 AM (UTC-7)` shows that text on the User section and in the HTML navigator, and the export writes the file.
- [x] 3.2 Read conversation JSON strings with `TryGetValue<string>`. Skip a message whose role is not a string, and ignore a content part whose type is not a string. Do not widen the export catch list. Verify an assistant message with a numeric part type and a text part exports only the text, a numeric role is omitted while a later user query is exported, and both cases write the file.
- [x] 3.3 In `ProtobufFields`, return null before any `int` cast when a length-delimited field is greater than `int.MaxValue` or when the 64-bit start plus length runs past the buffer. Verify a root blob with a length that does not fit in a 32-bit length, and no recoverable role message, makes the CLI report that the chat has no transcript, write no file, and avoid an unhandled exception.

## 4. Markdown metadata lines

- [x] 4.1 Write the Markdown title, workspace, and chat timestamp as single lines, turning a newline in those values into a space, and leave transcript body line breaks unchanged. Verify a title newline stays on the title line, a workspace newline becomes a space on the metadata line, and an assistant blank line remains in the body.

## 5. Activity cycle on a real stderr

- [x] 5.1 Remove `TerminalWriter`. Give `Activity` an `isTerminal` flag, and let `ExportSession` default that flag to true only when stderr is `Console.Error` and `Console.IsErrorRedirected` is false. Join the spinner in `Dispose`, then dispose `ManualResetEventSlim`, and ignore a second `Start`. Verify the existing activity tests pass by passing the flag, no test or production type references `TerminalWriter`, and a non-terminal `StringWriter` still receives no activity frame.

## 6. Lists, caches, and dead code

- [x] 6.1 Validate `--theme` against one ordered name list beside the HTML palettes. Keep the help const hand-written and unchanged. Verify an unknown theme still fails with no file written, the four current names still export, and a test shows the help text lists the same names in the same order as that list.
- [x] 6.2 Replace the `Read`, `Glob`, and `Shell` caption branches with a map from tool name to formatter. An unknown tool still returns its name. Verify the existing file-read, glob, and command caption tests pass, including a command with no description.
- [x] 6.3 Reuse `Regex` instances for `user_query`, `conversation_summary`, `cursor_commands`, and `timestamp` in `ElementTexts` and `StripElement`, still culture-invariant and single-line. Verify the existing query, detail, and timestamp transcript tests pass.
- [x] 6.4 Remove `PickerResult.DeleteRequested` and the blob-id identity maps in `Store.Load` and `OrderedIds`. Keep the 32-byte id scan and its order. Verify picker tests still show Backspace does not remove a chat, store tests still load the same messages, and the solution no longer mentions `DeleteRequested`.

## 7. Assembly surface and console interop

- [x] 7.1 Declare `InternalsVisibleTo` for `AiChatExport.Tests` on the app project, and make every implementation type except `Program` internal. `Program.Main` stays public. Verify `dotnet test tests/AiChatExport.Tests` compiles and the suite passes.
- [x] 7.2 Move `GetStdHandle`, `GetConsoleMode`, and `SetConsoleMode` to a partial class with `LibraryImport`, still called only on Windows and still setting virtual-terminal mode `0x0004`. Set `AllowUnsafeBlocks` because the generator requires it. Verify the app project builds and the picker and export tests pass.

## 8. Editor conventions

- [x] 8.1 Add `.editorconfig` recording UTF-8, 4-space indent, Allman braces, and file-scoped namespaces. Do not set `end_of_line`, do not raise analyzer severity, and do not enable `TreatWarningsAsErrors`. Verify `dotnet format --verify-no-changes` exits 0 with no unrelated diff.

## 9. Integration

- [x] 9.1 Run `dotnet test` for `AiChatExport.sln`. Verify the run exits 0.

## Workflow follow-up

- Implement with `/opsx-apply` after this proposal is reviewed.
- Archive with `/opsx-archive` after implementation is reviewed.
