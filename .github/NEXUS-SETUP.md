# Nexus release uploads

The **Upload release to Nexus Mods** GitHub Action runs manually from `master`.
It uses a ZIP attached to an existing published stable GitHub release. It does not
build the mod, create a GitHub release, or create a Nexus listing.

## One-time setup

1. Create the Nexus listing and upload its initial file through the website.
2. In GitHub **Settings → Secrets and variables → Actions**, add the repository
   secret `NEXUSMODS_API_KEY` using your Nexus personal API key. Do not commit it.
3. The Nexus page number is recorded in `.github/nexus.json`. For a new listing,
   set the repository variable `NEXUSMODS_PAGE_ID` to its number instead.
4. If the Nexus page has exactly one file, it is selected automatically. Otherwise
   set `NEXUSMODS_FILE_ID` to the API file ID from Nexus's Files → Advanced/API Info
   or Manage Files menu. This is not the ordinary numeric download/version ID.

## Upload

Build and test locally. Run the packaging command below against the existing local
package, then attach only the resulting `ModName-version-nexus.zip` to a GitHub
release. Keep receipts, licenses and documentation local. In **Actions → Upload release to Nexus Mods → Run
workflow**, choose `master`, enter the exact release tag and ZIP asset filename,
and leave **Publish to Nexus** unchecked for validation. Run again with that box
checked to upload. Archiving the previous version is an explicit optional checkbox.

The uploader creates a separate runtime-only ZIP. Only the DLLs and any explicitly
configured `runtime_files` are included. READMEs, manifests, icons, licenses,
changelogs, test reports and optional example configurations are excluded. No
changelog or page-description update is sent to Nexus. Checksums and validation
receipts stay outside the uploaded ZIP. Existing source documentation is not edited.

To prepare that same runtime-only ZIP locally:

```sh
python3 .github/scripts/nexus_release.py package path/to/source.zip --version 1.2.3
```

The result is under `dist/nexus/`. The packaging command checks the existing
manifest when present. Runtime-only archives must use the exact generated filename
to associate them with the requested version. The uploader accepts only runtime-only
release assets and verifies GitHub's
SHA-256 asset digest and validates archive paths, contents and CRCs. DLL bytes
are preserved; these checks do not establish in-game compatibility.

Publishing resolves the Nexus mod ID from its page number, checks that the file
belongs to that page, and rejects an existing version. Uploads for each repository
run serially. A failed or interrupted upload can still have reached Nexus; inspect
the page before retrying. Duplicate detection is a preflight check, not an atomic
server guarantee. No automatic retries or release-event uploads are enabled.

Nexus's version endpoints are experimental. Both external actions are pinned to
reviewed commit IDs. Test with `python3 -m unittest discover -s .github/tests -p
'test_nexus_release.py'`.

Sources: [official uploader](https://github.com/Nexus-Mods/upload-action),
[API schema](https://api.nexusmods.com/openapi.yaml).
