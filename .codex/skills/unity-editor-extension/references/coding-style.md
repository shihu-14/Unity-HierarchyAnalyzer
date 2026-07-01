# Unity Editor Coding Style

## General

- Match the existing C# style before introducing new patterns.
- Keep public APIs small and intentional.
- Use descriptive names over comments that restate code.
- Prefer guard clauses for invalid Unity objects and missing assets.
- Avoid broad exception swallowing; include enough context when logging.

## Editor Lifecycle

- Assume domain reload can recreate windows, caches, and static fields.
- Rebuild UI from state instead of depending on stale VisualElements.
- Unregister callbacks when elements or windows are disposed.
- Debounce Editor callbacks that may fire repeatedly.

## Data Handling

- Convert Unity objects to tool data at scanner boundaries.
- Use stable identifiers for cross-frame graph state.
- Keep display labels separate from lookup keys.
- Avoid storing `UnityEngine.Object` references in long-lived data unless selection or ping requires it.

## UI Toolkit

- Put static visual styling in USS.
- Put runtime geometry, animation state, and data-driven classes in C#.
- Avoid repeated full tree rebuilds for small state changes.
- Prefer class toggles over assigning many individual inline styles.

## Comments

- Comment non-obvious Unity lifecycle or API behavior.
- Do not comment ordinary assignments or simple control flow.
- Keep comments in English.
