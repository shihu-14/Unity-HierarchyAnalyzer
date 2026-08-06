# Refactoring Safety

## Evidence Scope

- Only rules mapped as `Official requirement` or `Official recommendation` in `source-map.md` are official, and only within the cited source scope.
- Repository workflows, thresholds, role meanings, and commit practices are `Project policy`.
- Inferences assembled from multiple official principles are `Derived guideline`.
- Never present a project policy as a direct Unity or Microsoft requirement.

## Separate Structure From Behavior

Keep a structural refactor behavior-preserving. Do not combine a rename or move with new analysis rules, UI behavior, public API changes, serialization changes, dependency additions, or unrelated cleanup.

If a behavior change is required, record it separately and obtain approval before combining work.

## Preflight Inventory

Before changing a symbol or path:

1. Confirm the baseline branch and inspect uncommitted changes.
2. Search declarations, constructors, inheritance, interfaces, generics, and all call sites.
3. Determine public, protected, internal, and serialized exposure.
4. Search file names, namespaces, asmdefs, test names, UXML, USS, reflection strings, asset paths, menu paths, documentation, and CI scripts.
5. Identify `MonoBehaviour`, `ScriptableObject`, custom editor, and fixture implications.
6. Record Unity asset and folder `.meta` GUIDs when a move is possible.

Use `rg` or `rg --files` for repository searches. Do not infer safety from IDE symbol references alone.

## Rename Map

Create this table before implementation:

| Old symbol/path | New symbol/path | Responsibility reason | Classification | Reference risks | `.meta` action | Verification |
|---|---|---|---|---|---|---|

Include unchanged contextual names when needed to explain why a broad replacement is unsafe.

## Unity Asset And `.meta` Procedure

Unity `.meta` files contain asset identity. Losing or recreating one can break references.

Prefer one of these methods:

- Move or rename in the Unity Project window when an already-open Editor is intentionally being used.
- Outside Unity, move the asset and matching `.meta` together with explicit `git mv` commands.

For an outside-Unity move:

1. Record the original `guid:` value.
2. Move the asset to an explicit target.
3. Move the matching `.meta` to the matching target name.
4. Confirm the GUID is unchanged.
5. Confirm git reports a rename rather than an unrelated delete/add when practical.
6. Check prefabs, scenes, materials, ScriptableObjects, and tests that reference the asset.

Do not launch or restart Unity directly. Follow `unity-editor-verification` and ask before scene saves, prefab saves, asset deletion, or script deletion.

## Serialized And String-Based Risks

Check these explicitly because compiler rename support may miss them:

- Serialized `MonoBehaviour` and `ScriptableObject` type identity
- Managed-reference type names
- UXML element names and C# queries
- USS classes and `AddToClassList` calls
- Reflection type and member names
- Asset paths, icon paths, menu paths, and settings paths
- asmdef names and references
- Documentation, fixtures, snapshots, and test data

Do not add `MovedFrom`, compatibility wrappers, or aliases unless backward compatibility is explicitly required and approved.

## Verification

For documentation-only organization guidance:

- Run the skill validator.
- Run `git diff --check`.
- Confirm no production, Unity asset, asmdef, workflow, or generated file changed.

For C#, UXML, USS, asmdef, or Unity asset changes:

- Search repository-wide for stale names.
- Confirm moved `.meta` GUIDs.
- Confirm clean import and zero current compile errors.
- Run all relevant Edit Mode Tests.
- Run the supported Unity version matrix in CI.
- Review public and serialized contract changes.
- Inspect `git diff --check`, `git status`, and the complete staged diff.

Report tests that could not be run and the reason. Do not claim compile or CI success without current evidence.

## Commit Boundaries

Use small, meaningful, reversible commits. Stage only related changes. Keep behavior changes, dependency changes, generated artifacts, and unrelated formatting out of a structural refactor commit.
