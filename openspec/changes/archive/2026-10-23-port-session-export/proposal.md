# Proposal

## Why

The Cursor chat exporter exists as a Python CLI. This repo should offer the same tool as a .NET console app, so a session can be exported without a Python install. The running Python CLI is the behavior to match.

## What Changes

- Add a `net10.0` console app that lists local Cursor agent chats and exports one chat to HTML, or to Markdown when `--markdown` is passed.
- Match the running Python CLI: flags, picker keys, file names, transcript filters, and the self-contained HTML page.
- Present the tool as `ai-chat-export` in help text and the README. Building the project produces a console executable, and `dotnet run` on the project runs the same tool.
- Read the local chat store only. Do not modify chats or call Cursor.
- Leave a global `dotnet tool` install, and any edit to the Python project, out of this change.

The Python durable spec still names `ai-session-exporter` and still writes HTML alongside Markdown. Those lines lag the program. This capability follows the program: the command is `ai-chat-export`, and `--markdown` writes Markdown only.

## Capabilities

### New Capabilities

- `session-export`: Export a local Cursor agent chat to an HTML page or a Markdown file, from a chat id or from an interactive picker.

### Modified Capabilities

## Impact

- New console project and test project in this repo. The repo currently has OpenSpec scaffolding and no application code.
- One package dependency, `Microsoft.Data.Sqlite`, to read `store.db`. Tests use xUnit.
- Reads `~/.cursor/chats` (or `--chats-root`) and writes an HTML or Markdown file in the working directory, an `--out` directory, or a `--name` path.
- The Python CLI at `C:\src\cursor_pretty_export` is the reference and the test oracle. This change does not edit it.
