# Spec Delta

## MODIFIED Requirements

### Requirement: Combined session list
The picker SHALL show agent chats, IDE transcripts, Copilot CLI sessions, and Copilot IDE sessions in one list, newest first.

#### Scenario: Newer transcript above an older chat
- **WHEN** an IDE transcript's file time is later than an agent chat's updated time
- **THEN** the IDE row appears above that agent chat

#### Scenario: Newer Copilot session above an older chat
- **WHEN** a Copilot CLI session's updated time is later than an agent chat's updated time
- **THEN** the Copilot row appears above that agent chat

#### Scenario: Newer Copilot IDE session above an older chat
- **WHEN** a Copilot IDE session's updated time is later than an agent chat's updated time
- **THEN** the Copilot IDE row appears above that agent chat

### Requirement: Source filter line
The picker SHALL show a source line under the workspace scope. The line SHALL name All sources, Cursor-CLI, Cursor-IDE, Copilot-CLI, and Copilot-IDE. It SHALL mark All sources when every source is allowed, and SHALL mark each allowed source when the filter is a subset.

#### Scenario: Open on every source
- **WHEN** the picker opens without `--source`
- **THEN** the source line marks All sources

#### Scenario: One source is marked
- **WHEN** the active filter allows only agent chats
- **THEN** the source line marks Cursor-CLI and does not mark All sources

#### Scenario: Copilot CLI is marked
- **WHEN** the active filter allows only Copilot CLI sessions
- **THEN** the source line marks Copilot-CLI and does not mark All sources

#### Scenario: Copilot IDE is marked
- **WHEN** the active filter allows only Copilot IDE sessions
- **THEN** the source line marks Copilot-IDE and does not mark All sources

### Requirement: Source filter key
Pressing `s` on the chat list, in either case, SHALL cycle the filter through All sources, Cursor-CLI, Cursor-IDE, Copilot-CLI, and Copilot-IDE, then back to All sources. The selection SHALL return to the first visible row. The footer SHALL name `s`.

#### Scenario: Cycle to Cursor-CLI
- **WHEN** All sources is active and the user presses `s`
- **THEN** Cursor-CLI is active and the first visible row is selected

#### Scenario: Cycle to Copilot-CLI
- **WHEN** Cursor-IDE is active and the user presses `s`
- **THEN** Copilot-CLI is active and the first visible row is selected

#### Scenario: Cycle to Copilot-IDE
- **WHEN** Copilot-CLI is active and the user presses `s`
- **THEN** Copilot-IDE is active and the first visible row is selected

#### Scenario: Cycle back to All sources
- **WHEN** Copilot-IDE is active and the user presses `s`
- **THEN** All sources is active

### Requirement: Source argument
`--source` SHALL accept `cursor-cli`, `cursor-ide`, `copilot-cli`, and `copilot-ide`. Repeating `--source` SHALL allow each named source. Omitting `--source` SHALL allow every source. The picker SHALL open on that filter.

#### Scenario: Cursor CLI flag
- **WHEN** the user passes `--source cursor-cli`
- **THEN** the picker opens with Cursor-CLI active

#### Scenario: Both source flags
- **WHEN** the user passes `--source cursor-cli` and `--source cursor-ide`
- **THEN** the list includes agent chats and IDE transcripts and omits Copilot CLI sessions and Copilot IDE sessions

#### Scenario: Flag still cycles
- **WHEN** the picker opened with `--source cursor-cli` and the user presses `s`
- **THEN** the filter moves to Cursor-IDE

#### Scenario: Copilot CLI flag
- **WHEN** the user passes `--source copilot-cli`
- **THEN** the picker opens with Copilot-CLI active

#### Scenario: Copilot IDE flag
- **WHEN** the user passes `--source copilot-ide`
- **THEN** the picker opens with Copilot-IDE active

#### Scenario: Partial flags return to All sources
- **WHEN** the picker opened with `--source cursor-cli` and `--source cursor-ide` and the user presses `s`
- **THEN** the filter moves to All sources

