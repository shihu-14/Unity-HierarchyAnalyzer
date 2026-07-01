# Diagnostic Scope

## Include

- Missing component on a GameObject.
- Missing object reference in a serialized field.
- Script compile error that can be linked to a script asset.
- Asset import warning or error that can be linked to an asset node.
- Prefab or scene serialization warning that can be linked to a prefab, scene, object, or component.

## Usually Exclude

- Generic Editor warnings with no stable object or asset target.
- Package noise unrelated to visible project nodes.
- Licensing, entitlement, or Hub messages.
- Performance logs that do not indicate an object issue.
- Duplicate logs that cannot improve graph understanding.

## Text

- Console-originated issues should preserve meaningful console wording.
- Remove noisy tool prefixes when they do not help the user.
- Keep property path, asset path, or object path when useful.
- Avoid inventing issue descriptions not supported by collected data.

## Mapping

- Prefer the direct object, component, script, or asset target.
- Fall back to nearest visible representative only for display/navigation.
- Never lose the original target identity during propagation.
