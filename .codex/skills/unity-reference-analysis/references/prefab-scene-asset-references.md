# Prefab, Scene, and Asset References

## Scene Objects

- Scene objects belong to loaded scenes.
- Hierarchy parent/child edges are structural dependencies.
- Component references should remain tied to their owning GameObject.
- Selection sync should tolerate objects that disappeared after scan.

## Prefab Instances

- Prefab instances have scene identity and prefab source identity.
- Display them distinctly when the user needs to understand source versus instance.
- Prefab overrides can create references not present on the source asset.
- Nested prefabs should preserve both visible hierarchy and source context.

## Prefab Assets

- Prefab assets are project assets and should use asset identity.
- Avoid loading or saving prefab contents during read-only analysis unless explicitly required.
- Use read-only APIs where possible.

## Imported Assets

- Materials, textures, audio clips, meshes, scripts, and text files are asset nodes.
- Skip excluded folders and extensions consistently.
- Do not treat package assets and project assets as interchangeable.

## Edges

- Hierarchy, inspector, asset dependency, and used-by edges may have different meanings.
- Keep edge kind explicit.
- UI style can depend on edge kind, but graph data should remain style-neutral.