### Requirement: Copilot CLI sessions in the catalog
The catalog SHALL include each session recorded in `<copilot-root>/session-store.db`. The copilot root SHALL default to `~/.copilot`. `--copilot-root` SHALL replace that default. A missing copilot root SHALL add no Copilot rows and SHALL NOT fail the command. A session-state directory that has no event log with a user or assistant message SHALL stay out of the list.

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
- **WHEN** a session-state directory has no row in the session database and no event log with a user or assistant message
- **THEN** the list omits that directory

## ADDED Requirements

### Requirement: Copilot IDE source cell
A Copilot IDE row's source cell SHALL be `Cp-IDE` in the six-character source column for a VS Code session, a Rider session, and a Visual Studio session.

#### Scenario: Copilot IDE source cell
- **WHEN** the page shows a Copilot IDE session from any of those hosts
- **THEN** that row's source cell is `Cp-IDE`

### Requirement: VS Code Copilot sessions in the catalog
The catalog SHALL include each Copilot chat session under the VS Code user directory's workspace session folders and empty-window session folder when that session has a user or assistant turn. The directory SHALL default to the platform Visual Studio Code User directory. `--vscode-root` SHALL replace that default. A missing directory SHALL add no rows and SHALL NOT fail the command.

#### Scenario: Session beside an agent chat
- **WHEN** the VS Code root has one Copilot session with a user turn and the chat catalog has a different chat
- **THEN** the list contains both

#### Scenario: Missing VS Code root
- **WHEN** the VS Code root does not exist
- **THEN** the list still contains the agent chats and the command does not fail because that root is missing

#### Scenario: Custom VS Code root
- **WHEN** the user passes `--vscode-root`
- **THEN** VS Code Copilot rows come from that directory

#### Scenario: JSONL snapshot
- **WHEN** a session file is JSONL and its first snapshot names a title
- **THEN** the row uses that snapshot and ignores later lines

### Requirement: Rider Copilot sessions in the catalog
The catalog SHALL include each Copilot chat session under `<copilot-ide-root>/rd/chat-agent-sessions` when that session has a user or assistant turn. The root SHALL default to the platform local `github-copilot` directory. `--copilot-ide-root` SHALL replace that default. A missing root SHALL add no rows and SHALL NOT fail the command.

#### Scenario: Rider session beside an agent chat
- **WHEN** the Copilot IDE root has one Rider session with a user turn and the chat catalog has a different chat
- **THEN** the list contains both

#### Scenario: Missing Copilot IDE root
- **WHEN** the Copilot IDE root does not exist
- **THEN** the list still contains the agent chats and the command does not fail because that root is missing

#### Scenario: Custom Copilot IDE root
- **WHEN** the user passes `--copilot-ide-root`
- **THEN** Rider rows come from that directory

### Requirement: Visual Studio Copilot sessions in the catalog
The catalog SHALL include each Copilot chat session under `<copilot-ide-root>/vs` when that session has a user or assistant turn. A missing `vs` directory SHALL add no Visual Studio rows and SHALL NOT fail the command.

#### Scenario: Visual Studio session beside an agent chat
- **WHEN** the Copilot IDE root has one Visual Studio session with a user turn
- **THEN** the list contains that session

#### Scenario: Missing Visual Studio directory
- **WHEN** the Copilot IDE root has no `vs` directory
- **THEN** the list still contains the other sessions and the command does not fail because that directory is missing

### Requirement: Empty Copilot IDE draft omitted
A Copilot IDE session with no user message and no assistant text SHALL stay out of the list.

#### Scenario: Empty draft
- **WHEN** a Copilot IDE session file has no user message and no assistant text
- **THEN** the list omits that session

### Requirement: Copilot IDE background agent omitted
A Copilot background-agent database SHALL stay out of the list.

#### Scenario: Background agent database
- **WHEN** the Copilot IDE root contains a background-agent database and a chat session
- **THEN** the list includes the chat session and omits the background-agent database

### Requirement: Copilot IDE session listed beside another id
An id that exists as a Copilot IDE session and as a Cursor session or a Copilot CLI session SHALL appear as both rows. The Copilot IDE row's source cell SHALL be `Cp-IDE`.

