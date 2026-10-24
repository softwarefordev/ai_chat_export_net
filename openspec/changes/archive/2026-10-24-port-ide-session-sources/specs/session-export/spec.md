# Spec Delta

## ADDED Requirements

### Requirement: Combined session list
The picker SHALL show agent chats and IDE transcripts in one list, newest first.

#### Scenario: Newer transcript above an older chat
- **WHEN** an IDE transcript's file time is later than an agent chat's updated time
- **THEN** the IDE row appears above that agent chat

### Requirement: IDE transcripts in the catalog
The catalog SHALL include each file at `<projects-root>/<project>/agent-transcripts/<id>/<id>.jsonl`. The projects root SHALL default to `~/.cursor/projects`. `--projects-root` SHALL replace that default. A missing projects root SHALL add no IDE rows and SHALL NOT fail the command.

#### Scenario: Transcript beside an agent chat
- **WHEN** the projects root has one transcript and the chat catalog has a different chat
- **THEN** the list contains both

#### Scenario: Missing projects root
- **WHEN** the projects root does not exist
- **THEN** the list still contains the agent chats and the command does not fail because that root is missing

#### Scenario: Custom projects root
- **WHEN** the user passes `--projects-root`
- **THEN** IDE rows come from that directory

### Requirement: Shared session listed once
An id that exists as an agent chat and as an IDE transcript SHALL appear once, from the agent chat, with source `Cu-CLI`.

#### Scenario: Same id in both stores
- **WHEN** the same id is in the chat catalog and in an IDE transcript
- **THEN** the list has one row for that id and its source cell is `Cu-CLI`

### Requirement: IDE title and time
An IDE row's title SHALL be the first line of its first user query. An empty first line SHALL use the transcript id. The row's time SHALL be that JSONL file's modification time in the local timezone, in the same form as an agent chat.

#### Scenario: Query title
- **WHEN** the first user query's first line is non-empty
- **THEN** the row title is that line

#### Scenario: Empty query
- **WHEN** the transcript has no user query text
- **THEN** the row title is the transcript id

#### Scenario: File time
- **WHEN** an IDE transcript is listed
- **THEN** the row shows that JSONL file's modification time in the catalog's local time form

### Requirement: IDE workspace
The workspace path SHALL come from `workspacePath` in the project's `.workspace-trusted` file, and the workspace column SHALL show that path's last segment. When `workspacePath` is missing, the workspace column SHALL show the project folder name and This workspace SHALL omit the row.

#### Scenario: Trusted workspace
- **WHEN** `.workspace-trusted` names a workspace path and the CLI is launched from that path
- **THEN** the row shows the last segment of that path and This workspace includes the row

#### Scenario: Missing workspace path
- **WHEN** the project has no workspace path
- **THEN** the row shows the project folder name under All chats and is absent from This workspace

### Requirement: Unreadable IDE transcript
An IDE transcript that cannot be read SHALL be omitted from the list, and the CLI SHALL warn on stderr.

#### Scenario: Unreadable file
- **WHEN** a transcript file cannot be read
- **THEN** the list omits that id and stderr names that id

### Requirement: Export an IDE transcript
The CLI SHALL export an IDE transcript by id without opening the picker, using the same HTML or Markdown output as an agent chat.

#### Scenario: IDE id
- **WHEN** the user passes the id of an IDE transcript that contains a user query
- **THEN** the CLI writes that export and does not open the picker

### Requirement: Shared id exports the agent chat
An id present in both stores SHALL export the agent chat.

#### Scenario: Shared id
- **WHEN** the user passes an id that exists in both stores
- **THEN** the export contains the agent chat's query and does not contain a query that exists only in the IDE transcript

### Requirement: Export omits the source cell
The written HTML page and Markdown file SHALL use the session title and workspace and SHALL omit the source cell.

#### Scenario: HTML header
- **WHEN** an IDE transcript is exported to HTML
- **THEN** the page shows the session title and workspace and has no source column

### Requirement: Read-only IDE transcripts
The CLI SHALL read IDE transcript files and SHALL NOT modify them or `.workspace-trusted`.

#### Scenario: Export leaves the files
- **WHEN** an IDE transcript is exported
- **THEN** the JSONL file and `.workspace-trusted` are unchanged

### Requirement: IDE conversation lines
A `user` or `assistant` line SHALL become a conversation message in transcript order. A `turn_ended` line SHALL add no section.

#### Scenario: Turn ended
- **WHEN** the transcript contains a `turn_ended` line between two assistant messages
- **THEN** those assistant messages share one Agent heading and the export has no section for `turn_ended`

### Requirement: Unrecognized IDE line
A line that is not a user message, an assistant message, or `turn_ended` SHALL be named in the footer and omitted from the body.

#### Scenario: Other line
- **WHEN** a transcript line has some other type
- **THEN** the footer names that line and the body omits it

### Requirement: IDE tool caption
A tool call in an IDE transcript SHALL use the same caption rules as an agent chat.

#### Scenario: Shell caption
- **WHEN** the user passes `--tools` and an assistant message calls a command tool with a short description
- **THEN** the caption uses that description and omits the command text

### Requirement: Source filter line
The picker SHALL show a source line under the workspace scope. The line SHALL name All sources, Cursor-CLI, and Cursor-IDE. It SHALL mark All sources when every source is allowed, and SHALL mark each allowed source when the filter is a subset.

#### Scenario: Open on every source
- **WHEN** the picker opens without `--source`
- **THEN** the source line marks All sources

#### Scenario: One source is marked
- **WHEN** the active filter allows only agent chats
- **THEN** the source line marks Cursor-CLI and does not mark All sources

