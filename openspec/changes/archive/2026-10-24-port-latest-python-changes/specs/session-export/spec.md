# Spec Delta

## ADDED Requirements

### Requirement: Terminal page shape
A terminal theme SHALL use square section corners. A web theme SHALL keep rounded section corners.

#### Scenario: Terminal corners
- **WHEN** the page uses `terminal-dark` or `terminal-light`
- **THEN** the sections have square corners

#### Scenario: Web corners
- **WHEN** the page uses `dark-web` or `light-web`
- **THEN** the sections keep rounded corners

### Requirement: Turn filter
Each User navigator entry SHALL have a checkbox beside its fragment link. No checkbox SHALL start checked. With none checked, every transcript section SHALL stay visible. The page SHALL still contain no script.

#### Scenario: Nothing starts checked
- **WHEN** the page opens
- **THEN** every checkbox is unchecked and every User section and Agent section is visible

#### Scenario: Checkbox beside the link
- **WHEN** a User entry is shown
- **THEN** a checkbox sits beside that entry's fragment link and the link remains

### Requirement: Filtered turns
While one or more navigator checkboxes are checked, the transcript SHALL show those User sections and the Agent sections that follow each of them until the next User section. Other User, Agent, System, and Context sections SHALL be hidden. The chat title, the footer, and every navigator entry SHALL stay visible.

#### Scenario: One selected turn
- **WHEN** the reader checks the first of three User entries and an Agent section follows that User section
- **THEN** that User section and that Agent section are visible and the other User sections are hidden

#### Scenario: Two selected turns
- **WHEN** the reader checks the first and third User entries
- **THEN** both of those User sections and the Agent sections that follow each of them are visible and the second User section is hidden

#### Scenario: System and context stay out
- **WHEN** a checkbox is checked and the page has a System section or a Context section
- **THEN** that section is hidden

#### Scenario: Title and navigator stay
- **WHEN** a checkbox is checked
- **THEN** the chat title, the footer, and every navigator entry remain visible

### Requirement: Clear filter
The navigator SHALL offer Clear filter pinned above the turn list and again after the last turn. Both controls SHALL appear only while a checkbox is checked. Using either SHALL clear every checkbox and show every transcript section again.

#### Scenario: Both ends
- **WHEN** a checkbox is checked
- **THEN** Clear filter is shown pinned above the turn list and again after the last turn

#### Scenario: Hidden when nothing is checked
- **WHEN** no checkbox is checked
- **THEN** Clear filter is not shown

#### Scenario: Either control restores the transcript
- **WHEN** the reader uses either Clear filter
- **THEN** every checkbox is unchecked and every transcript section is visible

### Requirement: Navigator time grouping
The space between a navigator turn time and the prompt under it SHALL be smaller than the space between navigator entries. An entry with no turn time SHALL use that same space between entries. The first entry SHALL have no added space above it.

#### Scenario: Time sits with the prompt below
- **WHEN** the navigator shows two entries that each have a turn time and a prompt
- **THEN** the space between each time and its prompt is smaller than the space between the two entries

#### Scenario: Untimed entry keeps the row gap
- **WHEN** an entry has no turn time and another entry follows it
- **THEN** the space between those entries matches the space between two timed entries

#### Scenario: First entry stays at the top
- **WHEN** the navigator shows more than one entry
- **THEN** the first entry has no added space above it

## MODIFIED Requirements

### Requirement: Distinct turn panels
The HTML page SHALL paint each User section and each Agent section with different panels. On a web theme a User panel SHALL use a warm background and a gold edge, and an Agent panel SHALL use a cooler, flatter background. On a terminal theme a User panel SHALL use a phosphor background and an amber edge, and an Agent panel SHALL use a flatter phosphor background. A System or Context section SHALL NOT use the User panel or the Agent panel.

#### Scenario: User and Agent differ
- **WHEN** the page contains a User section and an Agent section
- **THEN** those sections use different panel backgrounds

#### Scenario: Both themes keep the difference
- **WHEN** the same transcript is exported in `dark-web` and in `light-web`
- **THEN** each file paints the User panel and the Agent panel differently, and the User panel keeps its gold edge

#### Scenario: Terminal themes keep the difference
- **WHEN** the same transcript is exported in `terminal-dark` and in `terminal-light`
- **THEN** each file paints the User panel and the Agent panel differently, and the User panel keeps its amber edge

#### Scenario: Other sections stay quiet
- **WHEN** the page contains a System or Context section
- **THEN** that section uses neither the User panel nor the Agent panel

### Requirement: Navigator type
The navigator time SHALL be 10px. On a web theme the navigator SHALL use a sans-serif face and the transcript SHALL keep its serif face. On a terminal theme the navigator and the transcript SHALL use a monospace face.

#### Scenario: Time is 10px
- **WHEN** the navigator shows a turn time
- **THEN** that time is 10px

#### Scenario: Navigator face
- **WHEN** a web theme page is written
- **THEN** the navigator uses a sans-serif face and the transcript keeps its serif face

#### Scenario: Terminal face
- **WHEN** a terminal theme page is written
- **THEN** the navigator and the transcript use a monospace face

### Requirement: Theme shown before export
The picker SHALL show the HTML theme that Enter will write.

#### Scenario: Default theme is visible
- **WHEN** the picker opens without a theme request
- **THEN** the picker shows `terminal-dark` as the theme that will be written

#### Scenario: Light theme is visible
- **WHEN** the user requested `terminal-light` and the picker is open
- **THEN** the picker shows `terminal-light` as the theme that will be written

#### Scenario: Web theme is visible
- **WHEN** the user requested `dark-web` or `light-web` and the picker is open
- **THEN** the picker shows that requested name as the theme that will be written

### Requirement: HTML theme chosen at export
The HTML file SHALL use `terminal-dark` unless the user requests `terminal-light`, `dark-web`, or `light-web`. The requested theme SHALL be part of the written file. `dark-web` SHALL keep the former dark palette. `light-web` SHALL keep the former light palette. The names `dark` and `light` SHALL be rejected.

#### Scenario: Dark by default
- **WHEN** the user exports without requesting a theme
- **THEN** the HTML file renders in `terminal-dark`

#### Scenario: Light when requested
- **WHEN** the user requests `terminal-light`
- **THEN** the HTML file renders in `terminal-light`

#### Scenario: Web dark when requested
- **WHEN** the user requests `dark-web`
- **THEN** the HTML file renders in the former dark palette

#### Scenario: Web light when requested
- **WHEN** the user requests `light-web`
- **THEN** the HTML file renders in the former light palette

#### Scenario: Former names are rejected
- **WHEN** the user passes `dark` or `light` as the theme
- **THEN** the CLI rejects the request and writes no file
