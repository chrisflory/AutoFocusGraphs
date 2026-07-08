# Changelog

## Unreleased

- Tabbed options UI: **Graph** (preview, overlays, quality, posting, digests), Discord, Telegram, Slack, Email
- **Email** destination (SMTP send-only with graph/JSON attachments)
- **Slack** destination (Bot token + channel ID, files.upload + chat.postMessage)
- Shared `ReportMessageFormatter` for Telegram/Slack captions

## 0.1.0.0 (develop)

- Fork from AutofocusDiscord v1.3.1.5 as **AutoFocusGraphs**
- New plugin identity (assembly, GUID, NINA folder) — installs alongside AutofocusDiscord
- `IAutofocusDestination` abstraction with destination router
- **Discord** destination (existing webhook client, unchanged behavior)
- **Telegram** destination (Bot API: sendPhoto, sendDocument, sendMessage)
- Options UI: per-channel enable + Telegram token/chat ID + test buttons
- Renamed `PostPerRunToDiscord` → `PostPerRun` (posts to all enabled destinations)
