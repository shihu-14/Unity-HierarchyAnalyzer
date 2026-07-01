# Compile Check

## Repository Checks

- Run `git diff --check` after text edits.
- Inspect staged files before commit.
- Keep project management files out of routine commits unless explicitly requested.

## Unity Checks

- Wait until Unity is not compiling or importing.
- Use the already-open Unity Hub Editor.
- Trigger `Assets > Refresh` only when copied files are not picked up naturally.
- Do not use direct Unity process launch for validation.

## Error Reading

- Unity logs retain old compile errors.
- Check recent tail output before concluding current compile failed.
- Confirm `Tundra build success` or `Mono: successfully reloaded assembly` when relevant.
- If current errors remain, fix them before reporting success.

## Non-Code Changes

- Pure documentation changes do not require Unity compile.
- USS, UXML, icon, or C# changes should be verified through Unity import/compile when possible.
