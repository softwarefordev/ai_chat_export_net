# Proposal

## Why

The console exporter is a direct port: it writes a local HTML page from chat data the user did not author, and several paths treat that data as already safe or already well formed. A review of security, maintainability, extensibility, and current .NET practice found attribute injection in exported images, file names Windows reserves for devices, and parsers that throw out of the export instead of skipping bad input. The same review found an executable whose implementation types are all public, a terminal spinner that never runs on a real console, and theme and caption lists that have to be edited in more than one place.

## What Changes

- HTML and Markdown embed an image only for `image/png`, `image/jpeg`, `image/gif`, and `image/webp` with a base64 payload. The HTML data URI encodes that media type and payload. Any other media type, or a payload outside the base64 alphabet, is omitted from both bodies. A normal base64 PNG, JPEG, GIF, or WebP still embeds.
- **BREAKING:** A file stem that is a Windows reserved device name (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) is replaced the same way an empty stem is: the chat id for a default name, and the default file name when the typed name is reserved. `--name` with a reserved stem reports an error and writes no file.
- A turn timestamp that matches the expected shape but is not a real date is shown as the normalized raw text. The export finishes.
- A content part whose JSON shape is not the one the reader expects is skipped. A blob length that does not fit in the remaining bytes, or in a 32-bit length, does not end the process; if no turns are recovered, the CLI reports that the chat has no transcript and writes no file.
- Markdown title, workspace, and timestamp lines stay single lines. A newline in those values becomes a space.
- The activity cycle runs when process stderr is a console and is not redirected, which the existing activity requirement already requires. Tests request that behavior with a flag instead of a public `TextWriter` subclass.
- The executable keeps a public entry point and makes the rest of the implementation internal to the assembly, visible to the test project. Console P/Invoke uses `LibraryImport`. The activity wait handle is disposed. Theme names and tool-caption labels each live in one place. Element regexes are reused. The unused picker delete flag and the store identity-map dictionaries go away.
- An `.editorconfig` records the layout the code already uses. Analyzer policy stays at the SDK default. This change does not turn warnings into errors and does not reformat the tree.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `session-export`: Image embedding is limited to four image media types with base64 payloads and encoded data URIs. Reserved Windows device names cannot become export file names. Impossible turn timestamps, unexpected JSON part shapes, and unbounded blob lengths no longer abort the export. Markdown title, workspace, and timestamp stay on one line each.

## Impact

- `src/AiChatExport/Render.cs`, `Transcript.cs`, `Picker.cs`, `Export.cs`, and `Store.cs` change parsing, naming, and HTML/Markdown emission.
- `src/AiChatExport/AiChatExport.csproj` exposes internals to `AiChatExport.Tests`. The test project needs no new package.
- Tests that call implementation types, construct `TerminalWriter`, or cover stems, images, and timestamps follow the new visibility and the new edge cases.
- Help text, theme names, the default transcript, and the read-only chat store stay as they are. No new NuGet dependency. `System.CommandLine` is not introduced.
