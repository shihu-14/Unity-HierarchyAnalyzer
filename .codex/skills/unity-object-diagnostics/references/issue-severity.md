# Issue Severity

## User-Facing Warnings

- Missing component scripts and broken serialized references are shown as warnings grouped by Object type.
- Keep the missing target's normal type color and icon, with 45% opacity.
- Place the warning marker on the direct reference source. If that source is hidden, propagate to the nearest visible ancestor.
- The Issues header is a non-interactive warning count; there are no Console-style severity filters.

## Internal Diagnostics

- Scanner diagnostics retain their warning/error severity separately from project issue occurrences.
- Do not include internal diagnostic severity or Unity Console logs in the Issues header.

## Counts And Navigation

- Count missing occurrences, not propagated visual copies; include occurrences under collapsed nodes.
- Retain Object type accents in group rows and source names/full paths in location rows.
- Navigate to the related graph node. Disable unresolved location rows without crashing or triggering a scan.
