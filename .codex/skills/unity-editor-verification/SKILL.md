---
name: unity-editor-verification
description: Use for safely validating Unity Editor changes without directly launching Unity, including test project copy checks, Unity refresh, Editor.log compile checks, stale error detection, MCP safety, and process verification.
---

# unity-editor-verification

Use this skill for validating Unity Editor changes safely.

## When To Use

- After changing C#, USS, UXML, icons, or debug fixtures.
- Before claiming Unity compile success.
- When checking MCP availability or Editor state.
- When copying package files into a separate test project.

## Workflow

1. Do not launch or restart Unity directly.
2. Read:
   - `references/compile-check.md`
   - `references/editor-log-check.md`
   - `references/mcp-safety.md`
3. Apply changes to the test project only when the task requires it.
4. Trigger a safe refresh through the already-open Editor when needed.
5. Confirm recent logs, not stale whole-log errors.

## Defaults

- Code-only doc changes do not require Unity compile.
- UI/Unity changes should be verified in the test project when feasible.
- Scene or prefab saves require explicit user confirmation.
