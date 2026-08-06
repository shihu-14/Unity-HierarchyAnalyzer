# Changelog

## 0.1.0-dev - Unreleased

- Add automated Unity 6 Edit Mode coverage for scene, Inspector, Prefab, and broken-reference analysis.
- Distinguish unassigned references from broken serialized references.
- Preserve partial graph results when individual components or scanners fail.
- Use stable object identity for assets and sub-assets.
- Remove project-specific component health guesses from production analysis.
- Read Console issues only from the current Unity Console snapshot instead of Editor.log.
- Preserve unlinked Console issues and available file, line, stack trace, context, and occurrence metadata.
- Match Unity Console error and warning mode classification, including graph compile errors.
- Separate current Console row counts from Analyzer issue counts in the Issues header.
- Remove obsolete asset-size metadata and dependency count badges while retaining reference counts in tooltips.
- Show asset labels in node tooltips only when an Asset has non-empty labels.
- Rename public graph, node, edge, issue, cache, window, and graph-view element types to reflect their domain responsibilities.
- Split asset and diagnostic node creation, serialized-reference reading policies, icon loading, and node styling into focused types.
- Remove the mixed-responsibility `Editor/Utils` folder and organize Edit Mode Tests by production responsibility.
