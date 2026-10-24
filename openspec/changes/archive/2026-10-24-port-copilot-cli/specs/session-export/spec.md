# Spec Delta

## MODIFIED Requirements

### Requirement: Combined session list
The picker SHALL show agent chats, IDE transcripts, and Copilot CLI sessions in one list, newest first.

#### Scenario: Newer transcript above an older chat
- **WHEN** an IDE transcript's file time is later than an agent chat's updated time
- **THEN** the IDE row appears above that agent chat

#### Scenario: Newer Copilot session above an older chat
- **WHEN** a Copilot CLI session's updated time is later than an agent chat's updated time
- **THEN** the Copilot row appears above that agent chat

### Requirement: Source filter line
The picker SHALL show a source line under the workspace scope. The line SHALL name All sources, Cursor-CLI, Cursor-IDE, and Copilot-CLI. It SHALL mark All sources when every source is allowed, and SHALL mark each allowed source when the filter is a subset.

#### Scenario: Open on every source
- **WHEN** the picker opens without `--source`
- **THEN** the source line marks All sources

#### Scenario: One source is marked
- **WHEN** the active filter allows only agent chats
- **THEN** the source line marks Cursor-CLI and does not mark All sources

#### Scenario: Copilot CLI is marked
- **WHEN** the active filter allows only Copilot CLI sessions
- **THEN** the source line marks Copilot-CLI and does not mark All sources

### Requirement: Source filter key
Pressing `s` on the chat list, in either case, SHALL cycle the filter through All sources, Cursor-CLI, Cursor-IDE, and Copilot-CLI, then back to All sources. The selection SHALL return to the first visible row. The footer SHALL name `s`.

#### Scenario: Cycle to Cursor-CLI
- **WHEN** All sources is active and the user presses `s`
- **THEN** Cursor-CLI is active and the first visible row is selected

#### Scenario: Cycle to Copilot-CLI
- **WHEN** Cursor-IDE is active and the user presses `s`
- **THEN** Copilot-CLI is active and the first visible row is selected

#### Scenario: Cycle back to All sources
- **WHEN** Copilot-CLI is active and the user presses `s`
- **THEN** All sources is active

### Requirement: Source argument
`--source` SHALL accept `cursor-cli`, `cursor-ide`, and `copilot-cli`. Repeating `--source` SHALL allow each named source. Omitting `--source` SHALL allow every source. The picker SHALL open on that filter.

#### Scenario: Cursor CLI flag
- **WHEN** the user passes `--source cursor-cli`
- **THEN** the picker opens with Cursor-CLI active

#### Scenario: Both source flags
- **WHEN** the user passes `--source cursor-cli` and `--source cursor-ide`
- **THEN** the list includes agent chats and IDE transcripts and omits Copilot CLI sessions

#### Scenario: Flag still cycles
- **WHEN** the picker opened with `--source cursor-cli` and the user presses `s`
- **THEN** the filter moves to Cursor-IDE

#### Scenario: Copilot CLI flag
- **WHEN** the user passes `--source copilot-cli`
- **THEN** the picker opens with Copilot-CLI active

#### Scenario: Partial flags return to All sources
- **WHEN** the picker opened with `--source cursor-cli` and `--source cursor-ide` and the user presses `s`
- **THEN** the filter moves to All sources

## ADDED Requirements

### Requirement: Copilot source cell
A Copilot CLI row's source cell SHALL be `Cp-CLI` in the six-character source column.

#### Scenario: Copilot source cell
- **WHEN** the page shows a Copilot CLI session
- **THEN** that row's source cell is `Cp-CLI`

### Requirement: Copilot CLI sessions in the catalog
The catalog SHALL include each session recorded in `<copilot-root>/session-store.db`. The copilot root SHALL default to `~/.copilot`. `--copilot-root` SHALL replace that default. A missing copilot root SHALL add no Copilot rows and SHALL NOT fail the command. A session directory that is absent from that database SHALL stay out of the list.

#### Scenario: Session beside an agent chat
- **WHEN** the copilot root has one session and the chat catalog has a different chat
- **THEN** the list contains both

#### Scenario: Missing copilot root
- **WHEN** the copilot root does not exist
- **THEN** the list still contains the agent chats and the command does not fail because that root is missing

#### Scenario: Custom copilot root
- **WHEN** the user passes `--copilot-root`
- **THEN** Copilot rows come from that directory

#### Scenario: Directory without a database session
- **WHEN** a session-state directory has no row in the session database
- **THEN** the list omits that directory

### Requirement: Copilot session listed beside a Cursor id
An id that exists as a Copilot CLI session and as a Cursor agent chat or IDE transcript SHALL appear as both rows.

#### Scenario: Same id in Copilot and the chat store
- **WHEN** the same id is a Copilot CLI session and an agent chat
- **THEN** the list has both rows and the Copilot row's source cell is `Cp-CLI`

### Requirement: Copilot title
A Copilot row's title SHALL be the session summary. An empty summary SHALL use the first line of the first user message. When that line is empty, the title SHALL be the session id.

#### Scenario: Summary title
- **WHEN** the session summary is non-empty
- **THEN** the row title is that summary

#### Scenario: Empty summary
- **WHEN** the summary is empty and the first user message's first line is non-empty
- **THEN** the row title is that line

#### Scenario: No title text
- **WHEN** the summary and the user messages are empty
- **THEN** the row title is the session id

