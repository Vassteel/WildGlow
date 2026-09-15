"""Register the already play-tested 0.3.0 build in the existing r2modman Mods profile."""
from pathlib import Path
from datetime import datetime, timezone
import argparse, hashlib, json, os, shutil, subprocess, tempfile, time
import yaml

ROOT = Path(__file__).resolve().parents[1]
GAME = Path('/home/deck/.steam/steam/steamapps/common/Valheim')
MANAGER = Path('/home/deck/.var/app/io.github.ebkr.r2modman/config/r2modmanPlus-local/Valheim')
PROFILE = MANAGER / 'profiles/Mods'
NAME = 'Local-WildGlow'
HASH = 'b71ca6221bf3b4706c237b9960a0ca1160c1ab9058bd8dca49e8e0f674db4091'

def atomic(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(prefix='.wildglow-', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as f:
            f.write(data); f.flush(); os.fsync(f.fileno())
        os.chmod(tmp, 0o644)
        os.replace(tmp, path)
    finally:
        if os.path.exists(tmp): os.unlink(tmp)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args=parser.parse_args()
    mods=PROFILE/'mods.yml'
    old=mods.read_bytes()
    entries=yaml.safe_load(old)
    assert isinstance(entries,list)
    assert not any('wildglow' in e['name'].lower() for e in entries), 'WildGlow is already registered; inspect before updating.'
    assert any(e['name']=='denikson-BepInExPack_Valheim' and e['enabled'] for e in entries)
    assert not list((PROFILE/'BepInEx/plugins').rglob('*WildGlow*.dll')), 'Existing unmanaged copy needs review.'
    binary=(GAME/'BepInEx/plugins/WildGlow/WildGlow.dll').read_bytes()
    assert hashlib.sha256(binary).hexdigest()==HASH, 'Installed version differs from play-tested 0.3.0.'
    manifest=json.loads((ROOT/'packaging/manifest.json').read_text())
    manifest.update(version_number='0.3.0', description='Collectible motes, twisting vertical columns, shared pickup effects and local illumination. Client-side. Play-tested build.')
    metadata=dict(manifestVersion=2,name=NAME,authorName='Local',websiteUrl='',displayName='WildGlow',description=manifest['description'],gameVersion='0',networkMode='both',packageType='other',installMode='managed',installedAtTime=int(time.time()*1000),loaders=[],dependencies=manifest['dependencies'],incompatibilities=[],optionalDependencies=[],versionNumber=dict(major=0,minor=3,patch=0),enabled=True,onlineSource=False)
    updated=old.rstrip()+b'\n'+yaml.safe_dump([metadata],sort_keys=False).encode()
    assert yaml.safe_load(updated)==entries+[metadata]
    files={}
    readme=b'# WildGlow 0.3.0\n\nPlay-tested collectible visual effects. Local package by Local.\nThe proposed 0.4.0 effects remain in the workspace for preview.\n'
    for folder in (PROFILE/'BepInEx/plugins'/NAME, MANAGER/'cache'/NAME/'0.3.0'):
        files[folder/'WildGlow.dll']=binary
        files[folder/'manifest.json']=(json.dumps(manifest,indent=2)+'\n').encode()
        files[folder/'icon.png']=(ROOT/'packaging/icon.png').read_bytes()
        files[folder/'README.md']=readme
    config=PROFILE/'BepInEx/config/local.valheim.wildglow.cfg'
    if not config.exists(): files[config]=(GAME/'BepInEx/config/local.valheim.wildglow.cfg').read_bytes()
    assert all(not p.exists() for p in files), 'Target files already exist; inspect before overwriting.'
    print(f'Profile: {PROFILE}\nRegister {NAME} 0.3.0 with icon; {len(files)} new files. Game installation unchanged.')
    if not args.apply: return
    # The manager keeps its mod list in memory; require it closed to avoid a lost update.
    processes=subprocess.run(['pgrep','-xi','r2modman'],capture_output=True,text=True)
    assert processes.returncode==1, 'Close r2modman before registration to avoid an in-memory list overwrite.'
    backup=ROOT/'backups'/('r2modman-registration-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    backup.mkdir(parents=True)
    (backup/'mods.yml').write_bytes(old)
    (backup/'created-files.json').write_text(json.dumps([str(p) for p in files],indent=2))
    assert mods.read_bytes()==old, 'Profile changed during preparation.'
    created=[]
    try:
        for p,data in files.items():
            atomic(p,data);created.append(p)
        assert mods.read_bytes()==old, 'Profile changed during preparation.'
        atomic(mods,updated)
        assert yaml.safe_load(mods.read_bytes())==entries+[metadata]
        for p,data in files.items(): assert p.read_bytes()==data
    except Exception:
        if mods.read_bytes()==updated: atomic(mods,old)
        for p in created: p.unlink()
        raise
    print(f'Registered and verified. Backup: {backup}')
    (ROOT/'dist/r2modman-registration.txt').write_text(f'WildGlow 0.3.0 registered in {PROFILE}\nSHA-256: {HASH}\nBackup: {backup}\nExisting entries preserved; icon and DLL verified. UI refresh required.\n')

if __name__=='__main__': main()
