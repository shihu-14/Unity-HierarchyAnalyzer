# Issue Severity

## Severity Levels

- Error:
  - Compile error.
  - Import error.
  - Broken serialized state that prevents correct behavior.
  - Missing script or component when the object cannot operate as expected.
- Warning:
  - Missing object reference that may be intentional but deserves review.
  - Import warning.
  - Serialization warning that does not block compilation.
  - Diagnostic propagated from hidden descendants.

## Display

- Node color should continue to represent node kind.
- Severity is represented by warning/error icon and issue panel counters.
- Issue panel rows can use the node kind color as an accent.
- Do not replace a script node icon with an error icon; place severity next to the normal node identity.

## Counts

- Count direct issues, not every propagated display copy.
- Warning and error filters should behave like Unity Console count toggles.
- Hidden issues should still count if they are part of the loaded graph.

## Navigation

- Direct visible target wins.
- Hidden target navigates to nearest visible representative.
- If lookup fails, show the row but do not crash or start a scan.
