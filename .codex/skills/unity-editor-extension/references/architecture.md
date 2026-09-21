# Unity Editor Extension Architecture

## Assembly Boundary

- Keep tool code in an Editor-only assembly.
- Do not let runtime code depend on Editor-only types.
- Put shared plain data in the Editor assembly unless a runtime use case is explicit.
- Avoid reflection across assembly boundaries unless Unity does not expose a stable API.

## Common Layers

- Core data:
  - Stores stable, serializable, or immutable graph-oriented data.
  - Does not call UI Toolkit.
  - Should not require live Unity object references for routine display.
- Scanners:
  - Read Unity state and convert it into core data.
  - Own Unity API details such as `SerializedObject`, `AssetDatabase`, and scene traversal.
  - Should be chunkable when scans can become large.
- Controller:
  - Coordinates scan lifecycle, cache, selection sync, search, expand state, and issue routing.
  - Owns UI state transitions, not visual styling.
- UI:
  - Renders existing data and sends user intent back to the controller.
  - Avoids scanning, AssetDatabase traversal, and heavy lookup work.
- Debug:
  - Creates intentional fixtures and demo cases.
  - Must be isolated from production scanning rules.

## Dependency Direction

- The EditorWindow constructs the Controller and GraphView.
- Controller depends on Core, Scanners, Settings, and UI.
- UI depends on Core and Unity Editor/UI Toolkit APIs, not Controller. Views emit user intent through events; the Controller coordinates the response.
- GraphViewIndex derives traversal data from the collected graph without collecting Unity state.
- Scanners can depend on Core and UnityEditor APIs.
- Core should not depend on Controller, UI, or Scanners.

## Design Rules

- Treat Unity instance objects as volatile.
- Use stable IDs for persisted or cross-frame state.
- Keep user-visible behavior out of scanner-specific implementation details.
- Do not add global static mutable state unless Unity lifecycle requires it.
- Document lifecycle assumptions near Editor callbacks.