#### Scenario: Same id in Copilot IDE and the chat store
- **WHEN** the same id is a Copilot IDE session and an agent chat
- **THEN** the list has both rows and the Copilot IDE row's source cell is `Cp-IDE`

### Requirement: Copilot IDE title
A Copilot IDE row's title SHALL be the session title when that title has text. An empty title SHALL use the first line of the first user message. When that line is empty, the title SHALL be the session id.

#### Scenario: Session title
- **WHEN** the session title is non-empty
- **THEN** the row title is that title

#### Scenario: First user line
- **WHEN** the session title is empty and the first user message's first line is non-empty
- **THEN** the row title is that line

#### Scenario: No title text
- **WHEN** the session title and the user messages are empty
- **THEN** the row title is the session id

### Requirement: Copilot IDE list time
A Copilot IDE row's time SHALL be the session's latest timestamp, shown in the catalog's local time form. When the latest timestamp is missing, the row SHALL use the creation timestamp.

#### Scenario: Latest time
- **WHEN** a Copilot IDE session has a latest timestamp
- **THEN** the row shows that time in the catalog's local time form

#### Scenario: Creation time
- **WHEN** a Copilot IDE session has a creation timestamp and no latest timestamp
- **THEN** the row shows that creation time in the catalog's local time form

### Requirement: Copilot IDE workspace
The workspace column SHALL show the last segment of the session's workspace path. When that path is missing, the column SHALL be empty and This workspace SHALL omit the row.

#### Scenario: Workspace path
- **WHEN** the session has a workspace path and the CLI is launched from that path
- **THEN** the row shows the last segment of that path and This workspace includes the row

#### Scenario: Missing workspace path
- **WHEN** the session has no workspace path
- **THEN** the workspace column is empty and This workspace omits the row

### Requirement: Export a Copilot IDE session
Passing the id of a Copilot IDE session SHALL write that session through the same HTML or Markdown export as an agent chat. The written page SHALL omit the source cell.

#### Scenario: HTML export
- **WHEN** the user passes a Copilot IDE session id
- **THEN** the export contains the user message and the assistant text and omits the source cell

### Requirement: Copilot IDE turn text
The default Copilot IDE transcript SHALL show each user message and the assistant's visible text. Consecutive assistant text SHALL share one Agent heading until the next user message. An empty assistant reply SHALL add no Agent heading.

#### Scenario: User and assistant
- **WHEN** a turn has a user message and assistant text
- **THEN** the export shows that user message and that assistant text under one Agent heading

#### Scenario: Empty assistant
- **WHEN** a turn has a user message and no assistant text
- **THEN** the export shows the user message and no Agent heading for that turn

### Requirement: Copilot IDE session with no turns
A Copilot IDE session with no user message and no assistant text SHALL be reported with `Chat has no transcript: {id}`, and the CLI SHALL write no file.

#### Scenario: Empty session id
- **WHEN** the user passes the id of a Copilot IDE session that has no turns
- **THEN** the CLI reports that the chat has no transcript and writes no file

### Requirement: Copilot IDE tool caption
With `--tools`, a Copilot IDE tool call SHALL show one caption and SHALL omit the tool result and the command text. A tool id outside the named captions SHALL be shown as that id, without its argument values.

#### Scenario: Read a file
- **WHEN** the user passes `--tools` and the tool reads a file
- **THEN** the caption is `Read` plus that path and the result is omitted

#### Scenario: Create a file
- **WHEN** the user passes `--tools` and the tool creates a file
- **THEN** the caption is `Create` plus that path and the result is omitted

#### Scenario: Find files
- **WHEN** the user passes `--tools` and the tool searches for files by a pattern
- **THEN** the caption is `Glob` plus that pattern and the result is omitted

#### Scenario: Run a command
- **WHEN** the user passes `--tools` and the tool runs a command with a description
- **THEN** the caption is `Ran` plus that description and the command text is omitted

#### Scenario: Edit a file
- **WHEN** the user passes `--tools` and the tool edits a file
- **THEN** the caption is `Edit` plus that path and the result is omitted

#### Scenario: Other tool
- **WHEN** the user passes `--tools` and the tool id is not one of those captions
- **THEN** the caption is that tool id and the argument values are omitted

