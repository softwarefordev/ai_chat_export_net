# Proposal

## Why

The Python exporter now lists GitHub Copilot CLI sessions beside Cursor agent chats and IDE transcripts. This port stops at Cursor, so those sessions never appear in the picker or in an HTML or Markdown export.

## What Changes

- Add a source registry row after Cursor IDE: stored id `cp-cli`, `--source copilot-cli`, filter label `Copilot-CLI`, column `Cp-CLI`. The source column stays six characters wide.
- Read `<copilot-root>/session-store.db` into the combined newest-first list. The root defaults to `~/.copilot`. `--copilot-root` replaces that default. A missing root adds no Copilot rows and does not fail the command.
- Keep a Copilot row when its id also exists as a Cursor agent chat or IDE transcript. Title from the summary, then the first user line, then the id. Time from the session's updated timestamp, shown in the catalog's local time form. An unparsed time is shown collapsed as stored. Workspace is the last segment of the working directory.
- Export turn text by default from `session-state/<id>/events.jsonl` when that file can be read, otherwise from the turn table. `--tools` adds captions and omits tool results. `--verbose` adds system text, reasoning, and tool results. Encrypted content and checkpoint indexes stay unread.
- Extend the source line and the `s` cycle with Copilot-CLI. Passing only the two Cursor `--source` values is a subset, so the line marks those two and the next `s` returns to All sources.

## Capabilities

### New Capabilities

### Modified Capabilities

- `session-export`: The catalog, picker, and export include Copilot CLI sessions, and the source filter names that source.

## Impact

- Catalog merge, a Copilot reader, the source registry, picker cycle, caption arms for `Grep` and `Create`, and `--copilot-root`. README, help, and tests change.
- HTML and Markdown stay on the existing path. The source cell stays off the written page.
- VS Code Copilot Chat and the GitHub Copilot workflow files already in this repo stay out of this change.
