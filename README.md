# ai-chat-export

Export a local Cursor agent chat to an HTML page. The page follows the chat: the prompt you typed, then the assistant's visible text. The user section is labeled User. Consecutive assistant replies share one Agent heading until the next prompt. The HTML page lists user turns, and each entry jumps to that turn. HTML is the default. HTML uses terminal-dark unless you name terminal-light, dark-web, or light-web. `--markdown` writes the Markdown file instead of the HTML page.

`--detail` adds the conversation summary and Cursor command blocks after the query. `--tools` adds one caption per tool call. `--verbose` adds those captions plus system text, injected context, reasoning, and raw tool output.

Build the console app and run it with `dotnet run`.

```bash
dotnet run --project src/AiChatExport -- --help
```

The list shows the title, workspace, and time in aligned columns. Up and Down move. Left and Right switch All chats and This workspace. Enter asks for a file name. The chat list stays on screen. Esc closes. `i` shows the selected chat id.

```bash
dotnet run --project src/AiChatExport -- --id <chat-id> --out ./exports
dotnet run --project src/AiChatExport -- --id <chat-id> --markdown
dotnet run --project src/AiChatExport -- --id <chat-id> --name ./exports/session.html
```

`--name` writes that file path instead of a title-named file. `--name` requires `--id`. Passing `--name` with `--out` is an error. The tool only reads the chat store.
