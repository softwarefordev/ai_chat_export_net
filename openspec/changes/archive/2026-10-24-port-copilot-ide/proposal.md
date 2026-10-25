# Proposal

## Why

The Python source at `e69814f` (Add Copilot IDE support) lists VS Code, Rider, and Visual Studio Copilot chats beside the sessions this port already exports. Those chats should use the same picker, the same HTML or Markdown page, and the same source filter, and the README should name them.

## What Changes

- Add one registry row for every Copilot IDE host: stored id `cp-ide`, `--source copilot-ide`, filter label `Copilot-IDE`, column `Cp-IDE`. VS Code, Rider, and Visual Studio share that cell. The source line and the `s` cycle gain Copilot-IDE after Copilot-CLI.
- Read VS Code version-3 chat sessions, Rider sessions under the local `github-copilot/rd/chat-agent-sessions` store, and Visual Studio sessions under `github-copilot/vs` when that directory exists. `--vscode-root` and `--copilot-ide-root` replace the platform defaults. A missing root adds no rows and does not fail.
- A current VS Code or Rider chat may keep its transcript in a Copilot event log. A VS Code agent-host link and a Rider conversation id claim that log as one `Cp-IDE` row. An event log that no IDE row claims, and that the Copilot CLI database does not list, still joins the catalog as `Cp-CLI` when it has a user or assistant message.
- Leave a Copilot IDE session out of the picker when it has no user message and no assistant text. Passing that id still reports `Chat has no transcript: {id}` and writes no file. Keep a Copilot IDE row when its id also exists as a Cursor or Copilot CLI session.
- Export the turn text through the existing HTML or Markdown path. The written page omits the source cell. `--tools` adds captions and omits tool results and command text. `--verbose` adds the tool result and still omits the command text.
- Update the README and the command help so they name Cursor and Copilot, including `Copilot-IDE`, `Cp-IDE`, and `copilot-ide`. Keep the sentences the current README tests already require.

The current `session-export` requirement "Copilot CLI sessions in the catalog" says a session-state directory with no database row stays out of the list. The running Python program now lists that directory when its event log has a user or assistant message and the id is not already a catalog row. A directory with no such log still stays out. This change updates that requirement to the running behavior. The Python main spec left both sentences in place.

## Capabilities

### New Capabilities

### Modified Capabilities

- `session-export`: The catalog, picker, export, README, and command help include Copilot IDE sessions from VS Code, Rider, and Visual Studio under one `Cp-IDE` cell, and an unlisted Copilot event log joins the list as `Cp-CLI`.

## Impact

- Catalog merge, a Copilot IDE reader, the source registry, picker cycle, `--vscode-root`, and `--copilot-ide-root`. README, help, and tests change. The reader follows VS Code agent-host links and Rider conversation ids into Copilot event logs, and lists any remaining unlisted event log as Copilot CLI.
- HTML and Markdown stay on the existing path. The source cell stays off the written page. The Nitrite reader scans file bytes and does not add a Java runtime or a Nitrite package.
- Out of scope: the Python package description and `.github/copilot-instructions.md`, which have no counterpart in this console app; a separate filter or column per IDE host; JetBrains AI Assistant chats; Copilot background-agent databases; the Python repository.
