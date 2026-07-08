# Changelog

## Unreleased

- Documentation and flowcharts updated for AutoFocusGraphs as the public project
- **Slack** graph uploads migrated to external upload API (`files.getUploadURLExternal` / `files.completeUploadExternal`)
- Multi-destination posting continues when one channel fails

## 0.1.0.0 (develop)

- Initial public **AutoFocusGraphs** release — multi-channel autofocus graph delivery
- Tabbed options UI: **Graph** (preview, overlays, quality, posting, digests), Discord, Telegram, Slack, Email
- **Email** destination (SMTP send-only with graph/JSON attachments)
- **Slack** destination (Bot token + channel ID)
- **Telegram** destination (Bot API: sendPhoto, sendDocument, sendMessage)
- **Discord** destination (webhook client with embeds, threads, role pings)
- `IAutofocusDestination` abstraction with destination router
- Shared `ReportMessageFormatter` for Telegram/Slack/Email captions
- Graph analysis hints, quality gate, sequence/session digests
