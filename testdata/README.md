# AutoFocusGraphs test reports

Copy these into `%localappdata%\NINA\AutoFocus` with the plugin enabled (`develop` branch defaults).

| File | Expected Discord result |
| --- | --- |
| `success-good-run.json` | Blue success — clean V-curve, R² ≈ 0.998, HFR ≈ 1.62 |
| `warning-low-r2.json` | Yellow — **noisy/scattered points** (looks bad), R² **0.55** |
| `warning-high-hfr.json` | Yellow — clean-ish V but elevated, Final HFR **5.25** |
| `warning-both-low-r2-and-high-hfr.json` | Yellow — **noisy** points, R² **0.42**, HFR **4.80** |
| `warning-nan-r2.json` | Yellow — **noisy** points, hyperbolic R² **NaN** (fit failed) |
| `failure-no-measure-points.json` | Red failure — incomplete report |
| `failure-empty.json` | Red failure — empty file |

Low R² means the model does **not** explain the points well. Those warning files use scattered HFR samples so the graph matches the metadata.

Options: quality gate ON, min R² = **0.90**, max final HFR = **3.0**, post failures ON.

```powershell
Copy-Item .\testdata\*.json "$env:LOCALAPPDATA\NINA\AutoFocus\" -Force
```
