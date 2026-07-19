# Changelog

## 0.1.0.3 — 2026-07-19

Patch — Options Graph tab layout cleanup.

- Renamed **Quality & posting** → **Quality** (gate, sound, R²/HFR, filter profiles only)
- Merged **What to post** and quiet hours / delay / template into one **Posting** section
- Moved failure hooks (**no JSON report**, **unreadable reports**) to the top of **Posting**

## 0.1.0.2 — 2026-07-19

Feature release — quieter overnight posting, richer digests/charts, sharper exports.

- **Per-run send** controls on the Graph tab: Every run (default), Every Nth run (2–50; warnings/failures always post), or Problems only. Digests and failure hooks are unchanged; skipped runs stay in ReportStore.
- **Compare to last (same filter)** graph overlay: ghosts the previous session V-curve behind the current measure points
- **Focus drift radar** on digests: dual-axis focus position vs temperature chart plus a Δpos / ΔT / steps/°C summary line (with HFR trend when chart toggle is on)
- **Quiet hours**: suppress successful per-run posts in a local time window (default 22:00–07:00); warnings, failures, and digests still send
- **Focus drift radar** sample chart under **Include trend chart in session digest** on the Graph tab, with expand preview and toggles for summary strip, position/filter/HFR labels, and position trend line
- **Export AF night pack** — zip session V-curve PNGs, runs.csv, trend/drift charts, README, and original AF JSON for forums/support
- Pipeline flowchart updated with **AF night pack** branch (Graph tab + README); expand / click-to-zoom on the Graph tab, click-to-enlarge on GitHub; destinations labeled Discord / Telegram / Slack / Email with per-run send and quiet hours
- Exported V-curve, trend, and focus-drift PNGs render at **2×** (ScaleFactor) for sharper Discord / digest / night-pack images
- Focus drift chart: **All overlays on/off** buttons (same pattern as AF graph overlays)

## 0.1.0.1 — 2026-07-09

Patch release — author display fix, security hardening, UI polish, and expanded graph analysis.

- Plugin info page author shows **Chris Flory** (was `Chris Flory @starjunkie` in v0.1.0.0 assembly metadata)
- Security review fixes: outbound URL validation (SSRF guards), stricter Telegram/Slack API response checks, remote SMTP requires StartTLS, safer failure-post logging, graph preview teardown
- Options UI: pipeline flowchart and “How it works” moved to the **Graph** tab only (not Discord/Telegram/Slack/Email tabs); left-aligned setting fields with light separators between groups
- Manifest checksum corrected; catalog PR staging and validation tooling added
- `DigestIncludeTodayFromDisk` setting wired; sequence digest name uses `GetSequenceNameForDigest()`
- **Graph analysis hints** expanded with CN / NINA forum patterns: steep coarse V (`steep-v-both-wings`), zigzag / seesaw, outer approach plateau, outer measurement cliff, flat overall scan (plus existing shallow-wing / plateau / edge / R² rules)
- **Preview sample** dropdown expanded to 14 synthetic curves (Normal, backlash, low R², high HFR, edge minimum, outlier, overshoot IN+OUT, noisy, step too large/small, zigzag, flat approach wing, measurement cliff, flat scan)
- Live AF runs and preview share the same analyzer when **Graph analysis hints** is enabled

## 0.1.0.0 — 2026-07-08

Initial public release — multi-channel autofocus graph delivery for N.I.N.A. 3.3 nightlies.

- Tabbed options UI: **Graph** (preview, overlays, quality, posting, digests), Discord, Telegram, Slack, Email
- **Discord** destination (webhook client with embeds, threads, role pings)
- **Telegram** destination (Bot API: sendPhoto, sendDocument, sendMessage)
- **Slack** destination (external file upload API; `chat:write` + `files:write`)
- **Email** destination (SMTP send-only with graph/JSON attachments and configurable subjects)
- `IAutofocusDestination` abstraction with destination router
- Shared `ReportMessageFormatter` for Telegram/Slack/Email captions
- Graph analysis hints, quality gate, per-filter profiles, sequence/session digests
- Multi-destination posting continues when one channel fails
