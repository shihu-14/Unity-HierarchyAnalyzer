# Node and Edge Rendering

## Nodes

- Keep node kind colors distinct enough for quick scanning.
- Do not encode error state by replacing the node kind identity.
- Put issue icons near the central label area for direct issues.
- Put propagated issue icons near dependency or used-by indicators.
- Keep control icons vertically centered in both single and stacked layouts.

## Node Controls

- Use fixed square or circular dimensions for `+`, `-`, and menu controls.
- Preserve icon-to-button ratios when resizing.
- Use USS for base shape and hover state.
- Use C# only for scale-dependent runtime sizing.

## Edges

- Edge meaning should be visible through style or color.
- If edge color reflects node colors, define the blend rule in one place.
- Avoid allocating new meshes or paths every repaint unless geometry changed.
- Keep hit targets usable even when the edge line is thin.

## Highlights

- Highlight duration and fade should be consistent.
- Search highlight should affect only the active match unless product behavior says otherwise.
- Propagated issue highlights should target the visible representative node.
- Avoid permanent highlight states caused by animation loops.

## Icons

- Prefer clean raster icons with transparent backgrounds or Unity built-in icons.
- Keep icon dimensions consistent between counters and list rows when the design calls for it.
- Do not stretch icons non-uniformly unless intentionally matching a design.
