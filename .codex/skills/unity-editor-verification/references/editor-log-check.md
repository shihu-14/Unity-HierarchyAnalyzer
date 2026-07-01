# Editor Log Check

## Log Location

- macOS Unity Editor log is usually under:
  - `~/Library/Logs/Unity/Editor.log`

## Recommended Checks

- Use recent tail sections for current validation.
- Search for `error CS`, `Scripts have compiler errors`, `Tundra build success`, and `Mono: successfully reloaded assembly`.
- Distinguish old errors from current errors by surrounding timestamps and recent import lines.
- Look for imported paths when verifying copied package files.

## Common Noise

- Licensing entitlement 404 messages may not indicate compile failure.
- Package test assemblies may log invalid assembly messages.
- Touch gesture warnings can be unrelated UI noise.
- Do not treat unrelated old package logs as task failure.

## Reporting

- Report what was checked and whether current compile/import errors were found.
- If only stale errors exist, say they are stale and cite recent success evidence.
