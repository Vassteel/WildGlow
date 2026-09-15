"""Package only our plugin and documentation, never the game's proprietary assemblies."""
from pathlib import Path
import hashlib
import json
import struct
import shutil
import zipfile

root = Path(__file__).resolve().parents[1]
dist = root / 'dist'
manifest = json.loads((root / 'packaging/manifest.json').read_text())
version = manifest['version_number']
package = dist / ('WildGlow-' + version)
plugin_dir = package / 'BepInEx/plugins/WildGlow'
plugin_dir.mkdir(parents=True, exist_ok=True)
binary = root / 'WildGlow/bin/Release/net472/WildGlow.dll'
shutil.copy2(binary, plugin_dir / binary.name)
for name in ('README.md', 'EFFECTS.md', 'VALIDATION.md', 'WildGlow-tuning.cfg', 'WildGlow-effects.cfg'):
    shutil.copy2(root / name, package / name)
for name in ('manifest.json', 'icon.png', 'README.md', 'CHANGELOG.md'):
    shutil.copy2(root / 'packaging' / name, package / name)
assert struct.unpack('>II', (package / 'icon.png').read_bytes()[16:24]) == (256, 256)
assert len(manifest['description']) <= 250
with zipfile.ZipFile(dist / f'WildGlow-{version}.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(package.rglob('*')):
        if path.is_file():
            archive.write(path, path.relative_to(package))
digest = hashlib.sha256(binary.read_bytes()).hexdigest()
(dist / 'SHA256SUMS.txt').write_text(f'{digest}  WildGlow-{version}/BepInEx/plugins/WildGlow/WildGlow.dll\n')
print(f'{dist / f"WildGlow-{version}.zip"}\nDLL SHA-256: {digest}')

manager_zip = dist / f'Local-WildGlow-{version}.zip'
shutil.copy2(dist / f'WildGlow-{version}.zip', manager_zip)
print('r2modman local import: ' + str(manager_zip))

# Public uploads use the documented five-field manifest; local imports also need author.
public_zip = dist / f'WildGlow-{version}-thunderstore.zip'
public_manifest = {k: v for k, v in manifest.items() if k != 'author'}
with zipfile.ZipFile(public_zip, 'w', zipfile.ZIP_DEFLATED) as archive:
    archive.writestr('manifest.json', json.dumps(public_manifest, indent=2) + '\n')
    for name in ('README.md', 'CHANGELOG.md', 'icon.png'):
        archive.write(root / 'packaging' / name, name)
    archive.write(binary, 'BepInEx/plugins/WildGlow/WildGlow.dll')
    for name in ('WildGlow-effects.cfg', 'WildGlow-tuning.cfg', 'EFFECTS.md'):
        archive.write(root / name, name)
with zipfile.ZipFile(public_zip) as archive:
    assert archive.testzip() is None
    assert len([n for n in archive.namelist() if n.endswith('.dll')]) == 1
print('Thunderstore upload: ' + str(public_zip))
