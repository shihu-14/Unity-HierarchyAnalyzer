# AssetDatabase Guidance

## Safe Reads

- Prefer read-only AssetDatabase queries during analysis.
- Cache path, GUID, type, and label lookups when repeated.
- Normalize asset paths before comparing them.
- Treat packages, built-in resources, and scene objects as distinct sources.

## Mutations

- Do not call delete, move, save, or prefab write APIs without explicit user approval.
- Avoid APIs that can open blocking Unity dialogs from automation.
- If mutation is required, operate on the smallest possible asset set.
- Verify Editor state after mutation.

## Identity

- Use GUID for assets when possible.
- Use GlobalObjectId when a stable reference to scene or asset sub-object is needed.
- Use InstanceID only for current-session UI interaction such as ping or selection.
- Never persist InstanceID as durable identity.

## Refresh

- Avoid full refreshes when a targeted import or natural Unity refresh is enough.
- Expect import and domain reload logs to contain older messages.
- Check recent log tails, not the entire log, when confirming current results.
