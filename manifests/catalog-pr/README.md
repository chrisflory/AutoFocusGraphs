# N.I.N.A. catalog pull-request staging

This folder mirrors the **exact path** required in [isbeorn/nina.plugin.manifests](https://github.com/isbeorn/nina.plugin.manifests).

## What to copy into your fork

Copy everything under `manifests/` into the root of your `nina.plugin.manifests` fork:

```
manifests/a/AutoFocusGraphs/3.0.0/0.1.0.0/manifest.json
```

Resulting path in the fork:

```
nina.plugin.manifests/
  manifests/
    a/
      AutoFocusGraphs/
        3.0.0/
          0.1.0.0/
            manifest.json
```

## Keep in sync

When you cut a new release, update **both**:

1. [`manifests/<version>/manifest.json`](../0.1.0.0/manifest.json) — canonical copy in this plugin repo
2. This catalog-pr tree — same JSON, correct upstream folder layout

Run from the repo root:

```powershell
.\tools\sync_catalog_manifest.ps1 -Version 0.1.0.0
```

## Full checklist

See [`docs/NINA_MANIFEST_PR.md`](../../docs/NINA_MANIFEST_PR.md).
