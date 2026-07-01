---
name: unity-ui-toolkit-graph
description: Use for Unity UI Toolkit graph interfaces, including node and edge rendering, stacked nodes, graph layout, search UI, issue badges, highlight animations, USS styling, and interaction performance.
---

# unity-ui-toolkit-graph

Use this skill for UI Toolkit graph, node, edge, search, and issue display work.

## When To Use

- Editing graph rendering, node controls, edge visuals, or stacked node visuals.
- Changing search UI, highlight behavior, issue badges, or animation.
- Tuning USS and C# responsibility split.
- Improving graph interaction performance.

## Workflow

1. Read existing USS and VisualElement construction before editing.
2. Use the relevant reference:
   - `references/graph-layout.md`
   - `references/node-edge-rendering.md`
   - `references/animation-and-performance.md`
3. Keep static visuals in USS and runtime dimensions in C#.
4. Verify alignment, size, and animation with the real Unity UI when possible.
5. Avoid changing scanner or core data to solve a pure visual issue.

## Defaults

- Use stable dimensions for buttons, icons, counters, and node controls.
- Prefer class toggles over many inline style updates.
- Keep graph UI responsive during expand, collapse, search, and scan progress.
