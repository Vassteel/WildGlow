"""Update existing game/profile WildGlow copies together, with backups and rollback."""
from pathlib import Path
from datetime import datetime, timezone
import argparse, csv, hashlib, json, subprocess
import yaml
from install_approved import ROOT, GAME, merge_config, require_closed, replace_file

MANAGER=Path('/home/deck/.var/app/io.github.ebkr.r2modman/config/r2modmanPlus-local/Valheim')
PROFILE=MANAGER/'profiles/Mods'
UPDATES={'General': {'IncludeLooseItems': 'false'}}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
    package=ROOT/'dist/WildGlow-0.4.5'
    binary=(package/'BepInEx/plugins/WildGlow/WildGlow.dll').read_bytes()
    expected=(ROOT/'dist/SHA256SUMS.txt').read_text().split()[0]
    assert hashlib.sha256(binary).hexdigest()==expected
    mods=PROFILE/'mods.yml';oldmods=mods.read_bytes();entries=yaml.safe_load(oldmods)
    matching=[e for e in entries if 'wildglow' in e['name'].lower()]
    assert len(matching)==1, 'Expected one managed WildGlow entry.'
    entry=matching[0];name=entry['name'];folder=PROFILE/'BepInEx/plugins'/name
    assert (folder/'WildGlow.dll').is_file()
    assert len(list((PROFILE/'BepInEx/plugins').rglob('WildGlow.dll')))==1
    new={GAME/'BepInEx/plugins/WildGlow/WildGlow.dll':binary}
    manifest=json.loads((package/'manifest.json').read_text());manifest['author']=entry['authorName']
    for dest in (folder, MANAGER/'cache'/name/'0.4.5'):
        for filename in ('icon.png','README.md','CHANGELOG.md'):
            new[dest/filename]=(package/filename).read_bytes()
        new[dest/'manifest.json']=(json.dumps(manifest,indent=2)+'\n').encode()
        new[dest/'WildGlow.dll']=binary
    originalcfg=(GAME/'BepInEx/config/local.valheim.wildglow.cfg').read_bytes()
    for dest in (GAME/'BepInEx/config/local.valheim.wildglow.cfg',PROFILE/'BepInEx/config/local.valheim.wildglow.cfg'):
        text=(dest.read_bytes() if dest.exists() else originalcfg).decode('utf-8-sig')
        new[dest]=merge_config(text,UPDATES).encode()
    entry.update(versionNumber=dict(major=0,minor=4,patch=5),description=manifest['description'])
    # Keep other entries semantically identical, including enabled states and ordering.
    new[mods]=yaml.safe_dump(entries,sort_keys=False,allow_unicode=True).encode()
    old={p:p.read_bytes() if p.exists() else None for p in new}
    assert old[mods]==oldmods
    print('Update existing game and r2modman '+name+' copies to 0.4.5.\nPreserve profile identity, enabled state and other mods. Disable legacy IncludeLooseItems; preserve appearance settings.\nDLL SHA-256: '+expected)
    if not args.apply:return
    require_closed()
    check=subprocess.run(['pgrep','-xi','r2modman'],capture_output=True)
    assert check.returncode==1, 'Close r2modman to prevent a mod-list overwrite.'
    backup=ROOT/'backups'/('installed-copies-before-0.4.5-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    backup.mkdir(parents=True)
    listing=[]
    for i,(p,data) in enumerate(old.items()):
        key=f'{i:02d}-{p.name}';listing.append(dict(path=str(p),backup=key if data is not None else None))
        if data is not None:(backup/key).write_bytes(data)
    (backup/'files.json').write_text(json.dumps(listing,indent=2))
    assert all((p.read_bytes() if p.exists() else None)==data for p,data in old.items()), 'Installation changed during preparation.'
    require_closed();written=[]
    try:
        for p,data in new.items():
            p.parent.mkdir(parents=True,exist_ok=True);replace_file(p,data);written.append(p)
        assert all(p.read_bytes()==data for p,data in new.items())
    except Exception:
        for p in reversed(written):
            if old[p] is None:p.unlink()
            else:replace_file(p,old[p])
        raise
    result=f'WildGlow 0.4.5 installed in game and {PROFILE}.\nDLL SHA-256: {expected}\nBackup: {backup}\nFiles verified byte-for-byte; game and manager were closed. Launch-time loading and ore appearance await next playtest.\n'
    (ROOT/'dist/install-validation.txt').write_text(result)
    (ROOT/'INSTALLATION.md').write_text('# Local installation\n\n'+result+'\nNew per-style Enabled entries are generated on first launch. Existing preferences were preserved except disabling the legacy IncludeLooseItems setting.\n')
    print(result)

if __name__=='__main__':main()
