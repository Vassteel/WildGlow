# Discord release announcements

Destination: **#updates** in Vassteel's Workshop. Covers WildGlow, Quartermaster and Helmsman using the same workflow in each repository.

## One-time connection

1. In Discord, open **#updates → Edit Channel → Integrations → Webhooks**. Create or reuse a webhook dedicated to mod updates and copy its URL. A normal text/announcement channel is supported; a forum requires a thread-aware setup.
2. In each GitHub repository, open **Settings → Secrets and variables → Actions → New repository secret**. Name it **DISCORD_WEBHOOK_URL** and paste the same webhook URL as its value. Use the ordinary Discord URL, without `/github`.
3. Keep the URL in GitHub Secrets, not this repository or release notes. It grants posting access to the selected channel.
4. Include this workflow on the branch/tag used for future releases. Upload the finished ZIP and write the release notes while the release is still a draft, then publish it.
5. Check **Actions → Announce published release in Discord** and verify the message in #updates.

Only newly **published GitHub releases** trigger posts. Commits, pushes, local builds, Downloads-folder updates, draft releases and release edits do not. Published prereleases are explicitly labelled. Existing published releases are not backfilled. No bot hosting or local running computer is needed.

The post uses **Workshop Gull**, names the mod/version, includes up to 3,000 UTF-16 units of release notes and links to the release downloads. It does not mention users or roles. Long notes remain available through the release link.

## Delivery and recovery

The workflow has no repository permissions and runs no downloaded actions or release code. It reads GitHub's event as data. Discord must return a message receipt for a confirmed delivery; failures appear in Actions without logging the webhook URL. Missing secrets fail with a setup message.

A retry may duplicate an already delivered post. Check #updates before using GitHub's re-run button, especially after a timeout. There is no automatic retry or cross-run deduplication. Configure either this workflow or Discord's direct GitHub webhook, not both for the same release event.

## Local verification

Run `python3 .github/tests/test_discord_release.py`. Tests execute the actual inline workflow code with mocked delivery and never contact Discord.

References: [GitHub release events](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#release), [Discord webhooks](https://docs.discord.com/developers/resources/webhook), [Discord setup](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks).
