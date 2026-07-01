# MCP Safety

## Editor Process

- Do not start Unity Editor from the command line.
- Connect only to the Editor already opened from Unity Hub.
- If a restart seems necessary, ask the user first.
- Check that only the intended Unity Editor process is active.

## Operation Size

- Avoid long synchronous MCP calls.
- Split analyzer scans, full-scene traversal, GameObject generation, and asset operations into small batches.
- Test with 10 or fewer items before scaling up.
- Prefer read-only operations when verifying tool availability.

## Risky Operations

- Ask before scene save, prefab save, script deletion, asset deletion, or operations that may show dialogs.
- Do not leave Unity waiting for an unmanaged dialog through MCP.
- Avoid modifying debug scenes unless the user requested it.

## After Operations

- Check console or log errors.
- Confirm Editor responsiveness.
- Confirm MCP/relay running state when relevant.
- Confirm Unity Editor process count when process issues are suspected.
