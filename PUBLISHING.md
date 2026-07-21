# Publishing notes

Release artifacts for AutoFocusGraphs and (later) submission to the N.I.N.A. community catalog.

## Versioning

`Major.Minor.Patch.Build` — increment **Build** (last digit) for small fixes and polish; **Patch** for bug fix batches; **Minor** for features; **Major** for breaking changes.

## 2A — GitHub Release (v0.1.0.4)

| Item | Value |
| --- | --- |
| Tag | `v0.1.0.4` |
| Asset | `AutoFocusGraphs.0.1.0.4.zip` |
| Download URL | https://github.com/chrisflory/AutoFocusGraphs/releases/download/v0.1.0.4/AutoFocusGraphs.0.1.0.4.zip |
| SHA256 | `48ec56f1cbe48f10bb79849b073e8c725e28f27ba69b58463fcd115a6c79f37d` |

Previous release **v0.1.0.3**: tag `v0.1.0.3`, asset `AutoFocusGraphs.0.1.0.3.zip`, SHA256 `f070a3c50572f30adfd95464cddacad0fa71e4736e5e24431c01a6cf137f2ee2`.

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
| Canonical (this repo) | [`manifests/0.1.0.4/manifest.json`](manifests/0.1.0.4/manifest.json) |
| Catalog PR staging | [`manifests/catalog-pr/manifests/a/AutoFocusGraphs/3.0.0/0.1.0.4/manifest.json`](manifests/catalog-pr/manifests/a/AutoFocusGraphs/3.0.0/0.1.0.4/manifest.json) |

Path in [nina.plugin.manifests](https://github.com/isbeorn/nina.plugin.manifests):

`manifests/a/AutoFocusGraphs/3.0.0/0.1.0.4/manifest.json`

- Targets **N.I.N.A. 3.3 nightlies** (`MinimumApplicationVersion` 3.3.0.1047, .NET 10)
- `"Channel": "Beta"` for beta feed first
- Full PR checklist: [`docs/NINA_MANIFEST_PR.md`](docs/NINA_MANIFEST_PR.md)
- Validate: `.\tools\validate_catalog_manifest.ps1` (requires Node.js)
- Sync after edits: `.\tools\sync_catalog_manifest.ps1 -Version 0.1.0.4`

## Recreate a release package locally

```powershell
dotnet build -c Release --no-incremental
# stage managed + native deps into publish\AutoFocusGraphs (see csproj PostBuild list), then:
Compress-Archive -Path packages\AutoFocusGraphs\* -DestinationPath publish\AutoFocusGraphs.0.1.0.4.zip
Get-FileHash publish\AutoFocusGraphs.0.1.0.4.zip -Algorithm SHA256
```

Update `manifests/<version>/manifest.json` `Installer.Checksum` to match the new zip **before** uploading the release asset.
