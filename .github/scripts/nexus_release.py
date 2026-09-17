"""Prepare a Nexus archive and preflight a manually requested upload. No API writes."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
LIMIT = 256 * 1024 * 1024


def version_string(tag):
    version = tag.removeprefix('v')
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:[-.][A-Za-z0-9.-]+)?', version):
        raise ValueError('Use a release tag such as v1.2.3 or 1.2.3.')
    return version


def digest(data):
    return hashlib.sha256(data).hexdigest()


def prepare(source, destination, config, version):
    """Copy only configured runtime files; never execute or extract release code."""
    name = config['name']
    prefix = f'BepInEx/plugins/{name}/'
    dlls = {prefix + dll for dll in config['dlls']}
    if source.stat().st_size > LIMIT:
        raise ValueError('Archive exceeds the 256 MiB limit.')
    with zipfile.ZipFile(source) as archive:
        infos = archive.infolist()
        names = [i.filename for i in infos]
        if len(names) != len(set(n.casefold() for n in names)):
            raise ValueError('Archive contains duplicate names.')
        if sum(i.file_size for i in infos) > LIMIT:
            raise ValueError('Unpacked archive exceeds the 256 MiB limit.')
        for info in infos:
            p = PurePosixPath(info.filename)
            if p.is_absolute() or '..' in p.parts or '\\' in info.filename or ':' in info.filename:
                raise ValueError('Unsafe archive path.')
            if stat.S_ISLNK(info.external_attr >> 16) or info.flag_bits & 1:
                raise ValueError('Symlinks and encrypted entries are unsupported.')
        if archive.testzip() is not None:
            raise ValueError('Archive integrity check failed.')
        if 'manifest.json' in names:
            manifest = json.loads(archive.read('manifest.json'))
            if manifest['name'] != name or manifest['version_number'] != version:
                raise ValueError('Archive mod name or version differs from the requested release.')
        elif source.name != f'{name}-{version}-nexus.zip':
            raise ValueError('Runtime-only archive filename must match the mod and release version.')
        if {n for n in names if n.lower().endswith('.dll')} != dlls:
            raise ValueError('Archive has missing or unexpected DLLs.')
        runtime = dlls | set(config.get('runtime_files', []))
        if not runtime.issubset(names):
            raise ValueError('A configured runtime file is missing.')
        content = {n: archive.read(n) for n in sorted(runtime)}
        for dll in dlls:
            if not content[dll].startswith(b'MZ'):
                raise ValueError('Invalid plugin DLL header.')
    receipt = {'name': name, 'version': version, 'source_sha256': digest(source.read_bytes()),
               'files': {n: digest(data) for n, data in content.items()}}
    destination.mkdir(parents=True, exist_ok=True)
    output = destination / f'{name}-{version}-nexus.zip'
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
        for n, data in sorted(content.items()):
            entry = zipfile.ZipInfo(n, date_time=(2020, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, data)
    with zipfile.ZipFile(output) as archive:
        if archive.testzip() is not None:
            raise ValueError('Prepared archive integrity check failed.')
        for n, data in content.items():
            if archive.read(n) != data:
                raise ValueError('Prepared archive bytes changed.')
    output.with_suffix('.zip.sha256').write_text(f'{digest(output.read_bytes())}  {output.name}\n')
    output.with_suffix('.zip.receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    return output


def read_json(url, headers):
    request = urllib.request.Request(url, headers=headers)
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, response_headers, newurl):
            return None
    try:
        with urllib.request.build_opener(NoRedirect()).open(request, timeout=30) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise ValueError(f'API request failed (HTTP {error.code}). Check access and configuration.') from None
    except (urllib.error.URLError, TimeoutError):
        raise ValueError('API request failed or timed out; no upload was attempted.') from None


def preflight(config, version, key, file_id, page_id):
    if not key or not page_id:
        raise ValueError('Publishing requires NEXUSMODS_API_KEY and a Nexus page ID.')
    if not re.fullmatch(r'[0-9]+', str(page_id)) or (file_id and not re.fullmatch(r'[A-Za-z0-9-]+', file_id)):
        raise ValueError('Invalid Nexus ID format.')
    base = 'https://api.nexusmods.com/v3'
    headers = {'apikey': key, 'User-Agent': 'VassteelWorkshopRelease/1.0',
               'Application-Name': 'VassteelWorkshopRelease', 'Application-Version': '1.0'}
    mod = read_json(f'{base}/games/valheim/mods/{page_id}', headers)['data']
    mod_id = str(mod['id'])
    files = read_json(f'{base}/mods/{urllib.parse.quote(mod_id, safe="")}/files', headers)['data']['mod_files']
    if not file_id:
        if len(files) != 1:
            raise ValueError('Set NEXUSMODS_FILE_ID explicitly: the Nexus page must have exactly one file for automatic selection.')
        file_id = str(files[0]['id'])
    if not any(str(f['id']) == file_id for f in files):
        raise ValueError('Nexus file ID does not belong to the configured mod page.')
    if not re.fullmatch(r'[A-Za-z0-9-]+', file_id):
        raise ValueError('Unexpected Nexus file ID.')
    versions = read_json(f'{base}/mod-files/{file_id}/versions', headers)['data']['versions']
    if any(str(v['version']).removeprefix('v') == version for v in versions):
        raise ValueError('This version already exists on Nexus. Upload skipped to prevent duplicates; inspect the page before retrying.')
    return mod_id, file_id


def output_value(name, value):
    if '\n' in value or '\r' in value:
        raise ValueError('Invalid workflow output.')
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
            stream.write(f'{name}={value}\n')


def from_release(args, config):
    repo = os.environ['GITHUB_REPOSITORY']
    if repo != f'Vassteel/{config["name"]}':
        raise ValueError('Unexpected repository for this mod.')
    version = version_string(args.tag)
    headers = {'Authorization': f'Bearer {os.environ["GH_TOKEN"]}',
               'Accept': 'application/vnd.github+json', 'User-Agent': 'VassteelWorkshopRelease'}
    release = read_json(f'https://api.github.com/repos/{repo}/releases/tags/{urllib.parse.quote(args.tag, safe="")}', headers)
    if release['draft'] or release['prerelease'] or release['tag_name'] != args.tag:
        raise ValueError('Select an existing, published stable GitHub release.')
    assets = [a for a in release['assets'] if a['name'] == args.asset]
    if len(assets) != 1:
        raise ValueError('The exact source ZIP asset name must match one release asset.')
    asset = assets[0]
    expected_digest = asset.get('digest') or ''
    if not re.fullmatch(r'sha256:[a-f0-9]{64}', expected_digest):
        raise ValueError('Release asset has no GitHub SHA-256 digest. Re-upload the asset before publishing.')
    url = asset['browser_download_url']
    if not url.startswith(f'https://github.com/{repo}/releases/download/') or asset['size'] > LIMIT:
        raise ValueError('Unexpected release asset URL or size.')
    if args.asset != f'{config["name"]}-{version}-nexus.zip':
        raise ValueError('Select the runtime-only ModName-version-nexus.zip release asset.')
    source = Path('nexus-source') / args.asset
    with urllib.request.urlopen(url, timeout=60) as response:
        data = response.read(LIMIT + 1)
    if len(data) != asset['size'] or 'sha256:' + digest(data) != expected_digest:
        raise ValueError('Downloaded ZIP does not match the GitHub release digest.')
    source.parent.mkdir(exist_ok=True)
    source.write_bytes(data)
    with zipfile.ZipFile(source) as archive:
        allowed = {f'BepInEx/plugins/{config["name"]}/{d}' for d in config['dlls']} | set(config.get('runtime_files', []))
        if set(archive.namelist()) != allowed:
            raise ValueError('Release asset must contain runtime files only. Prepare it with the package command first.')
    result = prepare(source, Path('nexus-dist'), config, version)
    output_value('filename', str(result))
    output_value('version', version)
    output_value('display_name', f'{config["name"]} {version}')
    if args.publish:
        file_id = os.environ.get('NEXUSMODS_FILE_ID') or config.get('file_id') or ''
        page_id = os.environ.get('NEXUSMODS_PAGE_ID') or config.get('page_id')
        mod_id, file_id = preflight(config, version, os.environ.get('NEXUSMODS_API_KEY'), file_id, page_id)
        output_value('file_id', file_id)
        output_value('mod_id', mod_id)
    print(f'Validated {result}; SHA-256 {digest(result.read_bytes())}')
    if not args.publish:
        print('Validation only. No Nexus request or upload was made.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, default=ROOT / '.github/nexus.json')
    sub = parser.add_subparsers(dest='command', required=True)
    package = sub.add_parser('package')
    package.add_argument('source', type=Path)
    package.add_argument('--version', required=True)
    package.add_argument('--output', type=Path, default=ROOT / 'dist/nexus')
    release = sub.add_parser('release')
    release.add_argument('--tag', required=True)
    release.add_argument('--asset', required=True)
    release.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    config = json.loads(args.config.read_text())
    if args.command == 'package':
        print(prepare(args.source, args.output, config, version_string(args.version)))
    else:
        from_release(args, config)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError, zipfile.BadZipFile) as error:
        print(f'Nexus preparation failed: {error}', file=sys.stderr)
        sys.exit(1)
