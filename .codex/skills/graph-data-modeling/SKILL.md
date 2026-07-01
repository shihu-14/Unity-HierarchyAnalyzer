---
name: graph-data-modeling
description: Use for graph data model design, stable node identity, edge modeling, expand and collapse state, visibility projections, hidden descendant issue propagation, search indexing, and separation of UI state from core data.
---

# graph-data-modeling

Use this skill for graph model, visibility, search index, and issue propagation design.

## When To Use

- Changing node, edge, group, stack, or issue data.
- Adjusting expand/collapse or visible representative behavior.
- Implementing search indexes or issue propagation.
- Separating UI state from core graph state.

## Workflow

1. Identify the data owner before editing UI behavior.
2. Read:
   - `references/node-edge-model.md`
   - `references/visibility-and-propagation.md`
   - `references/search-indexing.md`
3. Keep graph identity, visibility, selection, and visual style separate.
4. Define behavior for hidden descendants and duplicate visual nodes.
5. Verify with small graphs before relying on large project scans.

## Defaults

- Stable IDs are required for node state.
- Edges should reference data IDs, not VisualElements.
- UI should derive from graph state, not become the source of truth.
