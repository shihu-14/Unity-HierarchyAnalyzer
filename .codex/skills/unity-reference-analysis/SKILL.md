---
name: unity-reference-analysis
description: Use for low-level Unity reference analysis involving SerializedProperty, missing references, missing components, GUID, fileID, GlobalObjectId, InstanceID, scene objects, prefab instances, prefab assets, and imported assets.
---

# unity-reference-analysis

Use this skill for low-level Unity reference and serialization analysis.

## When To Use

- Scanning Inspector references.
- Handling missing object references or missing components.
- Resolving scene, prefab, asset, script, or sub-asset identity.
- Working with GUID, fileID, GlobalObjectId, LocalFileIdentifier, or InstanceID.

## Workflow

1. Identify whether the source is scene object, prefab instance, prefab asset, imported asset, or script.
2. Read the relevant reference:
   - `references/serialized-properties.md`
   - `references/unity-object-identity.md`
   - `references/prefab-scene-asset-references.md`
3. Preserve the difference between missing, null, unloaded, and intentionally ignored references.
4. Convert Unity references into stable graph data before UI rendering.
5. Verify behavior with small fixtures before scanning large projects.

## Defaults

- Use SerializedProperty for Inspector-visible references.
- Use stable identity for graph keys.
- Use InstanceID only as a temporary bridge to Editor selection or ping.
