# WildGlow 0.4.11 review validation

Release compilation has zero warnings/errors. The full existing suite passes: 1,368,937 motion/art, 34,937 behavior, 9,598 grouping, 30,009 surface, 31 fruit and 135 material-lifecycle checks. The binary verifier resolves 323 game references/hooks and validates 58 styles and 165 aliases.

New regressions cover group merge/split ownership, rebuilt mining renderers, restoring materials before reassignment, preserving replacements owned by another mod, and bounded temporary-material lifetime. These tests exercise production material logic with host doubles; GPU brightness, bloom and actual light spill still need in-game checks.

## Historical checks

### WildGlow 0.4.8 validation

Version 0.4.8 updates the README voice and release metadata.

## Apple-tree attachment fix

The user reported effects at the apple-tree base while the apples remained unlit. The current game log reports WildGlow 0.4.3, which predates real light spill. Separately, the prepared code still fell back to tree colliders because Valharvest's combined apple mesh is not readable at runtime.

The new build uses nine verified fruit-surface points, limits material emission to Pickable.m_hideWhenPicked (the apples mesh), keeps each tree separate from neighboring effects, and places up to four budgeted spill lights among the fruit. Particles stay close to each apple. Hidden fruit invalidates the effect; regrowth restores it. Trunk, leaves and unsupported replacement meshes do not receive fruit attachment points.

Completed checks:

- Release build: zero warnings/errors.
- Binary API verification: 323 references/hooks, 58 styles and 165 aliases, zero failures.
- Fruit attachment/lifecycle harness: 31 checks, including rotation/nonuniform scale, harvest, regrowth and changed/missing mesh handling.
- Material and light harness: 31 checks, including fruit-only material selection, spill placement, shared intensity and cleanup.
- Existing surface/layout regressions: 30,009 checks passed.
- Installed Valharvest 3.3.4 asset inspection: both apple_tree variants use the apples harvest mesh, and all nine attachment coordinates match separate fruit components. See assets/compatibility/valharvest-apples.md.

The fix is prepared locally and has not been installed or observed in-game. After installation, confirm `wildglow_status` reports 0.4.8, inspect both tree variants by day/night, harvest and revisit after regrowth, and check nearby ground pickups, frame time and light spill with the user's graphics settings. Transform-based anchors do not simulate shader wind deformation.

## Earlier lighting work

The following records the previous 0.4.5 validation and remaining general lighting playtest; it is retained as historical context.

### WildGlow 0.4.5 validation

This build addresses overbright ore/bush self-emission with no surrounding illumination, dim mushrooms and unwanted greydwarf-nest effects. It applies the lighting path to all eligible styles. Mote motion and artwork are unchanged. See INSTALLATION.md for the last completed installation.

## Completed

- Release build against the installed Valheim/Unity/BepInEx assemblies: zero warnings/errors.
- Binary API verifier: 321 references/hooks, 58 styles and 165 aliases resolved with no failures. Real Unity Light linkage is present; retired surface texture/overlay types remain absent.
- Surface/layout tests: 30,009 checks covering six-face ore sampling, mesh triangles, tree/nest exclusions, light budgets, sources clear of model bounds, whole-bounds range plus an apron, and total intensity independent of source count. Geometry tests cover small, large, tall and elongated bounds. They do not simulate rendered attenuation or prove the absence of hotspots.
- Material/light lifecycle: 25 checks using the production classes with Unity test doubles. They cover shared material identity during mining, original textures/tiling, original-asset ownership, restore/re-enable, per-style emission off, brighter mushroom setting, real light creation, budget shrink, hide/resume, zero spill, missing-model suppression and complete cleanup.
- Behavior tests: 34,937 assertions across all motion families, including the mushroom rise fix and unchanged dandelion motion; exported lighting defaults for every one of the 58 styles. Automatic styles inherit fallback settings.
- Preview/runtime parity: 240 trajectory coordinates agree. Offline DOM checks cover all 58 effect switches, lighting defaults in config export, both preview entry points, day/night and pause. The web motion study does not simulate Unity lighting.
- Structure filter regression checks cover village roofs/walls/fences, lox_ribs bone props, doors, building components and generic architectural parts. They retain chests/hives/pickables beneath building parents, ores and collectible totems. Exclusions run before DropOnDestroyed and replacement-prefab loot classification.
- Greydwarf nest prefab confirmed in the installed SoftRef manifest as Spawner_GreydwarfNest. That exact name and underscore variants are excluded before classification; collectible Ancient Seeds remain eligible when found as world pickups (not ItemDrop objects).

## Lighting behavior

Model emission retains the original shaders, albedo tiling and leaf transparency. At the same global AreaGlow, default mushroom emission is 1.25 times 0.4.3, berry emission 0.065 times and geological emission 0.085 times. Other styles use 0.15 times. These ratios describe the shader contribution, not perceived screen brightness. SurfaceGlow can be adjusted independently per style.

Real spill lights sit above visible model bounds, with broad overlapping ranges and a shared intensity budget. Small groups request one source, large groups up to four. Nearest groups receive one before extra coverage is allocated, with a default total cap of 24. Buried anchors are omitted from lighting bounds when an exposed portion exists; underground rooms retain a fallback. The working mote anchors are unchanged. Bounds refresh with target grouping and partial mining.

Lighting.SpillIntensity (default 0.6) and SpillRange (3 m extra reach) control surrounding illumination. Style.<id>.LightSpill scales it per style. Appearance.AreaGlow controls self-emission. Particle Brightness is independent. Enabled=false removes the entire effect. Config updates rebuild effects. Legacy point-light settings are ignored.

## Remaining playtest

1. Confirm 0.4.5 using wildglow_status or the BepInEx log; status now reports active spill sources.
2. At night, inspect copper and small ore from several sides. The surface should retain its shading while nearby grass, terrain and the character receive gentle light. Inspect overlap/hotspots and after partial mining.
3. Compare mushrooms and berry bushes: mushrooms should be more readable, bushes less self-bright, with illumination around both. Check other eligible plants, hives, chests and fallback collectibles in daytime and at night.
4. Verify structure (including Fuling roofs/walls and bone gates), nest, tree/shrub, wildlife, common-item and dropped-item exclusions. Mote appearance/movement should remain unchanged.
5. Test individual Enabled, SurfaceGlow=0 and LightSpill=0; re-enable, harvest, deplete, leave range and unload the world. Check many nearby objects for performance and budget changes.

Spill approximates diffuse emission using broad, shadowless point lights, not emissive-mesh global illumination. Thin nearby walls can receive or transmit unwanted spill. Models beyond the nearest-light budget retain their motes/self-emission. Unsupported material shaders may lack self-emission but still receive actual illumination. In-game appearance and performance are not yet verified for this build.
