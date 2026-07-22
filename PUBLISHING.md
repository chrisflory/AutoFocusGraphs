# Publishing notes

Release artifacts for AutoFocusGraphs and (later) submission to the N.I.N.A. community catalog.

## Versioning

`Major.Minor.Patch.Build` — increment **Build** (last digit) for small fixes and polish; **Patch** for bug fix batches; **Minor** for features; **Major** for breaking changes.

## 2A — GitHub Release (v0.1.0.5)

| Item | Value |
| --- | --- |
| Tag | `v0.1.0.5` |
| Asset | `AutoFocusGraphs.0.1.0.5.zip` |
| Download URL | https://github.com/chrisflory/AutoFocusGraphs/releases/download/v0.1.0.5/AutoFocusGraphs.0.1.0.5.zip |
| SHA256 | `b868eb60d6edd2a32c10df94b1cb106bb4eaac2c90dc4a44f2f510387769646e` |

Previous release **v0.1.0.4**: tag `v0.1.0.4`, asset `AutoFocusGraphs.0.1.0.4.zip`, SHA256 `48ec56f1cbe48f10bb79849b073e8c725e28f27ba69b58463fcd115a6c79f37d` — this is the version referenced by catalog PR #576; do not modify its release asset.

Zip contents (extract into `%localappdata%\NINA\Plugins\3.0.0\AutoFocusGraphs\`):

- `AutoFocusGraphs.dll`
- `ScottPlot.dll`
- `SkiaSharp.dll`
- `SkiaSharp.HarfBuzz.dll`
- `HarfBuzzSharp.dll`
- `MailKit.dll`
- `MimeKit.dll`
- `BouncyCastle.Cryptography.dll`
- `libSkiaSharp.dll`
- `libHarfBuzzSharp.dll`

**Do not rebuild or re-zip after the manifest checksum is recorded.** Any change invalidates `Installer.Checksum`.

## 2B — Manifest

| Copy | Path |
| --- | --- |
| Canonical (this repo) | [`manifests/0.1.0.5/manifest.json`](manifests/0.1.0.5/manifest.json) |
| Catalog PR staging | [`manifests/catalog-pr/manifests/a/AutoFocusGraphs/3.0.0/0.1.0.5/manifest.json`](manifests/catalog-pr/manifests/a/AutoFocusGraphs/3.0.0/0.1.0.5/manifest.json) |

Path in [nina.plugin.manifests](https://github.com/isbeorn/nina.plugin.manifests):

`manifests/a/AutoFocusGraphs/3.0.0/0.1.0.5/manifest.json`

Catalog PR #576 currently submits **0.1.0.4** — the 0.1.0.5 manifest stays local until we decide whether to update the PR.

- Targets **N.I.N.A. 3.3 nightlies** (`MinimumApplicationVersion` 3.3.0.1047, .NET 10)
- `"Channel": "Beta"` for beta feed first
- Full PR checklist: [`docs/NINA_MANIFEST_PR.md`](docs/NINA_MANIFEST_PR.md)
- Validate: `.\tools\validate_catalog_manifest.ps1` (requires Node.js)
- Sync after edits: `.\tools\sync_catalog_manifest.ps1 -Version 0.1.0.5`

## Recreate a release package locally

```powershell
dotnet build -c Release --no-incremental
# stage managed + native deps into publish\AutoFocusGraphs (see csproj PostBuild list), then:
Compress-Archive -Path packages\AutoFocusGraphs\* -DestinationPath publish\AutoFocusGraphs.0.1.0.5.zip
Get-FileHash publish\AutoFocusGraphs.0.1.0.5.zip -Algorithm SHA256
```

Update `manifests/<version>/manifest.json` `Installer.Checksum` to match the new zip **before** uploading the release asset.