### Requirement: Source filter key
Pressing `s` on the chat list, in either case, SHALL cycle the filter through All sources, Cursor-CLI, and Cursor-IDE, then back to All sources. The selection SHALL return to the first visible row. The footer SHALL name `s`.

#### Scenario: Cycle to Cursor-CLI
- **WHEN** All sources is active and the user presses `s`
- **THEN** Cursor-CLI is active and the first visible row is selected

#### Scenario: Cycle back to All sources
- **WHEN** Cursor-IDE is active and the user presses `s`
- **THEN** All sources is active

### Requirement: Source key on the name line
While the file-name line is open, `s` SHALL be typed into the draft and SHALL leave the source filter unchanged.

#### Scenario: Typing s
- **WHEN** the file-name line is open and the user presses `s`
- **THEN** the draft gains `s` and the source filter is unchanged

### Requirement: Source filter with workspace scope
The source filter and the workspace scope SHALL both apply. Left and right SHALL keep switching All chats and This workspace without changing the source filter.

#### Scenario: Agent chats in this workspace
- **WHEN** This workspace and Cursor-CLI are both active
- **THEN** the list includes only agent chats whose workspace path is the launch directory

#### Scenario: Workspace key keeps the source
- **WHEN** the user presses left or right
- **THEN** the workspace scope changes and the source filter stays as it was

### Requirement: Source argument
`--source` SHALL accept `cursor-cli` and `cursor-ide`. Repeating `--source` SHALL allow each named source. Omitting `--source` SHALL allow every source. The picker SHALL open on that filter.

#### Scenario: Cursor CLI flag
- **WHEN** the user passes `--source cursor-cli`
- **THEN** the picker opens with Cursor-CLI active

#### Scenario: Both source flags
- **WHEN** the user passes `--source cursor-cli` and `--source cursor-ide`
- **THEN** the list includes agent chats and IDE transcripts

#### Scenario: Flag still cycles
- **WHEN** the picker opened with `--source cursor-cli` and the user presses `s`
- **THEN** the filter moves to Cursor-IDE

### Requirement: Unknown source argument
An unknown `--source` value SHALL be rejected before the catalog is read, and the CLI SHALL write no file.

#### Scenario: Unknown value
- **WHEN** the user passes `--source other`
- **THEN** the CLI rejects the value, does not read the catalog, and writes no file

### Requirement: Filtered chat id
An id whose source is outside the `--source` filter SHALL be reported as not found, and the CLI SHALL write no file.

#### Scenario: Agent chat excluded
- **WHEN** the user passes an agent chat id and `--source cursor-ide`
- **THEN** the CLI reports that the chat was not found and writes no file

### Requirement: Empty filtered list
When the active filters leave no sessions, the picker SHALL show an empty list. Enter SHALL write no file.

#### Scenario: Enter on an empty filter
- **WHEN** the visible list is empty and the user presses Enter
- **THEN** the CLI writes no file

## MODIFIED Requirements

### Requirement: Picker columns
The picker SHALL show each session's title, workspace folder name, source, and last-updated time in four columns on one row. The source column SHALL sit between the workspace and the time, SHALL be six characters wide, and SHALL read `Cu-CLI` for an agent chat and `Cu-IDE` for an IDE transcript. Rows on the same page SHALL share the same column starts. The selected row SHALL keep a `>` marker. Rules SHALL separate the header from the rows and the rows from the footer.

#### Scenario: Columns line up
- **WHEN** the page shows two sessions whose titles have different lengths
- **THEN** their workspace names start at the same column, their source cells start at the same column, and their times start at the same column

#### Scenario: Selected row
- **WHEN** a session is selected
- **THEN** that row begins with `>` and the other rows do not

#### Scenario: Source cells
- **WHEN** the page shows an agent chat and an IDE transcript
- **THEN** the agent chat's source cell is `Cu-CLI` and the transcript's source cell is `Cu-IDE`

### Requirement: Picker column widths
The time column SHALL have a fixed width wide enough for a two-digit day stamp, and a shorter stamp SHALL pad so it stays in that column. Last updated SHALL sit at the right edge of the time column. The source column SHALL be a fixed six characters between the workspace and the time. The workspace column SHALL match the longest workspace name on the current page, up to a cap. The title SHALL use the width that remains. A value that does not fit its column SHALL end with `...`.

#### Scenario: Time heading
- **WHEN** the picker is open
- **THEN** Last updated ends at the same column as the time stamps

#### Scenario: Long title truncates
- **WHEN** a title is wider than the title column
- **THEN** that cell ends with `...` and the workspace, source, and time columns stay aligned

#### Scenario: Long workspace truncates
- **WHEN** a workspace name is wider than the workspace cap
- **THEN** that cell ends with `...` and the source and time columns stay aligned

#### Scenario: Source column stays fixed
- **WHEN** a title is wider than the title column
- **THEN** each source cell is six characters and the source cells start at the same column

### Requirement: List scope
The CLI SHALL provide All chats and This workspace. This workspace SHALL include only sessions whose workspace path is the directory the CLI was launched from, whether the session is an agent chat or an IDE transcript. On Windows that comparison SHALL ignore letter case. On other systems it SHALL distinguish letter case.

#### Scenario: All chats
- **WHEN** All chats is selected
- **THEN** the list includes every catalogued session that the source filter allows

#### Scenario: This workspace
- **WHEN** This workspace is selected
- **THEN** the list includes only sessions whose workspace path is the launch directory

#### Scenario: Windows letter case
- **WHEN** the CLI runs on Windows and a session's workspace path differs from the launch directory only by letter case
- **THEN** This workspace includes that session

#### Scenario: Other systems letter case
- **WHEN** the CLI runs on a system other than Windows and a session's workspace path differs from the launch directory only by letter case
- **THEN** This workspace omits that session
