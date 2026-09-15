# Valharvest apple attachment compatibility

Inspected the locally installed Valharvest 3.3.4 bundle on 2026-09-14 after a playtest screenshot showed WildGlow motes at the apple-tree base. The game log reported WildGlow 0.4.3, which also predates real spill lights. The attachment fallback was still present in the prepared 0.4.6 source.

Both `apple_tree` and `apple_tree_1` use Pickable.m_hideWhenPicked to reference `beech/apples`. This child has a single MeshRenderer and an `apple` mesh containing nine disconnected apple components. The mesh has 1,792 vertices and is not readable at runtime. The leaves and trunk are separate renderers. Sampling only readable meshes therefore failed, and the old collider fallback attached effects to the tree body.

`FruitSurface.cs` stores one top surface point per apple in that renderer's local coordinates. These points were derived from connected triangle components after welding duplicated seam vertices to five decimal places. The live renderer transform supplies position, rotation and scale. Mesh name, vertex count and local bounds are checked before applying these points. Unsupported assets produce no fruit anchors; there is no trunk fallback. The compatibility check cannot detect every asset revision that happens to preserve the same metadata.

The fruit renderer alone receives material emission. Harvesting hides that renderer; its anchors immediately become invalid, and effects resume after regrowth. Fruit trees own independent groups. Up to four spatially separated apple attachment points receive shadowless pixel lights, within the existing global budget and with shared total intensity. Particle motion remains close to the individual apples rather than merging into a central rising column.

## Source fingerprints

- Installed `Valharvest.dll` SHA256: `7e5ba21a7ffa6c360d23936e569276fc1bc47484802badd566ce99ec0e8e0add`
- Embedded `Valharvest.Bundles.valharvest` SHA256: `b244cbf5e0be84e205a362408ca5b05804318b3bd3d4163101c25f27f7f55a77`

Only nine attachment coordinates and identifying metadata are included. Models, textures and third-party binaries are not redistributed.

## Verification

`tools/FruitTests` runs production attachment code against small Unity substitutes: nine anchors, translation/rotation/nonuniform scale, hidden/disabled fruit, regrowth, changed mesh metadata, sapling exclusions and missing meshes. `tools/EmissionTests` verifies fruit-only material selection, light positions, shared intensity and cleanup.

For the installed-asset check, extract the named embedded resource using Mono.Cecil's `EmbeddedResource.GetResourceData`, then run with a Python environment containing UnityPy:

```bash
python scripts/verify_valharvest_apples.py /path/to/Valharvest.Bundles.valharvest
```

This verifies both prefab harvest references and all nine attachment points against the actual mesh. In-game appearance, wind deformation, brightness and frame time still need visual verification. Static attachment points follow the renderer transform; vertex-shader wind deformation is not simulated.
