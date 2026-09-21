# Repository Agent Guide

## Startup

- At the beginning of a new task in this repository, reply with exactly:
  - `AGENTS.mdを読み込みました。`

## Communication

- Reply to the user in Japanese.
- Keep chat concise and action-oriented.
- Code, identifiers, comments, and commit messages must be English.

## Git

- Commit at safe checkpoints after meaningful changes.
- Push all task commits to the remote at completion.
- Commit messages must use one approved English prefix followed by `: `.
- Approved prefixes: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `style`, `build`, `ci`, `perf`, `revert`.
- Add `Co-authored-by: Codex <267193182+codex@users.noreply.github.com>` to new commits with Codex contributions; preserve existing authors and history.
- Do not commit unrelated files. AGENTS and skill files may be committed when the task explicitly asks for them.

## Unity Safety

- Do not launch or restart Unity Editor directly.
- Connect only to an Editor already opened from Unity Hub.
- Ask before any restart, scene save, prefab save, asset delete, or script delete.
- Avoid long synchronous MCP operations. Split scans, asset work, and GameObject generation into small batches.
- After Unity-side work, check recent compile errors, Editor responsiveness, MCP/relay state when relevant, and duplicate Unity Editor processes.
- See `.codex/skills/unity-editor-verification/SKILL.md`.

## Project Boundaries

- This repository contains an Editor-only Unity dependency graph tool.
- Keep responsibilities separated:
  - `Core`: graph, node, edge, issue, and cache data structures plus graph-local operations; it does not depend on Controller, UI, or Scanners, and does not collect Unity state or render UI.
  - `Scanners`: Unity object, asset, serialized reference, and diagnostic collection.
  - `Controller`: scan orchestration, state, selection sync, and UI coordination.
  - `UI`: UI Toolkit graph, node, edge, toolbar, issue panel, USS, and UXML.
  - `Tests`: Edit Mode coverage and intentional broken-reference fixtures; runtime test components remain in their test-only assembly.
- Prefer targeted changes over broad rewrites.
- Keep UI visual changes in USS unless runtime geometry or state requires C#.

## Skill Routing

- Naming, file/folder organization, responsibility, and safe structural refactoring: `.codex/skills/unity-code-organization/SKILL.md`
- Unity Editor extension structure: `.codex/skills/unity-editor-extension/SKILL.md`
- Serialized reference internals: `.codex/skills/unity-reference-analysis/SKILL.md`
- UI Toolkit graph rendering: `.codex/skills/unity-ui-toolkit-graph/SKILL.md`
- Graph data model and propagation: `.codex/skills/graph-data-modeling/SKILL.md`
- Object-linked diagnostics: `.codex/skills/unity-object-diagnostics/SKILL.md`
- Editor performance: `.codex/skills/unity-editor-performance/SKILL.md`
- Verification and MCP safety: `.codex/skills/unity-editor-verification/SKILL.md`
