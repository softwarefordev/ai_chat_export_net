# Proposal

## Why

The Python `ai-chat-export` on `main` (https://github.com/softwarefordev/ai_chat_export) can filter a long HTML transcript from the navigator, group each turn time with the prompt under it, and write a console-styled page. This solution still exports the older web page: two themes named `dark` and `light`, no turn filter, and turn times that sit closer to the row above than to their own prompt.

## What Changes

- **BREAKING:** `--theme` accepts `terminal-dark`, `terminal-light`, `dark-web`, and `light-web`. The names `dark` and `light` are rejected, and the default becomes `terminal-dark`.
- `terminal-dark` and `terminal-light` are phosphor pages with a monospace face, square sections, and an amber User edge. `dark-web` and `light-web` keep today's dark and light palettes, the serif transcript, the sans navigator, rounded sections, and the gold User edge.
- The picker footer shows the theme name that will be written, so a default open shows `terminal-dark`.
- Each HTML navigator entry gains an unchecked checkbox beside its fragment link. Checking one or more boxes shows those User sections and the Agent sections that follow each of them until the next User section, and hides the other transcript sections, including System and Context. The chat title, the footer, and every navigator entry stay visible.
- Clear filter is pinned above the turn list and repeated after the last turn. Both controls appear only while a box is checked, and either one shows every section again. The page still has no script and no external address.
- The space between a navigator turn time and the prompt under it becomes smaller than the space between entries. The first entry keeps no added space above it. Markdown is unchanged, and `--markdown` still writes Markdown only.

Assumption: match the running Python program, not its durable spec where that spec still says a module entry point or that `--markdown` also writes HTML.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `session-export`: The HTML theme set is `terminal-dark`, `terminal-light`, `dark-web`, and `light-web`, with `terminal-dark` as the default. The navigator can filter the transcript to selected User turns and the Agent sections that follow them, and a turn time groups with the prompt below it.

## Impact

- `src/AiChatExport/Render.cs` gains the four palettes, the filter markup and style rules, and the navigator spacing.
- `src/AiChatExport/Export.cs` changes the `--theme` choices, the default, and the help text.
- `README.md` names the four themes and the new default.
- `tests/AiChatExport.Tests/RenderTests.cs`, `PickerTests.cs`, and any export test that passes `dark` or `light` follow the new names.
- The console app, one-file Markdown export, and the chat store stay as they are. The Python repository is not edited.
