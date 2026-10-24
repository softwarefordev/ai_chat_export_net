# Design

## Context

See proposal.md for why this change exists. The executable is one assembly, `AiChatExport`, with static types in `Export.cs`, `Store.cs`, `Catalog.cs`, `Picker.cs`, `Transcript.cs`, and `Render.cs`. Tests in `AiChatExport.Tests` call those types directly. Nullable reference types are already on. The chat store is opened with `SqliteOpenMode.ReadOnly` and the SQL text is constant.

The behaviors to preserve are the current help text, the four theme names, the default transcript, and a read-only store. New externally visible behavior is only what `specs/session-export/spec.md` adds or modifies.

## Goals / Non-Goals

**Goals:**

- Put each new or tightened behavior behind one code path, so HTML and Markdown cannot drift.
- Make the production activity cycle follow the existing terminal requirement, and make the test double a flag.
- Shrink the accidental public surface of the executable to the process entry point.
- Use one list for theme acceptance and one map for known tool captions.

**Non-Goals:**

- Adopting `System.CommandLine`, a plugin host, or a generated protobuf schema.
- Adding themes, tools, or export formats.
- Turning on `TreatWarningsAsErrors`, raising analyzer severity, or reformatting files that this change does not otherwise touch.
- Changing SQLite mode, the SQL, or the happy-path transcript rules.

## Decisions

### 1. One image gate, then encode

Add one helper that accepts a media type and payload and returns the media type to write, or rejection. Both renderers call it. `ImageParts` uses it too, so a rejected image is not stored on the transcript. Rejection deletes the image from the body. It does not add a footer line.

Accepted media types are exactly `image/png`, `image/jpeg`, `image/gif`, and `image/webp`, compared ordinal-ignore-case. A missing media type becomes `image/png`. Parameters such as `image/png;charset=utf-8` are rejected.

A payload is accepted only when it is standard base64: alphabet `A–Z`, `a–z`, `0–9`, `+`, `/`, and `=`, length a multiple of 4, padding only at the end. ASCII whitespace is not stripped. The HTML writer still runs `WebUtility.HtmlEncode` on the accepted media type and payload. Markdown writes the accepted values only after that gate, so a quote or `)` never reaches the link.

Alternative considered: encode and still emit every payload. That keeps a quote inside an attribute, but it does not stop a non-image data URL, and a `)` still breaks the Markdown link. Omission is the rule in the spec.

### 2. Reserved device names are a stem check, not a character class

`Picker.Stem` keeps its current cleanup (illegal characters to spaces, trim, 80-character cap). It does not know the chat id. A separate ordinal-ignore-case set (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) is applied to the cleaned stem:

- `SafeStem` and `DefaultFileName` replace a reserved stem with the chat id.
- `ConfirmFileName` replaces a reserved typed stem with the fallback, which is already the safe default name.
- `--name` does not pick a substitute path. If `Path.GetFileNameWithoutExtension` is reserved, the export reports an error, writes nothing, and returns unsuccessful, same as when `--name` is an existing directory.

Alternative considered: rewrite `--name CON.html` to `<chat-id>.html` beside it. That writes a path the user did not pass. The spec scenario for `--name` is an error and no file.

### 3. Bad dates return the normalized stamp

`FormatTurnTime` already returns the whitespace-normalized stamp when the shape does not match. Construction of the `DateTime` moves into a guard: an impossible day, month, or hour returns that same normalized stamp instead of throwing `ArgumentOutOfRangeException`. The navigator already shows an unparsed stamp as stored, so `CompactNavTime` stays as it is.

Alternative considered: a shared timestamp parser for headings and the navigator. The two formats are different on purpose (catalog time versus `YY-MM-DD hh:mm`). Sharing construction would not remove a second grammar.

### 4. Untrusted JSON uses TryGetValue

String fields read from conversation JSON use `TryGetValue<string>`. A role that is not a string skips that message. A part `type` that is not a string does not match any part kind. The export catch list does not gain `InvalidOperationException` or a blanket `Exception`. Callers that already tolerate a missing string stay on that path.

### 5. Protobuf lengths are checked before they become ints

