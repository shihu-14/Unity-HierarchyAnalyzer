# Project Structure Guidance

## Evidence Scope

- Only rules mapped as `Official requirement` or `Official recommendation` in `source-map.md` are official, and only within the cited source scope.
- Repository workflows, thresholds, role meanings, and commit practices are `Project policy`.
- Inferences assembled from multiple official principles are `Derived guideline`.
- Never present a project policy as a direct Unity or Microsoft requirement.

## Current Distribution Boundary

This repository uses Assets-copy distribution, not UPM. Preserve this layout unless migration is explicitly requested:

```text
Assets/
└── DependencyAnalyzer/
    ├── Editor/
    └── Tests/
```

Use Unity's official UPM layout only as a version-specific reference for package-level boundaries. Unity 6000.0's recommended custom-package layout uses `Samples~` and `Documentation~` and explains that the trailing tilde makes Unity ignore their contents and `.meta` tracking. Unity 6000.4's package-creation layout instead shows `Samples` and `Documentation` without tildes. Both describe `Editor`, `Runtime`, `Tests/Editor`, `Tests/Runtime`, and asmdef placement, but neither prescribes the detailed classification axis inside each folder.

This difference is not a basis for changing the current Assets-copy layout. Before any future UPM conversion, recheck the target Unity version and how export or installation handles sample and documentation folders and trailing tildes.

## Current Repository Responsibilities

Inspect contents and usages before changing any folder. The current intended roles are:

| Area | Current responsibility |
|---|---|
| `Editor/Core` | Graph, node, edge, issue, and cache data structures plus graph-local operations |
| `Editor/Scanners` | Read Scene, serialized reference, AssetDatabase, and Console state into tool data |
| `Editor/Controller` | Coordinate scan lifecycle, UI state, search, selection, and issue routing |
| `Editor/UI` | Render and interact through UI Toolkit |
| `Editor/Settings` | Store analyzer configuration and expose Project Settings UI |
| `Editor/Tests` | Edit Mode Tests and fixed broken-data fixtures |
| `Tests/Runtime` | Test-only components that must compile outside the Editor assembly to attach to GameObjects |
| `Editor/Utils` | Current cross-cutting utilities; evaluate each type's actual consumers before proposing a replacement |

The production assembly is Editor-only. The runtime test-fixture assembly is not production runtime functionality.

## Folder Classification

`Derived guideline`: Use one primary classification axis among siblings and keep siblings at comparable abstraction levels.

Evaluate a folder level in this order:

1. List every child and its actual responsibility.
2. Name the current axis, such as layer, feature, platform, lifecycle, or artifact type.
3. Identify children that use another axis or sit at a different abstraction level.
4. Check dependency direction and common change reasons.
5. Keep an exception when Unity compilation, asmdef, asset import, or test-fixture constraints justify it.
6. Document the reason before proposing a move.

Do not mix technical layers and features casually at the same level. A mixed layout can remain when it is small, established, and easier to navigate than an artificial hierarchy.

## New Folder Threshold

`Project policy`: Create a folder when it has at least two cohesive files or represents a clear durable boundary.

Allow a one-file folder for:

- An asmdef or Unity special-folder boundary
- Fixed fixtures, resources, or assets with distinct import behavior
- A responsibility expected to gain a second peer in the approved change
- Isolation required to prevent an invalid dependency

Do not create `Misc`, `Common`, `Helpers`, or `Utils` as an unbounded destination. State the responsibility the new folder owns.

## Namespace And Assembly Boundaries

- Keep stable product and feature concepts in namespace segments.
- Avoid giving a namespace and a type the same name.
- Prefer folder/namespace correspondence when it improves navigation, but do not rename mechanically around Unity special folders.
- Treat an asmdef as a compilation and dependency boundary, not a cosmetic folder organizer.
- Do not add or split asmdefs without explicit approval and a demonstrated dependency or compilation need.
- Check `InternalsVisibleTo`, test references, platform constraints, and root namespaces before changing an asmdef or namespace.

## Test Layout

Mirror production structure when it makes the tested responsibility easier to find. Do not mirror folders mechanically when a test spans layers or when Unity requires a fixture to compile in another assembly. Keep fixed broken assets separate from generated test data.

## Repository Evaluation Output

For each reviewed folder, report:

- Current classification axis
- Child responsibilities and abstraction levels
- Dependency direction
- Unity or asmdef constraints
- Whether the existing placement is coherent
- Any proposed change and its evidence classification

Do not finalize rename or move candidates until the naming and responsibility rules are agreed.
