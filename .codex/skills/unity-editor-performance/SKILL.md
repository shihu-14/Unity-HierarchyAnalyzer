---
name: unity-editor-performance
description: Use for Unity Editor extension performance work, including scan chunking, AssetDatabase and SerializedProperty cost, caching, lazy evaluation, UI Toolkit layout and repaint reduction, search performance, and MCP-safe operation sizing.
---

# unity-editor-performance

Use this skill for Unity Editor extension performance and responsiveness.

## When To Use

- Scan, expand/collapse, search, issue panel, or graph interaction feels slow.
- Code touches AssetDatabase, SerializedProperty traversal, reflection, or large UI rebuilds.
- MCP operations might block the Editor.
- Large scenes or many assets are involved.

## Workflow

1. Identify whether the cost is scan, data transform, layout, repaint, search, or selection sync.
2. Read:
   - `references/scan-performance.md`
   - `references/ui-toolkit-performance.md`
   - `references/cache-strategy.md`
3. Optimize hot paths before adding new abstractions.
4. Prefer chunking and cached derived state over blocking full recomputation.
5. Verify on small and larger graphs.

## Defaults

- Keep Editor main thread work bounded.
- Avoid full graph rebuilds for local interactions.
- Cache Unity lookup data carefully and invalidate on refresh/rescan.
