# Publishing notes

Release artifacts for AutoFocusGraphs and (later) submission to the N.I.N.A. community catalog.

## Versioning

`Major.Minor.Patch.Build` — increment **Build** (last digit) for small fixes and polish; **Patch** for bugfix batches; **Minor** for features; **Major** for breaking changes.

## 2A — GitHub Release (v0.1.0.0)

| Item | Value |
| --- | --- |
| Tag | `v0.1.0.0` |
| Asset | `AutoFocusGraphs.0.1.0.0.zip` |
| Download URL | https://github.com/chrisflory/AutoFocusGraphs/releases/download/v0.1.0.0/AutoFocusGraphs.0.1.0.0.zip |
| SHA256 | `6db7f503a3b63dfc339fc067a9b8c99dcd8f4b3007a4b11e59dfcfc8abe6b130a6` |

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

[`manifests/0.1.0.0/manifest.json`](manifests/0.1.0.0/manifest.json)

Notes for the catalog PR (when ready):

- Path in [nina.plugin.manifests](https://github.com/isbeorn/nina.plugin.manifests): `manifests/a/AutoFocusGraphs/0.1.0.0/manifest.json`
- Targets **N.I.N.A. 3.3 nightlies** (`MinimumApplicationVersion` 3.3.0.1047, .NET 10)
- `"Channel": "Beta"` for beta feed first if preferred
- Validate with their `node gather.js` flow before opening the PR

## Recreate a release package locally

```powershell
dotnet build -c Release --no-incremental
# stage managed + native deps into publish\AutoFocusGraphs (see csproj PostBuild list), then:
Compress-Archive -Path publish\AutoFocusGraphs\* -DestinationPath publish\AutoFocusGraphs.0.1.0.0.zip
Get-FileHash publish\AutoFocusGraphs.0.1.0.0.zip -Algorithm SHA256
```

Update `manifests/<version>/manifest.json` `Installer.Checksum` to match the new zip **before** uploading the release asset.
