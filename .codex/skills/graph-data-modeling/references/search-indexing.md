# Search Indexing

## Indexed Fields

- Name, path, type, label, kind, and missing state are common fields.
- Keep user-visible labels separate from exact lookup keys.
- Normalize case and whitespace consistently.
- Preserve original text for display.

## Query Terms

- Support scoped terms only when the data exists.
- Examples: `name:`, `path:`, `type:`, `label:`, `kind:`, `missing:true`.
- Unknown scopes should fail softly or be ignored by documented behavior.
- Avoid adding search syntax that cannot be tested.

## Navigation

- Maintain a stable match order for a given graph state.
- Enter advances one match.
- Shift+Enter or up navigation moves backward.
- Counter should show current index and total matches.

## Filtering

- Filtering should be a visibility projection.
- It should preserve parent context needed to understand matches.
- It must update propagated issues after visibility changes.
- Do not mutate source graph data when filtering.
