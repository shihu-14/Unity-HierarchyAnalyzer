# Cache Strategy

## Cache Types

- Source cache:
  - Unity object or asset lookup data.
  - Invalidated by scene, asset, or domain changes.
- Graph cache:
  - Nodes, edges, and direct issues from a scan.
  - Invalidated by rescan.
- Derived UI cache:
  - visibility, propagation, search matches, and layout.
  - Invalidated by local state changes.

## Rules

- Keep cache ownership clear.
- Do not cache stale Unity object references as durable truth.
- Store enough version or scan identity to reject outdated async results.
- Prefer recomputing small derived data over keeping fragile global state.

## Invalidation

- Rescan invalidates graph and derived UI caches.
- Expand/collapse invalidates visibility and propagated issue display.
- Search query invalidates search matches only.
- Asset import or hierarchy change should schedule refresh, not block immediately.
