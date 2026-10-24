# ai-chat-export

Export a local Cursor agent chat to an HTML page. The page follows the chat: the prompt you typed, then the assistant's visible text. The user section is labeled User. Consecutive assistant replies share one Agent heading until the next prompt. The HTML page lists user turns, and each entry jumps to that turn. HTML is the default. HTML uses terminal-dark unless you name terminal-light, dark-web, or light-web. `--markdown` writes the Markdown file instead of the HTML page.

`--detail` adds the conversation summary and Cursor command blocks after the query. `--tools` adds one caption per tool call. `--verbose` adds those captions plus system text, injected context, reasoning, and raw tool output.

Build the console app and run it with `dotnet run`.

```bash
dotnet run --project src/AiChatExport -- --help
```

The list shows Cursor agent chats from `~/.cursor/chats`, Cursor IDE transcripts from `~/.cursor/projects`, and Copilot CLI sessions from `~/.copilot`. Columns are the title, workspace, source, and time. The source column is `Cu-CLI` for an agent chat, `Cu-IDE` for an IDE transcript, and `Cp-CLI` for a Copilot CLI session. Up and Down move. Left and Right switch All chats and This workspace. `s` switches All sources, Cursor-CLI, Cursor-IDE, and Copilot-CLI. Enter asks for a file name. The chat list stays on screen. Esc closes. `i` shows the selected chat id.

`--source cursor-cli`, `--source cursor-ide`, or `--source copilot-cli` limits the list to one store. Repeat `--source` to include more. `--projects-root` selects the IDE tree. `--copilot-root` selects the Copilot CLI tree.

```bash
dotnet run --project src/AiChatExport -- --id <chat-id> --out ./exports
dotnet run --project src/AiChatExport -- --id <chat-id> --markdown
dotnet run --project src/AiChatExport -- --id <chat-id> --name ./exports/session.html
```

`--name` writes that file path instead of a title-named file. `--name` requires `--id`. Passing `--name` with `--out` is an error. The tool only reads the chat store, the IDE transcript, and the Copilot CLI store.
