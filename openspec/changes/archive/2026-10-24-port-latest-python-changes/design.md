# Design

## Context

See proposal.md for why. `HtmlRenderer.Render` takes `dark` or `light` and otherwise uses the dark palette. One stylesheet hardcodes Georgia for the body, a sans navigator, `border-radius: 8px`, and `gap: 0.15rem` between a navigator time and its prompt. Navigator rows have no space between them. Each User entry is a fragment link inside a list item, and sections have no turn mark. `Exporter.Run` defaults `--theme` to `dark` and rejects any other name before it reads the store. Help, the picker footer, and the default-page test all use those two names. The page must still contain no script and no `http`.

## Goals / Non-Goals

**Goals:**

- Bake one of four palettes into the file, with `terminal-dark` as the default.
- Filter the transcript from navigator checkboxes without a script.
- Put each turn time against the prompt under it, and separate entries by a larger gap.

**Non-Goals:**

- A theme switch inside the page.
- Coloring the Markdown export, or writing HTML when `--markdown` is set.
- Filtering the navigator list itself.
- Accepting `dark` or `light` as flag values.

## Decisions

### One palette map, including face and corners

Each theme is a palette plus a body face, a navigator face, and a section radius. The stylesheet stays shared. Web themes keep the current hex values, Georgia for the body, the sans navigator, radius `8px`, and the gold `--mark`. Terminal themes set radius `0` and use `Consolas, ui-monospace, monospace` for the body and the navigator.

`terminal-dark`: background `#071207`, text `#c6f7c0`, muted `#6ea86a`, time and mark `#e2c15a`, panel `#0c1a10`, user `#12301a`, agent `#0b1610`, rule `#1e3a24`, code `#040a05`.

`terminal-light`: background `#e6f5df`, text `#0e3a16`, muted `#3f6b45`, time and mark `#7a5b12`, panel `#f4fbf1`, user `#d7f0c8`, agent `#e7f3e2`, rule `#b7d4b0`, code `#f7fff4`.

The amber mark is the User edge on both terminal themes. Agent has a different panel and no edge. System and Context stay on `--panel`.

Alternative: recolor only, and leave Georgia and rounded cards. That keeps a web page in green paint. The console face and square corners are what make the terminal themes read as a screen.

### The flag names are the only names

`--theme` choices are `terminal-dark`, `terminal-light`, `dark-web`, and `light-web`. The default on `Exporter.Run`, on `ExportSession`, and on `HtmlRenderer.Render` is `terminal-dark`. An unknown theme value inside the renderer also uses `terminal-dark`. The argument parser rejects `dark` and `light` before the store is read. The picker prints the chosen name, so the footer is `theme: terminal-dark` unless another allowed name was passed.

The README sentence that says HTML is the default stays. Add a sentence that names `terminal-dark` as the default and the other three names. The sample theme command, if one is added, uses `--theme terminal-light`.

### Style rules react to the checkboxes

The boxes and both Clear filter controls live in one form in the navigator. Each control is a reset button, so either one restores the unchecked state. The top button is the first item in the scrolling navigator and sticks to its top. The bottom button follows the last entry.

Each User section and the Agent sections after it, until the next User section, share a turn mark (`data-turn`). While any box is checked, transcript sections are hidden, and each checked box shows the sections with its mark. System and Context sections have no mark, so they stay hidden. The title and the footer are not sections, so they stay. The checkbox is a sibling of the fragment link, not wrapped around it.

Alternative: filter with a script. The page is required to contain none. Alternative: make the whole row the checkbox label. That mixes checking with navigating.

### Separate rows, then tighten the pair

Following rows get `margin-top: 0.85rem`. The first row gets none, so `li + li` carries the margin. The link's column gap becomes 0. The time keeps `font-size: 10px` and `white-space: nowrap`, and gains `line-height: 1`. `0.85rem` matches the Python page, which rejects `0.75rem`.

Alternative: a hairline between rows. The gap alone makes the pair closer than the rows, and a line would cut through the filter list.

## Risks / Trade-offs

- [Existing exports and scripts pass `--theme dark` or `--theme light`] → Those names are rejected. The README and help show the four names.
- [The default-page test looks for `#161616` and the sans navigator] → Those assertions move to a `dark-web` page. The default page asserts `#071207` and the monospace face. `ExportTests` currently expects `#161616` in the default HTML file and must expect the terminal palette instead.
- [An older browser ignores the checked-box rule] → The boxes and Clear filter are still present, and every turn stays visible.
- [A long chat adds one style rule per User section] → The rules stay in the same file. They name turns that are already on the page.
- [The prompt's inherited line height still leaves a few pixels under the stamp] → That remainder stays smaller than the `0.85rem` row gap.

## Migration Plan

No store migration. A new export without `--theme` writes `terminal-dark`. Pass `dark-web` or `light-web` to get the previous palettes. An export written before this change has no checkboxes. Rollback is restoring the two theme names and the previous navigator markup.
