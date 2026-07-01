# UI Toolkit Performance

## Rebuild Scope

- Rebuild only changed graph sections when possible.
- Avoid removing and recreating all nodes for local state changes.
- Keep expand/collapse transitions data-driven and bounded.
- Pool or reuse repeated row/icon elements when practical.

## Layout

- Fixed dimensions reduce layout churn.
- Avoid text-driven resizing in dense controls.
- Batch style and class changes.
- Prefer USS classes for repeated visual states.

## Rendering

- Recompute edge geometry only when endpoints move or zoom changes.
- Avoid expensive allocations in repaint or geometry callbacks.
- Keep highlight animation lightweight.
- Use cached textures/icons.

## Interaction

- Search navigation should not trigger a rescan.
- Issue row clicks should use existing mapping.
- Drag and pan handlers should avoid heavy work per event.
- Debounce sync from Unity selection when needed.