`ProtobufFields` treats a length-delimited field as failure, and returns null, when the varint length is greater than `int.MaxValue` or when the start plus that length, computed in a 64-bit integer, runs past the buffer. It does not cast to `int` first. A null layout keeps today's fallback. If that fallback recovers no role messages, `ExportSession` already reports that the chat has no transcript and writes no file.

Alternative considered: catch `ArgumentOutOfRangeException` around `Store.Load`. That hides the next bounds bug and can swallow a real defect in the caller.

### 6. Terminal is a bool, and the wait handle is disposed

Remove `TerminalWriter`. `Activity` takes `isTerminal`. `ExportSession` takes an optional flag. When the flag is omitted, stderr is a terminal only when it is `Console.Error` and `Console.IsErrorRedirected` is false. Tests that need the cycle pass true. `Dispose` joins the spinner thread, then disposes `ManualResetEventSlim`. A second `Start` does not create another thread.

### 7. InternalsVisibleTo, not a public library

The test assembly name is `AiChatExport.Tests`. The app project declares `[InternalsVisibleTo("AiChatExport.Tests")]`. `Program.Main` stays public. Catalog, store, picker, transcript, render, and export types become `internal`. No implementation type needs to stay public for the executable to run.

### 8. LibraryImport for the three console calls

Move `GetStdHandle`, `GetConsoleMode`, and `SetConsoleMode` to a partial internal class with `LibraryImport`. The Windows-only call site and the virtual-terminal flag `0x0004` stay. Set `AllowUnsafeBlocks` on the app project because the `LibraryImport` generator emits unsafe code (`SYSLIB1062`). Non-Windows still returns before those methods are called.

### 9. Extension points are data, not hosts

Theme acceptance reads one ordered name list that sits next to the palettes. `--theme` rejects a name that is not on that list, and the invalid-theme error stays. The help const stays hand-written because its sentences are specified; the names in that const stay the same four, in the same order.

Tool captions become a map from tool name to a function that formats `Read`, `Glob`, and `Shell`. An unknown tool still returns its name. Adding a caption is a new map entry. There is no registration API.

`ElementTexts` and `StripElement` reuse `Regex` instances for `user_query`, `conversation_summary`, `cursor_commands`, and `timestamp` instead of compiling a pattern per call. Flags stay culture-invariant and single-line.

### 10. Dead surface and the identity maps

`PickerResult.DeleteRequested` is removed. The picker still never deletes a chat. The two export checks that looked at the flag look at a missing chat id only.

`Store.Load` builds a dictionary that maps each blob id to itself, and `OrderedIds` builds another. Both go away. The id scan still walks the blob and matches 32-byte windows against the blob id set. Match order and results stay.

### 11. EditorConfig records the current layout

Add `.editorconfig` for `[*]` and `[*.cs]`: UTF-8, indent 4, Allman braces, file-scoped namespaces. Do not set `end_of_line`, so existing line endings stay. Do not set style rules to warning. Do not enable `TreatWarningsAsErrors` or a new `AnalysisLevel`.

## Risks / Trade-offs

- [Base64 that contains newlines is omitted] → The spec treats those characters as outside the alphabet. Known fixtures are continuous base64. A follow-up can allow whitespace if a real store needs it.
- [A title that is exactly `CON` no longer writes `CON.html`] → The chat id is the stem. A title that only contains those letters, such as `Console notes`, is unchanged.
- [`InternalsVisibleTo` or `internal` misses a test caller] → The test project fails to compile; the fix is visibility, not a new public API.
- [`LibraryImport` requires unsafe code] → `AllowUnsafeBlocks` is set only on the app project. The three calls stay equivalent and are still reached only from the Windows branch.
- [Theme list and the help const can still drift] → Help wording stays specified. The task that introduces the list also checks the help const names against that list in a test.

## Migration Plan

This is a local console tool. Nothing is deployed, and existing export files are left on disk. Rollback is reverting the change. Users who relied on a reserved device name as a file stem get the chat id, or an error when they passed `--name`.

## Open Questions

None. The `--name` reserved-stem outcome is specified: report the error and write no file.
