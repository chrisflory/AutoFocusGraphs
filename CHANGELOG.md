# Changelog

## Unreleased

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
