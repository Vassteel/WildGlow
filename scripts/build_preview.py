"""Embed the same effect catalog used by the compiled mod into the motion study."""
import csv
import json
import base64
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = Path('/home/deck/.codex/visualizations/2026/09/13/01a09c9e-b66c-7eb3-abe8-3d3f6279ef5b/wildglow-effects.html')
rows = list(csv.DictReader((ROOT / 'effects.tsv').open(), delimiter='\t'))
behaviors = json.loads((ROOT / 'previews/behaviors.json').read_text())
lighting = json.loads((ROOT / "previews/lighting-profiles.json").read_text())
for row in rows:
    row['family'] = behaviors[row['id']]
    row['surfaceGlow'] = lighting[row['id']]
    row['radius'] = float(row['radius'])
    row['height'] = float(row['height'])
source = (ROOT / 'previews/wildglow.fragment.html.in').read_text()
source = source.replace('/*__STYLES__*/ []', json.dumps(rows, separators=(',', ':')))
atlases = {p.stem: base64.b64encode(p.read_bytes()).decode() for p in sorted((ROOT / 'previews/atlases').glob('*.png'))}
if len(atlases) != 36:
    raise RuntimeError('Run tools/MotionTests with previews/atlases as the output directory first.')
source = source.replace('/*__ATLASES__*/ {}', json.dumps(atlases, separators=(',', ':')))
source = source.replace('/*__BEHAVIOR__*/', (ROOT / 'previews/behavior.js').read_text())
assert '/*__' not in source, 'Unexpanded preview placeholder'
DEST.parent.mkdir(parents=True, exist_ok=True)
DEST.write_text(source)
# A real offline entry point, without host CSS, fetches or iframe dependencies.
standalone = '''<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>WildGlow · Effect previews</title>
<style>:root{color-scheme:dark;--foreground:#e4eddf;--border:#4b6052;--card:#1c2921}
body{margin:0;padding:20px;background:#111b16}main{max-width:1200px;margin:auto}
h1{font:600 22px system-ui;margin:0 0 20px}@media(max-width:480px){body{padding:12px}}</style>
</head><body><main><h1>WildGlow · Effect previews</h1>''' + source + '</main></body></html>\n'
for filename in ('index.html', 'wildglow.template.html'):
    (ROOT / 'previews' / filename).write_text(standalone)
print(f'{len(rows)} effect previews: {DEST}')
print('Standalone preview (also available at the old template URL): ' + str(ROOT / 'previews/index.html'))
