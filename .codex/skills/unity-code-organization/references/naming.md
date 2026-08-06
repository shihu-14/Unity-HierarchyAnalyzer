# Naming Guidance

## Evidence Scope

Apply Microsoft Framework Design Guidelines as requirements only to public and protected APIs. Apply internal naming rules as repository policy supported by current Unity and C# recommendations. See `source-map.md` before labeling a rule official.

The objective is not the shortest name. Choose the shortest name that remains unambiguous, meaningful, searchable, and accurate at its call sites.

## Casing And Identifier Roles

| Identifier | Repository default |
|---|---|
| Class, struct, enum, property, method | PascalCase |
| Interface | PascalCase with an `I` prefix |
| Parameter and local variable | camelCase |
| Private field | Match the repository's existing camelCase style |
| Boolean | Use a question-like `Is`, `Has`, `Can`, or another accurate predicate |
| Class or struct | Use a noun or noun phrase |
| Method | Start with a verb or verb phrase |
| File | Match its primary type, subject to the documented exceptions |
| Namespace | Use PascalCase segments organized by stable product and feature concepts |

Avoid contractions and unfamiliar abbreviations. Allow conventional technical terms such as `UI`, `ID`, `GUID`, `UXML`, and `USS` when they improve recognition and match existing usage.

## Naming Decision Sequence

1. State the subject's responsibility without using its current name.
2. Identify its namespace, folder, base type, interfaces, consumers, and common call sites.
3. Mark words already supplied by context.
4. Remove a contextual word only if the remaining name stays clear when imported with common neighboring namespaces.
5. Search for collisions with Unity, .NET, repository types, UXML/USS identifiers, and likely future sibling concepts.
6. Prefer semantic role words over implementation or presentation details.
7. Compare complete candidates before selecting a rename.

## Contextual Prefixes And Generic Names

`Project policy`: Omit a project, namespace, or folder word only when the type remains unambiguous and searchable outside its declaration file.

`Derived guideline`: Retain a qualifier when removing it would create a generic or collision-prone type such as `Node`, `Element`, `Log`, or `Message`.

Do not remove `Dependency` mechanically. Check whether it distinguishes dependency graph data from UI render nodes, Unity objects, or other graph concepts.

Evaluate each candidate using:

- Semantic role and domain precision
- Common call-site readability
- Namespace import and collision risk
- Repository-wide search precision
- Relationship to sibling types
- Public, serialized, and reflection compatibility impact

## Ambiguous Terms And Suffixes

| Term | Project policy |
|---|---|
| `Data` | Use only for a value-oriented snapshot, record, or transfer representation when no more specific domain role is clearer. Do not assume every type with methods must lose `Data`; inspect its invariants and consumers. |
| `Manager` | Do not use it to hide unrelated coordination, state ownership, and data access. Name the primary responsibility. |
| `Helper` / `Utils` | First try a concrete role such as `Reader`, `Formatter`, `Resolver`, `Policy`, or `Guard`. Do not create a dumping ground. |
| `Custom` | Use only when a meaningful distinction from a standard or third-party type exists and no semantic qualifier is clearer. |
| `Model` | Use only when the type represents a defined model boundary. Do not use it as a synonym for any data-bearing class. |

Apply role suffix meanings from `responsibility-and-granularity.md` consistently.

## File And Primary Type Names

`Project policy`: Put one primary type in a file and match the file name to that type.

Allow these reviewed exceptions:

- Small private or nested implementation types that have no independent discovery value
- A small enum or tightly coupled value type that changes with the primary type
- A cohesive group of internal render or transport types when splitting would reduce discoverability
- Partial files named `PrimaryType.Concern.cs`

For `MonoBehaviour` scripts, preserve Unity's file/type naming requirement. Treat `ScriptableObject` and serialized types as compatibility-sensitive even when Unity permits a structural edit.

## UI Toolkit Names

Use BEM-style USS class names where they match existing UI Toolkit conventions. Prefer semantic roles and relationships over visual details. Omit `Button` or `Label` only when the remaining UXML/USS name stays clear. Keep UXML names, USS classes, and C# queries synchronized and searchable.

## Candidate Example: `DependencyNodeData`

Do not use this example as a predetermined rename. Inspect the current type and all usages first.

| Candidate | Strength | Risk to investigate |
|---|---|---|
| `DependencyNodeData` | Explicitly identifies dependency-domain node data | `Data` may be redundant if the type owns meaningful behavior or invariants |
| `DependencyNode` | Retains domain precision and removes a generic suffix | May imply the domain entity rather than its stored representation |
| `GraphNode` | Communicates graph membership | May collide conceptually with UI render nodes or other graphs |
| `NodeModel` | Signals a representation | Generic `Node` and ambiguous `Model` reduce search precision |
| `Node` | Short | Too generic and collision-prone; reject unless a narrowly scoped context proves otherwise |

Report why a candidate wins at real call sites. Do not select a rename merely because it removes characters.
