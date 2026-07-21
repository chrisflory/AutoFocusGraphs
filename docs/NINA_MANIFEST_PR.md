# Submitting AutoFocusGraphs to the N.I.N.A. plugin catalog

Step-by-step guide for opening a pull request to [isbeorn/nina.plugin.manifests](https://github.com/isbeorn/nina.plugin.manifests).

## Prerequisites (already done for v0.1.0.4)

| Item | Status |
| --- | --- |
| Public source repo | https://github.com/chrisflory/AutoFocusGraphs |
| Open-source license (MIT) | Yes |
| GitHub Release with zip asset | [v0.1.0.4](https://github.com/chrisflory/AutoFocusGraphs/releases/tag/v0.1.0.4) |
| Manifest matches assembly metadata | GUID `b8d4f02a-5c3e-499b-0f2d-7e6b9c1d4e38`, Name `AutoFocusGraphs`, Author **Chris Flory** |
| Installer checksum matches release zip | SHA256 `48ec56f1cbe48f10bb79849b073e8c725e28f27ba69b58463fcd115a6c79f37d` |

**Do not rebuild or re-zip the release asset after the checksum is recorded.** Any byte change invalidates the manifest. Documentation and LongDescription text in the manifest may be updated in-repo for the next catalog PR; the Installer URL/Checksum must still match the published zip.

## Manifest location in the catalog repo

Per the [official README](https://github.com/isbeorn/nina.plugin.manifests/blob/main/README.md):

```
manifests/<first-letter>/<PluginName>/<nina-version-band>/<plugin-version>/manifest.json
```

For AutoFocusGraphs v0.1.0.4:

```
manifests/a/AutoFocusGraphs/3.0.0/0.1.0.4/manifest.json
```

A ready-to-copy tree lives in [`manifests/catalog-pr/`](../manifests/catalog-pr/).

## Beta channel

The manifest includes `"Channel": "Beta"` so it appears in the beta feed first (recommended for N.I.N.A. 3.3 nightlies).

Users opt in via **Options → General → Plugin Repositories → +** and add:

```
https://nighttime-imaging.eu/wp-json/nina/v1/beta
```

Remove `"Channel": "Beta"` (or set `"Channel": "Release"`) when you are ready for the main catalog feed.

## Validate before opening the PR

### Option A — script (recommended)

```powershell
# From repo root; requires Node.js (winget install OpenJS.NodeJS.LTS)
.\tools\validate_catalog_manifest.ps1
```

This clones or refreshes a local copy of `nina.plugin.manifests`, copies the catalog-pr manifest into place, runs `npm install` and `node gather.js`, and reports pass/fail.

### Option B — manual

```powershell
git clone https://github.com/isbeorn/nina.plugin.manifests.git
cd nina.plugin.manifests
# Copy manifests/catalog-pr/manifests/... from this repo into the clone
npm install
node gather.js
```

Look for `Manifest valid at ...AutoFocusGraphs...` and no `INVALID MANIFEST!` lines.

## Open the pull request

1. **Fork** https://github.com/isbeorn/nina.plugin.manifests
2. **Clone your fork** and create a branch, e.g. `AutoFocusGraphs/0.1.0.4`
3. **Copy** the catalog-pr tree:

   ```powershell
   # From AutoFocusGraphs repo root, adjust paths as needed
   Copy-Item -Recurse manifests\catalog-pr\manifests\a `
     C:\path\to\your-nina.plugin.manifests-fork\manifests\a
   ```

4. **Commit and push** to your fork
5. **Open PR** against `isbeorn/nina.plugin.manifests` `main`

### Suggested PR title

```
Add AutoFocusGraphs 0.1.0.4 (Beta)
```

### Suggested PR body

```markdown
## Summary
- Adds manifest for **AutoFocusGraphs** v0.1.0.4
- Posts autofocus V-curve graphs to Discord, Telegram, Slack, and email
- Graph analysis hints + 14 CN-style preview samples on the Graph tab
- Targets N.I.N.A. 3.3 nightlies (MinimumApplicationVersion 3.3.0.1047)
- Beta channel (`"Channel": "Beta"`)

## Links
- Source: https://github.com/chrisflory/AutoFocusGraphs
- Release: https://github.com/chrisflory/AutoFocusGraphs/releases/tag/v0.1.0.4
- Installer: ARCHIVE zip with SHA256 checksum in manifest

## Test plan
- [ ] `node gather.js` passes in manifest repo
- [ ] Beta repo URL added in N.I.N.A. Options → Plugin Repositories
- [ ] Plugin appears in Plugin Manager and installs from catalog
- [ ] Installed version matches 0.1.0.4; checksum verifies
```

6. **Respond to review feedback** — maintainers may ask for screenshots, description tweaks, or stable-channel timing.

## Future releases

1. Build and publish a new GitHub Release (zip + tag).
2. Update `manifests/<new-version>/manifest.json` with URL and SHA256.
3. Run `.\tools\sync_catalog_manifest.ps1 -Version <new-version>`.
4. Run `.\tools\validate_catalog_manifest.ps1`.
5. Add a new version folder under `manifests/a/AutoFocusGraphs/3.0.0/<version>/` in your manifests fork (or update the single manifest if you only maintain one version — see upstream README).

Optional: enable [`.github/workflows/release.yml`](../.github/workflows/release.yml) to automate zip + manifest generation on version tags (requires workflow permission tweaks; see comments in that file).

## Optional: auto-publish workflow

The upstream repo ships [`tools/github-action.yaml`](https://github.com/isbeorn/nina.plugin.manifests/blob/main/tools/github-action.yaml). A customized copy for AutoFocusGraphs is in `.github/workflows/release.yml`. To auto-open catalog PRs you also need:

1. A fork of `nina.plugin.manifests` under your GitHub account (same repo name)
2. A personal access token with repo scope stored as secret `PAT` on AutoFocusGraphs
3. GitHub Actions workflow permissions set to **Read and write**

This is optional; manual PR submission using `manifests/catalog-pr/` works fine.
