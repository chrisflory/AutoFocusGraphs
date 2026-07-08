# Changelog

## Unreleased

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
