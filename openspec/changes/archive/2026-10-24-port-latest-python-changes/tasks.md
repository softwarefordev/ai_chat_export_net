# Tasks

## 1. Theme names and palettes

- [x] 1.1 Give `HtmlRenderer` the four palettes from `design.md`, including body face, navigator face, and section radius, and default an omitted or unknown theme to `terminal-dark`. Verify a default page contains `#071207`, `Consolas, ui-monospace, monospace`, and `border-radius: 0`; `dark-web` keeps `#161616`, Georgia, the sans navigator, `border-radius: 8px`, and the gold mark; `light-web` contains `#f7f7f4`; `terminal-light` contains `#e6f5df` and its amber mark; User and Agent panels still differ on every theme; and no page contains `<script` or `http`.
- [x] 1.2 Accept only `terminal-dark`, `terminal-light`, `dark-web`, and `light-web` in `Exporter.Run`, default the flag and `ExportSession` to `terminal-dark`, and reject `dark` and `light` before any store read. Verify help names those four choices, a rejected theme writes no file, `Writes_html_and_leaves_an_existing_markdown_file` expects `#071207` in the default HTML file, and picker frames show `theme: terminal-dark` by default and the requested name for `terminal-light`, `dark-web`, and `light-web`.
- [x] 1.3 Name `terminal-dark` as the HTML default and list the other three theme names in `README.md`. Verify `Help_exits_zero_and_readme_names_the_tool` still passes and the README contains those four names.

## 2. Navigator filter and spacing

- [x] 2.1 Mark each User section and the Agent sections that follow it, until the next User section, with the same `data-turn` value, and leave System and Context unmarked. Verify a page with three User sections shares the first mark with the Agent stretch after the first User section, gives the third User section and its following Agent section a different mark, and does not mark System or Context.
- [x] 2.2 Put an unchecked checkbox beside each User fragment link, inside one navigator form with a Clear filter reset button pinned above the list and another after the last turn, and add the style rules that hide transcript sections while a box is checked and show only the marked sections for each checked box. Verify the boxes start unchecked, both buttons are present and hidden until a box is checked, each User section has a matching show rule, the page still has no script and no external address, and a Markdown export contains no checkbox and no Clear filter.
- [x] 2.3 Set navigator link gap to 0, turn-time line height to 1, and `margin-top: 0.85rem` on `li + li` only. Verify the stylesheet contains that row gap and `line-height: 1`, does not contain `0.75rem`, and a Markdown export does not contain `0.85rem`.

## Workflow follow-up

- Implement with `/opsx-apply` after this proposal is reviewed.
- Archive with `/opsx-archive` after implementation is reviewed.
