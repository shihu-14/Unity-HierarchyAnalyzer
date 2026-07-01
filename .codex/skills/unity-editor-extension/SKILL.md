---
name: unity-editor-extension
description: Use for Unity Editor-only extension architecture, file layout, asmdef boundaries, EditorWindow, SettingsProvider, AssetDatabase, SerializedObject, and Unity-specific C# coding guidance.
---

# unity-editor-extension

Use this skill for Unity Editor extension architecture and implementation.

## When To Use

- Adding or changing Editor-only Unity tooling.
- Working with `EditorWindow`, `SettingsProvider`, `AssetDatabase`, `SerializedObject`, or `SerializedProperty`.
- Choosing where code belongs in an Editor assembly.
- Reviewing Unity-specific coding style and file layout.

## Workflow

1. Confirm the feature is Editor-only and belongs outside runtime assemblies.
2. Read the relevant reference before editing:
   - `references/architecture.md`
   - `references/coding-style.md`
   - `references/asset-database.md`
3. Keep Unity API calls behind small, testable boundaries when practical.
4. Avoid direct scene, prefab, or asset mutation unless the task explicitly requires it.
5. Verify with compile logs and repository checks after changes.

## Defaults

- Prefer explicit data models over passing Unity objects through UI state.
- Prefer incremental Editor work over full-project refresh or blocking scans.
- Prefer existing project folder roles before introducing new directories.
