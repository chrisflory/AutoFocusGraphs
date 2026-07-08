# <img src="assets/webhook-icon-af-graphs.png" alt="AutoFocusGraphs logo" width="96" align="middle" /> AutoFocusGraphs

**Experimental spin-off of [AutofocusDiscord](https://github.com/chrisflory/AutofocusDiscord)** — same V-curve graph engine, multiple delivery channels.

N.I.N.A. plugin that watches autofocus JSON reports, renders dark-mode V-curve PNGs (ScottPlot), and posts them to **Discord**, **Telegram**, **Slack**, **email**, or any combination.

> **Status:** `develop` branch — not a replacement for AutofocusDiscord yet. Install side-by-side only if you want to test (different plugin GUID / folder name).

## Architecture

```
NINA AF JSON → parse + quality gate → AutofocusGraphGenerator (shared PNG)
                                      ↘ IAutofocusDestination router
                                         ├─ Discord (webhook + embeds)
                                         ├─ Telegram (Bot API)
                                         ├─ Slack (Web API)
                                         └─ Email (SMTP)
```

Graph overlays, hints, digests, and session tracking are **destination-agnostic**. Each channel implements `IAutofocusDestination`.

## Requirements

- N.I.N.A. **3.3** or newer (.NET 10 nightlies)
- **Discord:** channel webhook URL
- **Telegram:** bot token from [@BotFather](https://t.me/BotFather) + chat ID
- **Slack:** bot token (`xoxb-...`) + channel ID (`C...` / `G...`); bot needs `chat:write` and `files:write`
- **Email:** SMTP host/port, from/to, credentials (send-only)

## Install (from source)

```powershell
git clone https://github.com/chrisflory/AutoFocusGraphs.git
cd AutoFocusGraphs
git checkout develop
dotnet build -c Release
```

Deploys to:

`%localappdata%\NINA\Plugins\3.0.0\AutoFocusGraphs\`

Close N.I.N.A. before rebuilding.

## Configure

**Options → Plugins → AutoFocusGraphs** (tabbed like NINA Ground Station)

| Tab | Contents |
|---|---|
| **Graph** | Enable, preview, overlays, graph options, quality gate, posting, digests |
| **Discord** | Webhook, threads, embed/attach, role pings |
| **Telegram** | Bot token, chat ID, test |
| **Slack** | Bot token, channel ID, test |
| **Email** | SMTP settings, from/to, test |

1. **Graph** tab — enable monitoring, tune the graph, quality thresholds, and what gets posted
2. Enable one or more destinations on their tabs and run each **Test** button

Enable any combination of destinations. Per-run posts and digests fan out to every enabled, configured channel.

## Relationship to AutofocusDiscord

| | AutofocusDiscord | AutoFocusGraphs |
| --- | --- | --- |
| Focus | Discord webhook only | Multi-channel graph delivery |
| Plugin ID | `a7c3e91f-…` | `b8d4f02a-…` (separate install) |
| Maturity | Released (v1.3.1.x) | Experimental (v0.1.0.0) |

AutofocusDiscord remains the stable Discord-only plugin until AutoFocusGraphs is proven out.

## License

MIT — Chris Flory @starjunkie
