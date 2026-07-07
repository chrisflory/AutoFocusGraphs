# Changelog

## Unreleased

## 0.1.0.0 (develop)

- Fork from AutofocusDiscord v1.3.1.5 as **AutofocusGraphs**
- New plugin identity (assembly, GUID, NINA folder) — installs alongside AutofocusDiscord
- `IAutofocusDestination` abstraction with destination router
- **Discord** destination (existing webhook client, unchanged behavior)
- **Telegram** destination (Bot API: sendPhoto, sendDocument, sendMessage)
- Options UI: per-channel enable + Telegram token/chat ID + test buttons
- Renamed `PostPerRunToDiscord` → `PostPerRun` (posts to all enabled destinations)