### Requirement: Copilot IDE verbose detail
`--verbose` SHALL include Copilot IDE tool captions and tool result text, and SHALL omit command text.

#### Scenario: Tool result
- **WHEN** the user passes `--verbose` and a turn has a tool result
- **THEN** the export includes that tool result and omits the command text

### Requirement: Copilot IDE response parts omitted
The export SHALL omit Copilot IDE response parts other than visible assistant text and tool calls, including avatar addresses and icon addresses.

#### Scenario: Skipped part
- **WHEN** a response part is a text edit, an inline reference, a code block address, or a progress marker
- **THEN** the export omits that part

#### Scenario: Avatar address
- **WHEN** a Copilot IDE session carries an avatar address
- **THEN** the export omits that address

### Requirement: Read-only Copilot IDE store
The tool SHALL leave Copilot IDE session files byte-for-byte unchanged.

#### Scenario: Bytes unchanged
- **WHEN** a Copilot IDE session is exported
- **THEN** the session file has the same bytes as before the export

### Requirement: Unreadable Copilot IDE session
An unreadable Copilot IDE session SHALL be omitted with the warning `Unreadable Copilot IDE session {id}: {error}`, and the command SHALL still list the other sessions. An unreadable agent-host database SHALL warn `Unreadable Copilot IDE session agent-host: {error}`.

#### Scenario: Session cannot be read
- **WHEN** a Copilot IDE session file cannot be read
- **THEN** the warning names that session and the other sessions remain in the list

#### Scenario: Agent-host database cannot be read
- **WHEN** the agent-host database cannot be read
- **THEN** the warning names the agent-host failure and the other sessions remain in the list

### Requirement: VS Code agent-host session
A VS Code agent-host chat whose linked Copilot event log has a user or assistant message SHALL appear as one `Cp-IDE` row. A tombstoned agent-host chat SHALL stay out of the list. A missing agent-host database SHALL add no rows and SHALL NOT fail the command.

#### Scenario: Linked event log
- **WHEN** the VS Code root's agent-host database names a Copilot event log that has a user message and assistant text
- **THEN** the list contains one `Cp-IDE` row for that log, titled with the agent-host summary when that summary has text

#### Scenario: Tombstoned chat
- **WHEN** an agent-host chat is tombstoned
- **THEN** the list omits that chat

#### Scenario: Missing agent-host database
- **WHEN** the VS Code root has no agent-host database
- **THEN** the command does not fail because that database is missing

### Requirement: Rider event-log transcript
A Rider session whose conversation id names a Copilot event log with a user or assistant message SHALL appear once, as `Cp-IDE`. Export of that id SHALL show the log's user and assistant text. The Nitrite file SHALL stay byte-for-byte unchanged.

#### Scenario: Conversation id joins once
- **WHEN** a Rider database names a conversation id and that id's event log has a user message
- **THEN** the list contains one `Cp-IDE` row for that id and does not also list it as `Cp-CLI`

#### Scenario: Export uses the event log
- **WHEN** the user exports that id
- **THEN** the export contains the event log's user message and assistant text and the Nitrite file bytes are unchanged

### Requirement: Unlisted Copilot event log
A Copilot event log that has a user or assistant message, and that is not already a catalog row, SHALL appear as one `Cp-CLI` row.

#### Scenario: Log without a database row
- **WHEN** the Copilot root has an event log with a user message and the session database has no row for that id
- **THEN** the list contains one `Cp-CLI` row for that log

### Requirement: Product description names Cursor and Copilot
The README and the command help SHALL name both Cursor and Copilot.

#### Scenario: README names both products
- **WHEN** the README is read
- **THEN** it names Cursor and Copilot, and it names `Copilot-IDE`, `Cp-IDE`, and `copilot-ide`

#### Scenario: README keeps the export sentences
- **WHEN** the README is read
- **THEN** it contains `Enter asks for a file name.` and `HTML is the default.`

#### Scenario: Command help
- **WHEN** the user passes `--help`
- **THEN** the help text contains `usage: ai-chat-export` and `copilot-ide`
