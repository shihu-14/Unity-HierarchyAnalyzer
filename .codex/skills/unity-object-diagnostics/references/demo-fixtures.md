# Diagnostic Demo Fixtures

## Purpose

- Fixtures should cover scanner and UI behavior without requiring a real broken project.
- Keep intentional broken cases isolated and named clearly.
- Use small batches when creating or modifying Unity scene objects through automation.

## Useful Cases

- GameObject with missing component.
- Component with missing serialized object reference.
- Script asset with a known compile issue fixture when safe.
- Asset with import warning or error fixture.
- Prefab instance with an override that differs from its source.
- Hidden child issue that propagates to a visible parent.

## Safety

- Ask before saving scenes or prefabs.
- Do not delete scripts or assets through MCP without explicit approval.
- Avoid creating hundreds of objects in one MCP call.
- Verify Editor responsiveness after fixture generation.

## Naming

- Prefix intentional fixture objects clearly, such as `Issue_`.
- Do not show internal prefixes in user-facing issue text unless useful.
- Keep fixture folders separate from production assets.
