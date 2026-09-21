# Search Indexing

## Current Search Contract

- Match only node display names using case-insensitive contiguous substrings.
- Trim the query; empty or whitespace-only queries have no results.
- Treat scoped-looking text such as `type:Audio` literally. Do not restore path, type, label, kind, or missing-state query syntax.
- Preserve original text for display and graph node order for result navigation.
- Keep the selected result by stable node ID when it still matches.

## Navigation

- Enter advances one match; Shift+Enter and previous navigation move backward.
- Navigation wraps at either end; counters show the current index and total.
- Suggestions show matching names with type/path detail, without using that detail for matching.
- Search highlights and focuses matches while preserving nonmatching nodes.
- `DependencyGraphView.SetSearch(query, focusCurrent)` does not filter visibility.
- Search controls own input and suggestion rendering; the Controller coordinates graph navigation.
