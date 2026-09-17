import importlib.util
import argparse
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('nexus_release', Path(__file__).parents[1] / 'scripts/nexus_release.py')
nexus = importlib.util.module_from_spec(spec)
spec.loader.exec_module(nexus)


class NexusTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.config = {'name': 'Example', 'dlls': ['Example.dll'], 'game': 'valheim'}
        self.dll = 'BepInEx/plugins/Example/Example.dll'

    def source(self, extra=None, name='Example', version='1.2.3'):
        entries = {'manifest.json': json.dumps({'name': name, 'version_number': version}),
                   self.dll: b'MZplugin-bytes', 'README.md': 'approved text',
                   'CHANGELOG.md': 'private release notes', 'LICENSE.txt': 'license',
                   'VALIDATION.md': 'test notes', 'icon.png': b'icon',
                   'BepInEx/plugins/Example/notes.txt': 'not runtime'}
        entries.update(extra or {})
        source = self.root / 'source.zip'
        with zipfile.ZipFile(source, 'w') as z:
            for n, data in entries.items():
                z.writestr(n, data)
        return source

    def prepare(self, source=None):
        return nexus.prepare(source or self.source(), self.root / 'out', self.config, '1.2.3')

    def test_runtime_only_and_preserves_dll(self):
        output = self.prepare()
        with zipfile.ZipFile(output) as z:
            self.assertEqual(z.namelist(), [self.dll])
            self.assertEqual(z.read(self.dll), b'MZplugin-bytes')
        self.assertTrue(output.with_suffix('.zip.sha256').exists())
        self.assertTrue(output.with_suffix('.zip.receipt.json').exists())

    def test_two_dlls_and_required_asset(self):
        self.config['dlls'].append('Core.dll')
        asset = 'BepInEx/plugins/Example/assets/model.bin'
        self.config['runtime_files'] = [asset]
        source = self.source({'BepInEx/plugins/Example/Core.dll': b'MZcore', asset: b'asset'})
        with zipfile.ZipFile(self.prepare(source)) as z:
            self.assertEqual(len(z.namelist()), 3)

    def test_wrong_name_or_version(self):
        for kwargs in [{'name': 'Other'}, {'version': '9.9.9'}]:
            with self.subTest(kwargs=kwargs), self.assertRaises(ValueError):
                self.prepare(self.source(**kwargs))

    def test_unexpected_dll(self):
        with self.assertRaises(ValueError):
            self.prepare(self.source({'assembly_valheim.dll': b'MZgame'}))

    def test_missing_runtime_asset(self):
        self.config['runtime_files'] = ['BepInEx/plugins/Example/model.bin']
        with self.assertRaises(ValueError):
            self.prepare()

    def test_bad_dll_header(self):
        with self.assertRaises(ValueError):
            self.prepare(self.source({self.dll: b'not a DLL'}))

    def test_unsafe_and_duplicate_paths(self):
        for name in ['../outside', '/absolute', 'C:/absolute', 'bad\\path', self.dll.upper()]:
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.prepare(self.source({name: b'bad'}))

    def test_version_tag_validation(self):
        self.assertEqual(nexus.version_string('v1.2.3'), '1.2.3')
        for tag in ['../main', 'v1.2.3\nBAD=value', '$(echo oops)', 'main']:
            with self.assertRaises(ValueError):
                nexus.version_string(tag)

    def replies(self, versions=None, files=None):
        return [{'data': {'id': 'mod-uuid'}},
                {'data': {'mod_files': files if files is not None else [{'id': 'file-uuid'}]}},
                {'data': {'versions': versions or []}}]

    def test_preflight_selects_only_file(self):
        with patch.object(nexus, 'read_json', side_effect=self.replies()):
            self.assertEqual(nexus.preflight(self.config, '1.2.3', 'secret', '', 3813), ('mod-uuid', 'file-uuid'))

    def test_duplicate_version_blocks_retry(self):
        with patch.object(nexus, 'read_json', side_effect=self.replies([{'version': 'v1.2.3'}])):
            with self.assertRaisesRegex(ValueError, 'already exists'):
                nexus.preflight(self.config, '1.2.3', 'secret', 'file-uuid', 3813)

    def test_wrong_destination_and_ambiguous_file(self):
        for file_id, files in [('other-file', [{'id': 'file-uuid'}]), ('', []),
                               ('', [{'id': 'one'}, {'id': 'two'}])]:
            with patch.object(nexus, 'read_json', side_effect=self.replies(files=files)):
                with self.assertRaises(ValueError):
                    nexus.preflight(self.config, '1.2.3', 'secret', file_id, 3813)

    def test_missing_key_makes_no_request(self):
        with patch.object(nexus, 'read_json') as request:
            with self.assertRaises(ValueError):
                nexus.preflight(self.config, '1.2.3', '', '', 3813)
            request.assert_not_called()

    def test_runtime_archive_can_be_validated_again(self):
        source = self.prepare()
        output = nexus.prepare(source, self.root / 'second', self.config, '1.2.3')
        self.assertEqual(source.read_bytes(), output.read_bytes())

    def release_fixture(self):
        source = self.prepare()
        data = source.read_bytes()
        release = {'draft': False, 'prerelease': False, 'tag_name': 'v1.2.3',
                   'assets': [{'name': source.name, 'digest': 'sha256:' + nexus.digest(data),
                               'size': len(data), 'browser_download_url':
                               'https://github.com/Vassteel/Example/releases/download/v1.2.3/' + source.name}]}
        args = argparse.Namespace(tag='v1.2.3', asset=source.name, publish=False)
        return data, release, args

    def run_release(self, data, release, args):
        previous = Path.cwd()
        os.chdir(self.root)
        try:
            with patch.dict(os.environ, {'GITHUB_REPOSITORY': 'Vassteel/Example', 'GH_TOKEN': 'test-token'}, clear=True), \
                 patch.object(nexus, 'read_json', return_value=release), \
                 patch.object(nexus.urllib.request, 'urlopen', return_value=io.BytesIO(data)), \
                 patch.object(nexus, 'preflight', return_value=('mod-uuid', 'file-uuid')) as preflight:
                nexus.from_release(args, self.config)
                return preflight.call_count
        finally:
            os.chdir(previous)

    def test_validation_mode_never_contacts_nexus(self):
        self.assertEqual(self.run_release(*self.release_fixture()), 0)

    def test_publish_mode_requires_preflight(self):
        data, release, args = self.release_fixture()
        args.publish = True
        self.assertEqual(self.run_release(data, release, args), 1)

    def test_corrupt_download_rejected(self):
        data, release, args = self.release_fixture()
        with self.assertRaisesRegex(ValueError, 'digest'):
            self.run_release(data[:-1] + b'x', release, args)

    def test_non_runtime_release_rejected(self):
        data, release, args = self.release_fixture()
        data = self.source().read_bytes()
        release['assets'][0].update(digest='sha256:' + nexus.digest(data), size=len(data))
        with self.assertRaisesRegex(ValueError, 'runtime files only'):
            self.run_release(data, release, args)

    def test_draft_or_prerelease_rejected(self):
        for field in ['draft', 'prerelease']:
            data, release, args = self.release_fixture()
            release[field] = True
            with self.assertRaisesRegex(ValueError, 'stable'):
                self.run_release(data, release, args)


if __name__ == '__main__':
    unittest.main()
