# Publishing notes

This documents the **2A / 2B** release artifacts for submitting AutofocusGraphs to the N.I.N.A. community catalog later.

## Versioning

`Major.Minor.Patch.Build` — increment **Build** (last digit) for small fixes and polish; **Patch** for bugfix batches; **Minor** for features; **Major** for breaking changes.

## 2A — GitHub Release

| Item | Value |
| --- | --- |
| Tag | `v1.3.0.1` |
| Asset | `AutofocusGraphs.1.3.0.1.zip` |
| Download URL | https://github.com/chrisflory/AutofocusGraphs/releases/download/v1.3.0.1/AutofocusGraphs.1.3.0.1.zip |
| SHA256 | `ced9e3accaaeb0805b3c6241b2c59676987c744b6488971472a9f8761ac673c1` |

Zip contents (installed into `%localappdata%\NINA\Plugins\3.0.0\AutofocusDiscord\`):

- `AutofocusGraphs.dll`
- `ScottPlot.dll`
- `SkiaSharp.dll`
- `SkiaSharp.HarfBuzz.dll`
- `HarfBuzzSharp.dll`
- `libSkiaSharp.dll`
- `libHarfBuzzSharp.dll`

**Do not rebuild or re-zip after the manifest checksum is recorded.** Any change invalidates `Installer.Checksum`.

## 2B — Manifest

Manifest file in this repo:

[`manifests/1.3.0.1/manifest.json`](manifests/1.3.0.1/manifest.json)

Notes for the catalog PR (when you are ready):

- Path in [nina.plugin.manifests](https://github.com/isbeorn/nina.plugin.manifests) should follow their layout, e.g. `manifests/a/AutofocusGraphs/1.3.0.1/manifest.json`
- This build targets **N.I.N.A. 3.3 nightlies** (`MinimumApplicationVersion` 3.3.0.1047, .NET 10)
- Manifest is marked `"Channel": "Beta"` so it can go to the beta feed first if preferred
- Validate with their `node gather.js` flow before opening the PR

## Recreate a release package locally

```powershell
dotnet build -c Release --no-incremental
# stage managed + native deps into publish\AutofocusDiscord, then:
Compress-Archive -Path publish\AutofocusDiscord\* -DestinationPath publish\AutofocusGraphs.1.3.0.1.zip
Get-FileHash publish\AutofocusGraphs.1.3.0.1.zip -Algorithm SHA256
```

Update `manifests/1.3.0.1/manifest.json` `Installer.Checksum` to match the new zip **before** uploading the release asset.

## Previous release

| Tag | Asset |
| --- | --- |
| `v1.3.0.0` | `AutofocusGraphs.1.3.0.0.zip` |

See [`manifests/1.3.0.0/manifest.json`](manifests/1.3.0.0/manifest.json) for the prior checksum.
