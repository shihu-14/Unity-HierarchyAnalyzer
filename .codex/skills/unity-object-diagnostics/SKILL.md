---
name: unity-object-diagnostics
description: Use for Unity warning and error diagnostics that map meaningfully to graph nodes, including missing references, missing components, console-linked script or asset issues, prefab or scene serialization warnings, issue severity, propagation, and debug fixtures.
---

# unity-object-diagnostics

Use this skill for warning/error collection that meaningfully maps to graph nodes.

## When To Use

- Adding or changing issue scanners.
- Deciding whether a Unity warning/error belongs in the graph.
- Editing issue panel rows, counts, icons, propagation, or navigation.
- Creating debug fixtures for diagnostics.

## Workflow

1. Include only diagnostics that can map to a meaningful node.
2. Read:
   - `references/diagnostic-scope.md`
   - `references/issue-severity.md`
   - `references/demo-fixtures.md`
3. Keep issue text close to Unity Console wording when the issue came from the console.
4. Keep direct issue target and propagated display target separate.
5. Verify warning and error behavior independently.

## Defaults

- Direct issues use the source node kind/icon.
- Propagated issues show severity near dependency or used-by context.
- Issue panel rows should navigate to the best visible graph target.
