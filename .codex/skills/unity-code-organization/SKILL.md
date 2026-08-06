---
name: unity-code-organization
description: Use for repository-wide Unity C# naming, namespace/folder/file/assembly organization, responsibility and granularity reviews, partial-class decisions, and behavior-preserving rename or move work that must preserve Unity .meta GUIDs. Trigger when evaluating ambiguous names, reorganizing code without changing behavior, or planning and verifying structural refactors.
---

# unity-code-organization

Use this skill to make evidence-based naming and structural decisions without changing product behavior.

## When To Use

- Review class, interface, method, field, file, folder, namespace, or assembly names.
- Decide whether a contextual prefix or suffix is redundant or necessary.
- Evaluate folder classification axes, type responsibility, file granularity, or partial classes.
- Plan or perform an explicitly authorized behavior-preserving rename or move.
- Verify Unity `.meta`, serialized, reflection, UXML, USS, test, and documentation references after a structural refactor.

## When Not To Use

- Do not use this skill to redesign dependency analysis, diagnostics, graph behavior, or UI behavior.
- Do not treat a naming review as authorization to rename or move files.
- Do not use line count alone to justify a split.
- Do not propose UPM migration unless the user explicitly requests it.
- Do not add compatibility layers or change public or serialized contracts without approval.

## Required References

Read only the references needed for the task, but always read `references/source-map.md` before presenting a rule as official.

- Naming or UI identifier work: `references/naming.md`
- Folder, namespace, asmdef, or test layout work: `references/project-structure.md`
- Class, file, role suffix, or partial-class work: `references/responsibility-and-granularity.md`
- Rename, move, or structural refactoring work: `references/refactoring-safety.md`
- Evidence classification and official-source scope: `references/source-map.md`

## Analysis Workflow

1. Confirm the requested scope and whether the task is analysis-only or authorizes changes.
2. Inspect `AGENTS.md`, the current branch and diff, asmdefs, namespaces, declarations, partial files, and relevant tests.
3. Search all usages before judging a name. Include string-based references, serialized types, reflection, UXML, USS, asset paths, and documentation.
4. Describe each subject's responsibility, consumers, dependencies, and reasons to change.
5. Apply naming and responsibility rules before generating candidates.
6. Compare candidates by role clarity, ambiguity, collision risk, searchability, call-site readability, and compatibility impact.
7. Classify each rule as `Official requirement`, `Official recommendation`, `Project policy`, or `Derived guideline`.
8. Separate findings from proposed changes. Do not execute unapproved renames or moves.

## Rename And Move Workflow

1. Record a rename map with old and new symbols and paths, reason, evidence class, reference risks, and verification.
2. Confirm public, protected, serialized, reflection, UXML/USS, asmdef, test, and documentation impact.
3. Move Unity assets and their `.meta` files together. Preserve the original GUID.
4. Keep structural changes separate from behavior changes.
5. Update declarations, file names, namespaces, asmdefs, tests, string references, and documentation as one coherent change.
6. Run the verification checklist before committing.

## Verification Checklist

- Search for stale names repository-wide.
- Compare pre-move and post-move `.meta` GUIDs for Unity assets.
- Confirm no unexpected public or serialized contract change.
- Check UXML, USS, reflection, asset path, and documentation references.
- Run `git diff --check` and review the complete diff.
- Compile and run all relevant Edit Mode Tests for code changes.
- Confirm the supported Unity CI matrix for repository changes that touch Unity code or assets.
- Keep commits small, reversible, and limited to the approved scope.

## Output Format

Provide these sections when reviewing or planning organization work:

1. Current responsibility and usage
2. Findings with evidence classification
3. Candidate comparison
4. Proposed rename or move map, if requested
5. Compatibility and Unity reference risks
6. Verification performed or required
7. Human decisions still required

## Related Existing Skills

- Use `unity-editor-extension` for Editor/runtime boundaries, asmdef architecture, and Unity Editor APIs.
- Use `unity-reference-analysis` for GUID, fileID, GlobalObjectId, prefab, scene, and serialized reference internals.
- Use `unity-ui-toolkit-graph` for graph rendering and UI Toolkit implementation details.
- Use `graph-data-modeling` for graph identity and model semantics.
- Use `unity-editor-verification` for safe Unity compile and test verification.
- Use `unity-object-diagnostics` or `unity-editor-performance` only when those concerns are explicitly in scope.

## Repository-Specific Boundaries

- Preserve the Assets-copy distribution format unless UPM migration is explicitly requested.
- Preserve the existing `Core`, `Scanners`, `Controller`, and `UI` responsibility direction.
- Treat `Settings`, `Utils`, and test fixture placement as subjects to investigate, not automatic rename targets.
- Do not mechanically remove `Dependency` or shorten a type to `Node`, `Log`, `Message`, or another generic name.
- Do not mix structural refactoring with production behavior changes.
- Ask before changing public APIs, serialized compatibility, asmdef boundaries, or Unity assets in ways that can break references.
