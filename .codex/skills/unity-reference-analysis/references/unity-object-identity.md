# Unity Object Identity

## Identity Types

- GUID:
  - Durable asset identity.
  - Best for project assets.
- fileID or local file identifier:
  - Identifies an object inside an asset or scene file.
  - Needed for components, sub-assets, and MonoScript references.
- GlobalObjectId:
  - Stable Unity identity for assets and scene objects when available.
  - Good bridge between serialized data and Editor object lookup.
- InstanceID:
  - Current Editor session identity.
  - Useful for ping, selection, and temporary lookup only.

## Graph Keys

- Use deterministic keys that survive refresh and domain reload.
- Prefer asset GUID plus local ID for assets and sub-assets.
- Prefer scene path or scene GUID plus hierarchy/local identity for scene objects when available.
- Do not key long-lived nodes only by object name or path.

## Display

- Display names can change and are not identity.
- Paths are useful for search and tooltip context.
- Labels, kinds, and types are metadata, not identity.

## Failure Modes

- Imported assets can be unavailable during import.
- Scene objects can be destroyed between scan and UI interaction.
- Prefab instances can remap local identity.
- Always handle failed lookup as a recoverable stale reference.
