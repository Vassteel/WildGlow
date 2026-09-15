"""Back up and install WildGlow 0.4.0. Default is read-only; --apply writes the update."""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import os
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
GAME = Path('/home/deck/.steam/steam/steamapps/common/Valheim')
UPDATES = {
    'Appearance': {'Density': '0.6', 'VariantMode': 'Auto'},
}


def merge_config(original, updates):
    """Preserve comments, unrelated settings and section layout; replace only requested keys."""
    newline = '\r\n' if '\r\n' in original else '\n'
    missing = {section: dict(keys) for section, keys in updates.items()}
    section = None
    output = []

    def flush():
        if section in missing:
            output.extend(f'{key} = {value}' for key, value in missing.pop(section).items())

    for line in original.splitlines():
        heading = re.fullmatch(r'\s*\[([^]]+)\]\s*', line)
        if heading:
            flush()
            section = heading[1]
        setting = re.match(r'^\s*([^#;=]+?)\s*=\s*(.*)$', line)
        if setting and section in updates and setting[1].strip() in updates[section]:
            key = setting[1].strip()
            line = f'{key} = {updates[section][key]}'
            missing.get(section, {}).pop(key, None)
        output.append(line)
    flush()
    for heading, keys in missing.items():
        output.extend(['', f'[{heading}]'])
        output.extend(f'{key} = {value}' for key, value in keys.items())
    return newline.join(output) + newline


def require_closed():
    processes = subprocess.run(['pgrep', '-a', '-f', '[v]alheim.x86_64|[v]alheim.exe'], capture_output=True, text=True)
    if processes.returncode != 1:
        raise SystemExit('Valheim may be running; close it before installing. ' + processes.stdout)


def replace_file(path, data):
    descriptor, temporary = tempfile.mkstemp(prefix='.wildglow-', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'wb') as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        os.chmod(temporary, 0o644)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    source = ROOT / 'dist/WildGlow-0.4.0/BepInEx/plugins/WildGlow/WildGlow.dll'
    target = GAME / 'BepInEx/plugins/WildGlow/WildGlow.dll'
    config = GAME / 'BepInEx/config/local.valheim.wildglow.cfg'
    binary = source.read_bytes()
    checksum, filename = (ROOT / 'dist/SHA256SUMS.txt').read_text().strip().split()
    assert filename == 'WildGlow-0.4.0/BepInEx/plugins/WildGlow/WildGlow.dll'
    assert hashlib.sha256(binary).hexdigest() == checksum, 'Package checksum mismatch'
    old_binary, old_config = target.read_bytes(), config.read_bytes()
    updated = merge_config(old_config.decode('utf-8-sig'), UPDATES).encode('utf-8')
    print('Approved changes:', UPDATES)
    if not args.apply:
        print('Read-only check complete; existing configuration can be merged.')
        return
    require_closed()
    backup = ROOT / 'backups' / ('before-0.4.0-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    backup.mkdir(parents=True)
    (backup / 'WildGlow.dll').write_bytes(old_binary)
    (backup / config.name).write_bytes(old_config)
    assert target.read_bytes() == old_binary and config.read_bytes() == old_config, 'Installed files changed during preparation'
    require_closed()
    try:
        replace_file(target, binary)
        replace_file(config, updated)
        assert target.read_bytes() == binary and config.read_bytes() == updated
    except Exception:
        replace_file(target, old_binary)
        replace_file(config, old_config)
        raise
    (ROOT / 'dist/install-validation.txt').write_text(
        f'Installed WildGlow 0.4.0\nDLL SHA-256: {checksum}\nBackup: {backup}\n'
        'Installed DLL and merged config verified byte-for-byte. Game was closed.\n')
    print(f'Installed and verified: {target}\nMerged config: {config}\nBackup: {backup}\nDLL SHA-256: {checksum}')


if __name__ == '__main__':
    main()
