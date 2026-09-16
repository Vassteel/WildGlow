import contextlib
import copy
import io
import json
import os
from pathlib import Path
import tempfile
import textwrap
import unittest
from unittest.mock import patch, MagicMock
import urllib.error

workflow = (Path(__file__).parents[1] / 'workflows/discord-release.yml').read_text()
source = textwrap.dedent(workflow.split("python3 - <<'PYTHON'\n", 1)[1].rsplit('          PYTHON', 1)[0])
module = {'__name__': 'announcement_test'}
exec(compile(source, 'discord-release.yml', 'exec'), module)
BASE = {'action': 'published', 'repository': {'full_name': 'Vassteel/Quartermaster'}, 'release': {
    'id': 123, 'tag_name': 'v0.1.11', 'html_url': 'https://github.com/Vassteel/Quartermaster/releases/tag/v0.1.11',
    'body': 'Sort one slot at a time.', 'draft': False, 'prerelease': False, 'published_at': '2026-09-15T12:00:00Z'}}
WEBHOOK = 'https://discord.com/api/webhooks/123/test-secret'

class ReleaseTests(unittest.TestCase):
    def test_all_mods_and_release_link(self):
        for repo, name in module['NAMES'].items():
            event = copy.deepcopy(BASE);event['repository']['full_name'] = repo
            event['release']['html_url'] = f'https://github.com/{repo}/releases/tag/v1'
            result = module['payload'](event)
            self.assertIn(name, result['embeds'][0]['title'])
            self.assertEqual(result['allowed_mentions'], {'parse': []})
            self.assertEqual(result['embeds'][0]['fields'][0]['value'], event['release']['html_url'])

    def test_only_published_releases(self):
        for action in ['edited', 'created', 'deleted', 'unpublished', None]:
            event = copy.deepcopy(BASE);event['action'] = action
            self.assertIsNone(module['payload'](event))
        event = copy.deepcopy(BASE);event['release']['draft'] = True
        self.assertIsNone(module['payload'](event))
        event = copy.deepcopy(BASE);event['repository']['full_name'] = 'someone/fork'
        self.assertIsNone(module['payload'](event))

    def test_untrusted_notes_are_data_and_size_is_bounded(self):
        event = copy.deepcopy(BASE)
        event['release']['body'] = '@everyone $(echo unsafe) ' + '🪶' * 5000
        event['release']['tag_name'] = '🪶' * 500
        post = module['payload'](event);embed = post['embeds'][0]
        self.assertLessEqual(len(embed['description'].encode('utf-16-le')) // 2, 3000)
        self.assertLessEqual(len(embed['title'].encode('utf-16-le')) // 2, 240)
        self.assertIn('$(echo unsafe)', embed['description'])
        self.assertEqual(post['allowed_mentions']['parse'], [])

    def test_prerelease_and_empty_notes(self):
        event = copy.deepcopy(BASE);event['release'].update(body=None, prerelease=True)
        post = module['payload'](event)
        self.assertIn('Pre-release', post['embeds'][0]['footer']['text'])
        self.assertTrue(post['embeds'][0]['description'])

    def execute(self, event=BASE, webhook=WEBHOOK, receipt=None, error=None):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'event.json';path.write_text(json.dumps(event))
            output = io.StringIO();response = MagicMock()
            response.__enter__.return_value = io.StringIO(json.dumps(receipt or {'id': '456'}))
            with patch.dict(os.environ, {'GITHUB_EVENT_PATH':str(path),'DISCORD_WEBHOOK_URL':webhook}), \
                 patch('urllib.request.build_opener') as factory, contextlib.redirect_stdout(output):
                factory.return_value.open.return_value = response
                factory.return_value.open.side_effect = error
                result = module['main']()
            return result, output.getvalue(), factory

    def test_confirmed_delivery(self):
        result, output, factory = self.execute()
        self.assertEqual(result, 0)
        request = factory.return_value.open.call_args.args[0]
        self.assertTrue(request.full_url.endswith('?wait=true'))
        self.assertEqual(request.method, 'POST')
        self.assertEqual(json.loads(request.data)['allowed_mentions'], {'parse': []})
        self.assertNotIn('test-secret', output)

    def test_missing_or_invalid_webhook_never_sends(self):
        for url in ['', 'https://example.com/secret', WEBHOOK+'/github', WEBHOOK+'?thread_id=1']:
            result, output, factory = self.execute(webhook=url)
            self.assertEqual(result, 1);factory.assert_not_called()
            self.assertNotIn('test-secret', output)

    def test_ignored_event_never_sends(self):
        event = copy.deepcopy(BASE);event['action'] = 'edited'
        result, _, factory = self.execute(event=event)
        self.assertEqual(result, 0);factory.assert_not_called()

    def test_errors_do_not_leak_secret_or_retry(self):
        for error in [urllib.error.HTTPError(WEBHOOK,429,'rate limited',{},None),
                      urllib.error.URLError(WEBHOOK),TimeoutError(WEBHOOK)]:
            result, output, factory = self.execute(error=error)
            self.assertEqual(result, 1);self.assertNotIn('test-secret', output)
            self.assertEqual(factory.return_value.open.call_count, 1)

    def test_unconfirmed_response_fails(self):
        result, _, _ = self.execute(receipt={'unexpected':True})
        self.assertEqual(result, 1)

    def test_no_redirect_and_unexpected_release_url(self):
        self.assertIsNone(module['NoRedirect']().redirect_request(None,None,302,'',{},'https://example.com'))
        event = copy.deepcopy(BASE);event['release']['html_url'] = 'https://example.com'
        with self.assertRaises(ValueError):module['payload'](event)

if __name__ == '__main__':unittest.main()
