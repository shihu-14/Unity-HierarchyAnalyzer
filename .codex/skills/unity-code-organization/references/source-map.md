# Official Source And Rule Map

Accessed: 2026-08-06

Use summaries rather than long quotations. Reopen a source and update the access date when materially revising this skill.

## Evidence Classifications

- `Official requirement`: The cited Unity or Microsoft source explicitly uses mandatory guidance. Preserve the source's stated scope.
- `Official recommendation`: The source presents a tip, recommendation, consideration, or adaptable convention.
- `Project policy`: This repository defines the rule; no official source states it directly.
- `Derived guideline`: The rule is an inference from multiple official principles. State that it is an inference.

Do not label a project-specific folder, suffix, or partial-class rule as an official Unity requirement.

## Source Status

| ID | Document | Organization | Sections used | Exact URL | Update status | Supports | Does not support |
|---|---|---|---|---|---|---|---|
| U1 | Naming and code style tips for C# scripting in Unity | Unity Technologies | Introduction; Fields and variables; Classes and interfaces; Methods; Namespaces | https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity | No page-level last-updated date shown; page links Unity 6 guidance and displays 2026 copyright | Consistency, meaningful/searchable names, abbreviations, casing, class/interface/method/namespace recommendations, team-specific style | Repository suffix definitions or automatic removal of `Dependency` |
| U2 | Package layout for UPM packages | Unity Technologies | Package layout tree and folder table | https://docs.unity3d.com/Manual/cus-layout.html | Current page was Unity 6.5, built 2026-08-05; equivalent pages verified for 6000.0 and 6000.4 | UPM top-level `Editor`, `Runtime`, `Tests`, `Samples`, `Documentation`, and asmdef placement | Detailed internal folder axes, Assets-copy migration, or repository-specific namespaces |
| U3 | Naming conventions | Unity Technologies | BEM; Tips: Naming conventions in UI Toolkit | https://docs.unity3d.com/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/naming-conventions.html | Current page was Unity 6.5, built 2026-07-08; equivalent pages verified for 6000.0 and 6000.4 | BEM, readability over brevity, short but unambiguous names, semantic roles, UXML/USS/C# consistency | General C# type naming or a mandatory BEM policy for this repository |
| U4 | Asset metadata | Unity Technologies | Asset IDs; Meta files; Moving and renaming assets | https://docs.unity3d.com/Manual/AssetMetadata.html | Current page was Unity 6.5, built 2026-08-05; equivalent pages verified for 6000.0 and 6000.4 | `.meta` identity, keeping metadata with assets, reference breakage when metadata is lost | A specific git command, commit strategy, or repository rename map |
| M1 | Naming guidelines | Microsoft | In this article | https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines | Last updated 2022-10-04 | Readable functional names and the public/protected API scope of Framework Design Guidelines | Mandatory rules for all internal Unity code |
| M2 | General Naming Conventions | Microsoft | Word Choice; Using Abbreviations and Acronyms | https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/general-naming-conventions | Last updated 2023-10-03; warns that 2008 source material may be outdated | Readability over brevity, meaningful names, abbreviation and contraction guidance | Current internal project policy or Unity-specific naming |
| M3 | Names of Classes, Structs, and Interfaces | Microsoft | General type naming; Names of Generic Type Parameters; Names of Common Types | https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-classes-structs-and-interfaces | Last updated 2025-05-29; warns that 2008 source material may be outdated | Noun phrases, PascalCase, `I` prefix, relationship suffix guidance | Repository meanings for `Data`, `Reader`, `Scanner`, or `Controller` |
| M4 | Names of Namespaces | Microsoft | Namespace hierarchy; Namespaces and Type Name Conflicts | https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces | Last updated 2023-10-03; warns that 2008 source material may be outdated | Stable product/technology hierarchy, namespace/type name conflicts, collision-prone `Node`, `Log`, and `Message` | The final name of any repository type or automatic prefix removal |
| M5 | Common C# code conventions | Microsoft | Introduction; Language guidelines | https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions | Last updated 2025-01-18; explicitly an adaptable example, not an authoritative list | Readability, consistency, collaboration, and adapting conventions to team needs | A requirement to restyle existing repository code |
| M6 | Architectural principles | Microsoft | Separation of concerns; Explicit dependencies; Single responsibility; Don't repeat yourself | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles | Last updated 2023-05-09; written in ASP.NET/Azure context | Separation by work, one reason to change, explicit dependencies, avoiding wrong abstractions | Direct Unity folder, file-count, partial-class, or suffix requirements |

## Source-To-Rule Mapping

| Rule | Classification | Source | Exact section | Repository-specific extension | Confidence |
|---|---|---|---|---|---|
| Use a consistent team style | Official recommendation | U1 | Introduction | Record the selected repository policy instead of treating Unity's examples as universal | High |
| Prefer meaningful, searchable names and avoid unnecessary abbreviation | Official recommendation | U1 | Fields and variables | Evaluate `Dependency` through usages and collisions before removal | High |
| Use noun phrases for classes, `I` for interfaces, and verbs for methods | Official recommendation | U1 | Classes and interfaces; Methods | Adopt as the internal repository default | High |
| Limit Framework Design Guideline requirements to public and protected API | Official requirement | M1 | In this article | Treat internal consistency as project policy | High |
| Prefer readability over brevity and avoid unfamiliar abbreviations | Official requirement for the documented public API scope | M2 | Word Choice; Using Abbreviations and Acronyms | Apply cautiously because the page carries an outdated-content warning | Medium |
| Use noun phrases and clear relationship suffixes for public types | Official requirement for the documented public API scope | M3 | General type naming | Do not infer repository role suffixes from this source | Medium |
| Avoid generic collision-prone public type names such as `Node`, `Log`, and `Message` | Official requirement for the documented public API scope | M4 | Namespaces and Type Name Conflicts | Use as support against mechanical shortening, not as a final naming decision | Medium |
| Adapt conventions to project and team needs | Official recommendation | U1, M5 | Introduction | Preserve established repository style unless an approved policy changes it | High |
| Use BEM and semantic, readable UI Toolkit names | Official recommendation | U3 | BEM; Tips: Naming conventions | Keep UXML, USS, and C# queries synchronized | High |
| Keep assets and matching `.meta` files together when renaming or moving | Official requirement | U4 | Moving and renaming assets | Use paired explicit moves and verify GUIDs | High |
| Use UPM top-level package boundaries when authoring a UPM package | Official recommendation | U2 | Package layout tree and folder table | Keep the current Assets-copy format; do not infer internal folder axes | High |
| Keep sibling folders on one classification axis and comparable abstraction levels | Derived guideline | U1, M6 | Introduction; Separation of concerns; Single responsibility | Document justified Unity and asmdef exceptions | Medium |
| Split by responsibility and avoid the wrong abstraction | Derived guideline | M6 | Separation of concerns; Single responsibility; Don't repeat yourself | Do not use line count as the decision | Medium |
| Match one primary type to its file, with reviewed exceptions | Project policy | No direct official source | N/A | Improve discovery and rename safety; preserve partial and cohesive internal-type exceptions | Medium |
| Use repository-defined role suffixes and partial-class conditions | Project policy | M6 supplies principles only | N/A | Keep `Scanner`, `Reader`, `Builder`, `Resolver`, and `Controller` meanings explicit | Medium |
