# Diagnostic Scope

## User-Facing Issues

- Missing component scripts on a GameObject.
- Broken object references in serialized fields; unassigned `None` is not missing.
- Group missing occurrences by referenced Object type. Component and serialized script references share the Script group.

## Internal Diagnostics

- Keep property, component, and scanner failures in the graph for diagnosis while retaining partial results.
- Do not add these failures to the user-facing Issues count or automatically log them to Unity Console.
- An unhandled failure of the complete scan is logged as an exception by the Controller.

## Excluded Sources

- Do not ingest Unity Console or Editor.log entries, runtime exceptions, compile/import logs, or generic Editor warnings.
- Do not add project-specific component health rules or infer that an unassigned optional reference is broken.

## Mapping And Text

- Preserve source object identity and the serialized member path.
- Display the source object name and full occurrence path.
- Navigate to the graph node associated with the occurrence; do not start a new scan for an unresolved location.
- Preserve the original source identity when its warning is propagated to a visible ancestor.
