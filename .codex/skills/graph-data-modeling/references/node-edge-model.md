# Node and Edge Model

## Nodes

- A node represents a stable graph entity.
- It should have identity, display metadata, kind, optional Unity lookup data, and issue summaries.
- Do not store VisualElement state in core node data.
- Keep raw Unity object references optional and short-lived.

## Edges

- An edge represents a typed relationship between two node identities.
- Edge kind should be explicit, such as hierarchy, inspector, dependency, or used-by.
- Edge visual color is derived from data but is not part of identity.
- Duplicate edges should be normalized unless duplicate relationships are meaningful.

## Derived Views

- A visual node can represent a real node, a stacked group, or a visible representative.
- Visual IDs and data IDs may differ.
- Store mapping between visible elements and source nodes in controller/UI state.

## State

- Expand/collapse state belongs outside core identity.
- Search focus belongs outside core identity.
- Selection sync state belongs outside scanner output.
- Issue propagation can be cached, but must be invalidated when visibility changes.
