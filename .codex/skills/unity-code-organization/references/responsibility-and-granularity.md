# Responsibility And Granularity

## Responsibility Test

Describe a type or file by the work it performs, the data it owns, and the reason it changes. Prefer a cohesive responsibility with explicit dependencies.

Use these questions:

- Does the type change for more than one independent product or technical reason?
- Do methods operate on the same state and invariants?
- Do consumers use the whole type or unrelated subsets?
- Does the type combine Unity data collection, application coordination, and presentation?
- Can a boundary be tested without introducing an abstraction used only by tests?
- Would a split reduce coupling, or only move private methods between files?

Line count is a signal to inspect responsibility, never a sufficient split reason.

## Split A Class Or File When

- It has independent reasons to change.
- One part owns a distinct state lifecycle or dependency boundary.
- Consumers need separate capabilities and currently depend on unrelated members.
- Unity API access can be isolated behind a small, meaningful reader or scanner boundary.
- A cohesive extracted type has a specific role name and real production value.

Do not split merely to satisfy an arbitrary size, to create one-method wrappers, or to make tests bypass the production design.

## Keep Or Merge Small Types When

- They share the same lifecycle and reason to change.
- They are private or internal implementation details with no independent discovery value.
- They form one cohesive transport or rendering representation.
- Separating them would scatter one concept without reducing coupling.

Avoid merging behavior only because it looks repeated. Duplication is safer than coupling unrelated concepts to the wrong abstraction.

## File Policy

`Project policy`: Use one primary type per file and match the file name to that type.

Review exceptions for nested/private types, tightly coupled enums or values, cohesive internal model groups, and partial files. Do not change an established exception unless the change improves navigation or responsibility clarity without hiding ownership.

## Partial Classes

Use a partial class only to organize one cohesive type. Name parts `Type.Concern.cs`.

Use partial files when:

- The type has one identity and lifecycle but several substantial internal concerns.
- Splitting into independent objects would create artificial coordination or expose internal state.
- Each part can be named by a stable concern such as `Search`, `Layout`, or `ReferenceReader`.

Do not use partial files to hide unrelated responsibilities, circular state ownership, or an unclear public surface. Inspect all parts together before judging the type.

## Role Names

These are repository policies, not Unity or Microsoft official suffix requirements.

| Role | Meaning |
|---|---|
| `Controller` | Coordinate application flow, UI state, selection, and user intent; do not own heavy Unity collection or visual styling |
| `Scanner` | Collect Unity project, scene, asset, serialized, or diagnostic state and convert it into tool data |
| `Reader` | Read through a Unity or external API boundary without deciding broader application flow |
| `Builder` | Assemble a new representation from existing inputs without becoming the long-lived owner of that state |
| `Resolver` | Select or map the best target from candidates using explicit rules |
| `View` | Render state and emit user intent; do not perform project-wide collection |
| `Model` | Represent a defined domain or presentation model boundary, not any arbitrary class with fields |
| `Policy` | Encapsulate a stable decision rule without collecting or coordinating unrelated behavior |
| `Guard` | Enforce or restore a narrow safety invariant |

Reject `Manager` when one of these roles or a domain noun identifies the responsibility more precisely.

## Dependency And Abstraction Placement

Place types with peers that share their responsibility and abstraction level. Preserve the repository direction:

- UI depends on Controller and Core.
- Controller depends on Core and Scanners.
- Scanners depend on Core and Unity Editor APIs.
- Core does not depend on Controller, UI, or Scanners.

Treat this as the current repository architecture, not a universal Unity requirement. Use `unity-editor-extension` when the architecture itself is in scope.
