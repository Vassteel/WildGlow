# Changelog

## 0.4.10

- Updated README testing-status wording.


## 0.4.9

- Shortened the README and gave the gull a pitch specific to this mod.


## 0.4.8

- Rewrote the README in the voice of a Viking gull selling a well-used longship. Installation, controls and testing status remain documented.


## 0.4.7

- Attach Valharvest apple-tree particles and surface glow to the nine apples, with light spill originating among the fruit.
- Keep fruit-tree effects separate from nearby plants and remove them when harvested; restore them on regrowth.
- Handle the installed non-readable apple mesh without falling back to trunk colliders.

## 0.4.6

- New Viking gull icon artwork.
- Shorter README with GitHub and Discord links.

## 0.4.5

Lighting changes.

## 0.4.4

Lighting changes.

## 0.4.3
- Fixed mushroom spores bunching below a faster column: both now share full-height upward motion. Dandelion movement is unchanged.
- Smaller translucent spore flecks, shorter tails and no hard spore glints.
- Replaced all local point lights and glow patches with whole-model material emission using original texture colors and tiling.
- Preserved original shaders, foliage alpha clipping, mesh geometry and material-slot identity; original materials restore when the effect ends.
- Excluded beech/pine/fir seedlings, small/dead/shrub variants, generic shrubs and tree/log components.
- Retired LightRange, LightIntensity and MaxAreaLights controls; AreaGlow now controls model emission.

## 0.4.2
- Removed ore surface textures, fracture lines and added crystal geometry.
- Preserved working ore mote attachment and sparse vertical columns.
- Distributed soft glow and real illumination over model surfaces, with up to four lights per large model and a strict global light budget.
- Extended attachments to readable world-collectible meshes, with physical-surface fallback.
- Excluded all dropped ItemDrop objects even when a legacy config enables loose items.
- Updated wildglow_status to report model anchors and active surface lights.

## 0.4.1
- Ore fracture/inclusion texture covers the actual intact and mined 3D meshes, following live mesh rebuilds.
- Surface sampling covers all six sides; crystal facets, flecks and trails follow local surface normals.
- Vertical columns start on upper surfaces, preserving a sparse beacon above the deposit.
- Mined or hidden mesh parts lose their overlay; effect disabling removes all surface layers.
- Added wildglow_status diagnostics for loaded version and nearby ore coverage.
- Added Vassteels workshop feedback invite to package metadata and documentation.

## 0.4.0
- Individual Enabled switches for all 58 effect styles, including fallback items; disabled items are excluded before grouping.
- Self-contained preview entry points with independent on/off controls and config export.
- Ten motion families alongside retained vertical columns; default main mote count reduced from 64 to 38.
- Automatic day/night presentation and a manual comparison override.
- Collider-attached ore effects, fracture veins and crystal facets spread over exposed deposits.
- Independent deposit grouping and Fir Tree exclusions.
- Thunderstore-format package, dependency manifest and 256x256 icon for r2modman local imports.

## 0.3.0
- Doubled height, wider real lights, proximity grouping and wildlife filtering.

## 0.2.1
- Tuned trails/twist/glow and removed flint and Surtling core effects.
