# Changelog

## 0.1.0-dev - Unreleased

- Add automated Unity 6 Edit Mode coverage for scene, Inspector, Prefab, and broken-reference analysis.
- Distinguish unassigned references from broken serialized references.
- Preserve partial graph results when individual components or scanners fail.
- Use stable object identity for assets and sub-assets.
- Remove project-specific component health guesses from production analysis.
- Read Console issues only from the current Unity Console snapshot instead of Editor.log.
- Preserve unlinked Console issues and available file, line, stack trace, context, and occurrence metadata.
- Remove file size and dependency count badges from node presentation while retaining reference counts in tooltips.
- Show asset labels in node tooltips only when an Asset has non-empty labels.
