# WildGlow development workspace

Version 0.4.5 adds real surrounding illumination sized to each eligible model, dims ore/bush self-emission, brightens mushrooms and excludes greydwarf nests and structures (including Fuling huts and bone rib gates). Motes are unchanged. Each style has independent SurfaceGlow and LightSpill controls. See VALIDATION.md for verification and INSTALLATION.md for the last completed installation.

See [package README](packaging/README.md) for behavior, configuration and local import instructions. See [validation](VALIDATION.md) for checks and remaining playtest work.

Build with `DOTNET=/path/to/dotnet bash scripts/build.sh`. Verification projects are `tools/Verify`, `tools/GroupingTests`, `tools/MotionTests` and `tools/BehaviorTests` and `tools/SurfaceTests`. Run BehaviorTests with this workspace as its argument to export behavior mappings and parity fixtures, then run `node tools/BehaviorTests/check-preview.cjs`. `python3 scripts/build_preview.py` generates the inline motion study and self-contained `previews/index.html` (the old template URL also works); `python3 scripts/package.py` creates both manual and r2modman ZIPs.

The icon master is in `assets/wildglow-icon-master-v2.png`; `packaging/icon.png` is its 256×256 import version. The revised icon adds WILDGLOW text and uses extracted Valheim copper ore, carrot, mushroom and thistle sprites as image-generation references; see `assets/game-items/SOURCES.md`. The prior AI concept is art direction; the runtime uses procedural sprite atlases and local meshes.

`python3 scripts/update_installed_copies.py` checks the prepared update without writing to installations. After approval, `--apply` backs up and updates the existing game and r2modman Mods-profile copies together, preserves the registered local-WildGlow identity and other mods, and disables the legacy dropped-item setting while preserving appearance settings. Valheim and r2modman must be closed. The older game-only installer is retained for reference.

Packaging references: [Thunderstore packaging](https://wiki.thunderstore.io/mods/packaging-your-mods), [r2modman package routing](https://github.com/ebkr/r2modmanPlus/wiki/Structuring-your-Thunderstore-package).

Individual effects can be disabled using `Enabled = false` in a `[Style.<id>]` section. All 58 switches are listed in `WildGlow-effects.cfg`. Reload with `wildglow_reload`, or edit while the game is closed. Disabled effects are filtered before grouping and consume no light/particle budget.

## Feedback and support

Join [Vassteels workshop](https://discord.gg/abN7R2tWyK) for bug reports, suggestions, config help and screenshots. Include your mod version, game version and steps to reproduce when reporting a problem.
