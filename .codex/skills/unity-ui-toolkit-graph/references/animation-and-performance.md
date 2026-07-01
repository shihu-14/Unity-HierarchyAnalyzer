# UI Animation and Performance

## Animation

- Use short, purposeful animation for expand/collapse and highlight fade.
- Keep visual endpoint and state endpoint synchronized.
- Avoid moving nodes instantly when another expanded group changes.
- Use one progress signal for related movement and opacity when possible.

## UI Toolkit Costs

- Layout rebuilds are expensive.
- Repeated style changes across many elements can stutter.
- Prefer class toggles and batched updates.
- Avoid rebuilding the entire graph for small expand/collapse changes.

## Search

- Build or update search indexes outside hot rendering paths.
- Reuse match collections when query text has not changed.
- Do not highlight all matches if only the active match is needed.
- Keep suggestion rendering bounded by UI needs.

## Issue UI

- Keep issue row rendering lightweight.
- Reuse icons and avoid loading textures per row.
- Sort and group data before creating VisualElements.
- Click handling should resolve targets without running a new scan.

## Verification

- Check large graphs after UI changes.
- Test expand/collapse, search navigation, issue row click, and zoom.
- Confirm no obvious stutter in detail mode.
