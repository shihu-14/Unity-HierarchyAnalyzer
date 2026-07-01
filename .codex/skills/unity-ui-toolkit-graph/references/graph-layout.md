# Graph Layout Guidance

## Layout Ownership

- Data model decides what exists.
- Controller decides what is visible.
- Graph view decides where visible items are placed.
- Node and edge views render assigned geometry.

## Node Placement

- Keep parent-child spacing predictable.
- Apply scale rules consistently by depth.
- Avoid letting dynamic labels resize fixed-format controls.
- Maintain stable positions during expand/collapse when possible.

## Stacked Nodes

- A stacked node should look like the same node repeated behind the front node.
- Back layers must not leak through the front layer.
- Highlight should cover the full stacked visual, not only the front node.
- Edges from stacked representations should originate from the intended visual layer.

## Search and Focus

- Search should move focus to the current match, not all matches.
- Enter should advance once per key action.
- Search counters and navigation icons should align to the search field center.
- Clicking outside the field should return focus to the graph when appropriate.

## Issue Panel

- The issue panel should be resizable without breaking graph layout.
- Toggle affordances should match toolbar icon style.
- Rows should map back to the most meaningful node target.
