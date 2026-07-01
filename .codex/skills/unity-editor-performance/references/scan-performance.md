# Scan Performance

## Main Thread

- Unity object access usually happens on the Editor main thread.
- Keep each scan slice bounded.
- Yield between batches when scanning many objects or properties.
- Avoid nested full-project traversals inside per-object loops.

## SerializedProperty

- Traversal can be expensive.
- Skip known irrelevant component types early.
- Track visited identities to avoid cycles.
- Cache repeated asset path, type, label, and GUID lookups.

## AssetDatabase

- Batch lookups when possible.
- Do not call refresh as part of read-only scanning.
- Avoid loading asset contents when metadata is enough.
- Treat package and built-in assets as lower-priority unless visible in the graph.

## Cancellation

- Long scans should be cancelable or naturally chunked.
- Cancellation should leave prior valid graph data intact when possible.
- UI progress should update without forcing layout thrash.