### Requirement: Copilot list time
A Copilot row's time SHALL be the session's updated time, interpreted as UTC and shown in the catalog's local time form. An unparsed updated time SHALL be shown as stored, with whitespace collapsed.

#### Scenario: Updated time
- **WHEN** a Copilot session has an updated time
- **THEN** the row shows that time in the catalog's local time form

#### Scenario: Unparsed updated time
- **WHEN** a Copilot session's updated time is not a timestamp
- **THEN** the row shows that value with whitespace collapsed

### Requirement: Copilot workspace
The workspace column SHALL show the last segment of the session's working directory. When that directory is missing, the column SHALL be empty and This workspace SHALL omit the row.

#### Scenario: Working directory
- **WHEN** the session has a working directory and the CLI is launched from that path
- **THEN** the row shows the last segment of that path and This workspace includes the row

#### Scenario: Missing working directory
- **WHEN** the session has no working directory
- **THEN** the workspace column is empty and This workspace omits the row

### Requirement: Export a Copilot session
Passing the id of a Copilot CLI session SHALL write that session through the same HTML or Markdown export as an agent chat. The written page SHALL omit the source cell.

#### Scenario: HTML export
- **WHEN** the user passes a Copilot session id
- **THEN** the export contains the user message and the assistant text and omits the source cell

### Requirement: Copilot turn text
The default Copilot transcript SHALL show each turn's user message and assistant text. Consecutive assistant text SHALL share one Agent heading until the next user message. An empty assistant reply SHALL add no Agent heading.

#### Scenario: User and assistant
- **WHEN** a turn has a user message and assistant text
- **THEN** the export shows that user message and that assistant text under one Agent heading

#### Scenario: Empty assistant
- **WHEN** a turn has a user message and no assistant text
- **THEN** the export shows the user message and no Agent heading for that turn

### Requirement: Copilot session with no turns
A Copilot session with no user message and no assistant text SHALL be reported with `Chat has no transcript: {id}`, and the CLI SHALL write no file.

#### Scenario: Empty session
- **WHEN** the user passes the id of a Copilot session that has no turns
- **THEN** the CLI reports that the chat has no transcript and writes no file

### Requirement: Copilot tool caption
With `--tools`, a Copilot tool call SHALL show one caption and SHALL omit the tool result.

#### Scenario: Read a file
- **WHEN** the user passes `--tools` and the tool is `view` with a path
- **THEN** the caption is `Read` plus that path and the result is omitted

#### Scenario: Find files
- **WHEN** the user passes `--tools` and the tool is `glob` with a pattern
- **THEN** the caption is `Glob` plus that pattern and the result is omitted

#### Scenario: Run a command
- **WHEN** the user passes `--tools` and the tool is `powershell` with a description
- **THEN** the caption is `Ran` plus that description and the command text is omitted

#### Scenario: Search files
- **WHEN** the user passes `--tools` and the tool is `grep` with a pattern
- **THEN** the caption is `Grep` plus that pattern and the result is omitted

#### Scenario: Create a file
- **WHEN** the user passes `--tools` and the tool is `create` with a path
- **THEN** the caption is `Create` plus that path and the result is omitted

### Requirement: Copilot verbose detail
`--verbose` SHALL include Copilot tool captions, system message text, reasoning text, and tool results.

#### Scenario: Reasoning and result
- **WHEN** the user passes `--verbose` and a turn has reasoning text and a tool result
- **THEN** the export includes that reasoning text and that tool result

#### Scenario: System message
- **WHEN** the user passes `--verbose` and the session has a system message
- **THEN** the export includes that system message text

### Requirement: Copilot encrypted content
Encrypted Copilot content SHALL stay out of the export, including when `--verbose` is set.

#### Scenario: Encrypted assistant content
- **WHEN** an assistant message carries encrypted content
- **THEN** the export omits that encrypted content

### Requirement: Copilot turns without an event log
When a Copilot session has turn text and no event log, the export SHALL write that turn text, and `--tools` SHALL add no captions.

#### Scenario: Missing event log
- **WHEN** the session has turns and no event log
- **THEN** the export contains the turn text and no tool caption

### Requirement: Unreadable Copilot events
An unreadable Copilot event log SHALL warn `Unreadable Copilot events {id}: {error}`, and the export SHALL still write the turn text.

#### Scenario: Event log cannot be read
- **WHEN** the event log cannot be read and the session has turn text
- **THEN** the warning names the session id and the export still contains the turn text

### Requirement: Copilot checkpoint ignored
A Copilot checkpoint index SHALL NOT become a turn and SHALL NOT add a footer line.

#### Scenario: Index file
- **WHEN** the session directory contains a checkpoint index
- **THEN** the export omits that index

### Requirement: Read-only Copilot store
The tool SHALL open the Copilot database read-only and SHALL leave the database, the event log, and the checkpoint index byte-for-byte unchanged.

#### Scenario: Bytes unchanged
- **WHEN** a Copilot session is exported
- **THEN** the database and the event log have the same bytes as before the export

### Requirement: Unreadable Copilot store
An unreadable Copilot database SHALL be omitted with the warning `Unreadable Copilot store: {error}`, and the command SHALL still list the other sessions.

#### Scenario: Database cannot be read
- **WHEN** the session database cannot be read
- **THEN** the warning names the failure and the agent chats remain in the list
