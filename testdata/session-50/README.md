# 50-run session digest test set

Files: `session-run-01.json` … `session-run-50.json`

Varied filters (L/R/G/B/Ha/OIII/SII), HFR wave, a few low-R² and high-HFR runs.

With the plugin enabled:

```powershell
Copy-Item .\testdata\session-50\*.json "$env:LOCALAPPDATA\NINA\AutoFocus\" -Force
```

Wait for processing (or use AF History → Refresh), then **Post session digest**.
