# Spec Delta

## ADDED Requirements

### Requirement: Reserved device file name
The CLI SHALL NOT write an export whose stem is a Windows reserved device name. After illegal-character cleanup, and before the extension, the stem SHALL be compared without letter case to CON, PRN, AUX, NUL, COM1 through COM9, and LPT1 through LPT9. A reserved default stem SHALL be replaced with the chat id. A reserved typed stem SHALL be replaced with the default file name.

#### Scenario: Title is a device name
- **WHEN** the chat title is `CON` and the user does not pass `--name` or type a file name
- **THEN** the file stem is the chat id

#### Scenario: Device name ignoring case
- **WHEN** the chat title is `com1` and the user does not pass `--name` or type a file name
- **THEN** the file stem is the chat id

#### Scenario: Typed reserved name
- **WHEN** the user confirms a name line whose text is `PRN` and the chat title is `Notes`
- **THEN** the written file uses the title file name and is not named `PRN`

#### Scenario: Title and typed name are both reserved
- **WHEN** the chat title is `CON` and the user confirms a name line whose text is `AUX`
- **THEN** the file stem is the chat id

#### Scenario: Ordinary title
- **WHEN** the chat title is `Console notes` and the user does not pass `--name` or type a file name
- **THEN** the file stem is `Console notes`

#### Scenario: Named path is a device name
- **WHEN** the user passes a chat id and `--name` whose file stem is `CON`
- **THEN** the CLI reports the error, writes no file, and exits unsuccessfully

### Requirement: Impossible turn timestamp
A turn timestamp that matches the stamp shape but is not a real civil date SHALL be shown as its normalized raw text. The export SHALL write the file.

#### Scenario: Impossible day
- **WHEN** a visible user turn records `Friday, Oct 32, 2026, 11:13 AM (UTC-7)`
- **THEN** the User section shows that text and the export writes the file

#### Scenario: Navigator keeps the raw stamp
- **WHEN** that same turn is shown in the HTML navigator
- **THEN** the navigator shows that text as stored

### Requirement: Unexpected content shape
A readable message SHALL contribute only content parts whose JSON shape matches what that part claims to be. The export SHALL write the file.

#### Scenario: Numeric part type
- **WHEN** an assistant message has one content part whose type is a number and another whose type is text
- **THEN** the export includes the text part, omits the numeric part, and writes the file

#### Scenario: Numeric role
- **WHEN** one message has a numeric role and a later message has a user query
- **THEN** the export includes that query, omits the numeric-role message, and writes the file

### Requirement: Unbounded conversation blob
When a conversation blob declares a length that does not fit in the remaining bytes or in a 32-bit length, the export SHALL NOT end with an unhandled exception. If no turns are recovered, the CLI SHALL report that the chat has no transcript and SHALL write no file.

#### Scenario: Length does not fit
- **WHEN** the root blob declares a length that does not fit in a 32-bit length and no role message can be read
- **THEN** the CLI reports that the chat has no transcript, writes no file, and does not end with an unhandled exception

### Requirement: Markdown metadata stays one line
The Markdown title line, workspace, and chat timestamp SHALL each stay on one line. A newline in any of those values SHALL be written as a space. Line breaks inside the transcript body SHALL remain.

#### Scenario: Title contains a newline
- **WHEN** the chat title contains a newline and the user passes `--markdown`
- **THEN** the title text from both sides of that newline appears on the title line, separated by a space, and that newline does not start another line

#### Scenario: Workspace contains a newline
- **WHEN** the workspace name contains a newline and the user passes `--markdown`
- **THEN** that newline is written as a space on the metadata line

#### Scenario: Body line breaks remain
- **WHEN** assistant text contains a blank line and the user passes `--markdown`
- **THEN** the Markdown body keeps that blank line

## MODIFIED Requirements

### Requirement: Image attached to a turn
The HTML page SHALL embed a visible turn's image inside that file only for a base64 payload whose media type is image/png, image/jpeg, image/gif, or image/webp, compared without letter case. A missing media type SHALL be treated as image/png. The HTML data URI SHALL encode the media type and payload. Anything else SHALL be omitted from the HTML body and the Markdown body, and the page SHALL have no external address.

#### Scenario: Embedded image
- **WHEN** a visible turn includes an image whose media type is image/png and whose payload is base64
- **THEN** the HTML embeds that image and the page has no external address

#### Scenario: Missing media type
- **WHEN** a visible turn includes a base64 image payload and no media type
- **THEN** the HTML embeds that image as image/png

#### Scenario: JPEG GIF and WebP
- **WHEN** a visible turn is exported once each for image/jpeg, image/gif, and image/webp, each with a base64 payload
- **THEN** each HTML file embeds that payload under the media type used for that export

#### Scenario: Media type letter case
- **WHEN** a visible turn's media type is `IMAGE/PNG` and its payload is base64
- **THEN** the HTML embeds that image

#### Scenario: Payload outside base64
- **WHEN** a visible turn's image payload contains a double quote
- **THEN** the HTML body and the Markdown body omit that image and the page contains no script

#### Scenario: Media type outside the set
- **WHEN** a visible turn's media type is `image/svg+xml` and its payload is base64
- **THEN** the HTML body and the Markdown body omit that image and the page contains no script
