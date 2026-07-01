# Visibility and Issue Propagation

## Visibility

- Visible graph is a projection of full graph data.
- Hidden nodes should remain addressable by stable ID.
- Collapsed descendants should not be treated as deleted.
- Duplicate visual appearances should map back to the same source identity.

## Propagation

- Direct issues belong to the source node that owns the problem.
- If that source node is hidden, propagate the issue icon to the nearest visible ancestor or representative.
- When the source becomes visible, remove incorrect propagated display from the parent.
- Apply the same propagation rules to warning and error severities.

## Click Behavior

- Issue row click should navigate to the direct source when it is visible.
- If the source is hidden, navigate to the nearest visible representative and preserve issue context.
- Script and asset issues should not be forced onto unrelated parent objects.
- Object issues should not highlight only an ancestor when the root cause is visible.

## Cache Rules

- Recompute propagation after expand, collapse, filter, search visibility, or full rescan.
- Keep direct issue data immutable during propagation.
- Store propagated display as derived state.
