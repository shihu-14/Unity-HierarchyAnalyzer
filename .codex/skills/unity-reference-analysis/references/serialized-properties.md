# SerializedProperty Reference Scanning

## Purpose

- SerializedProperty scanning should reflect what the Inspector can serialize.
- It should not guess arbitrary runtime-only relationships.
- It should distinguish visible data from derived display convenience.

## Object References

- Check `SerializedPropertyType.ObjectReference`.
- A real reference has a non-null `objectReferenceValue`.
- A missing reference may have a null object value while still carrying serialized identity data.
- Use Unity's current entity/global identity APIs when available.
- Avoid relying on deprecated InstanceID APIs for durable data.

## Missing Cases

- Missing script and missing object reference are different issues.
- Missing component is attached to a GameObject/component slot.
- Missing object reference is attached to a serialized field path.
- Record enough context to show the property path and owning object.

## Traversal

- Respect excluded folders, extensions, and built-in component rules.
- Avoid infinite traversal through cyclic references.
- Track visited object identities.
- Yield or chunk when scanning many objects.

## Output

- Emit graph edges and issues separately.
- Do not encode issue severity into edge identity.
- Keep source object, property path, target identity, and issue context available.
