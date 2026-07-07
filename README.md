# <img src="assets/webhook-icon-af-graphs.png" alt="AutofocusGraphs logo" width="96" align="middle" /> AutofocusGraphs

**Experimental spin-off of [AutofocusDiscord](https://github.com/chrisflory/AutofocusDiscord)** — same V-curve graph engine, multiple delivery channels.

N.I.N.A. plugin that watches autofocus JSON reports, renders dark-mode V-curve PNGs (ScottPlot), and posts them to **Discord**, **Telegram**, or both. More destinations can plug in behind the same graph pipeline.

> **Status:** `develop` branch — not a replacement for AutofocusDiscord yet. Install side-by-side only if you want to test (different plugin GUID / folder name).

## Architecture

```
NINA AF JSON → parse + quality gate → AutofocusGraphGenerator (shared PNG)
                                      ↘ IAutofocusDestination router
                                         ├─ Discord (webhook + embeds)
                                         └─ Telegram (Bot API sendPhoto)
```

Graph overlays, hints, digests, and session tracking are **destination-agnostic**. Each channel implements `IAutofocusDestination`.

## Requirements

- N.I.N.A. **3.3** or newer (.NET 10 nightlies)
- **Discord:** channel webhook URL
- **Telegram:** bot token from [@BotFather](https://t.me/BotFather) + chat ID (bot must be able to post)

## Install (from source)

```powershell
git clone https://github.com/chrisflory/AutofocusGraphs.git
cd AutofocusGraphs
git checkout develop
dotnet build -c Release
```

Deploys to:

`%localappdata%\NINA\Plugins\3.0.0\AutofocusGraphs\`

Close N.I.N.A. before rebuilding.

## Configure

**Options → Plugins → AutofocusGraphs**

1. **Enable autofocus graph posts** — master monitoring switch
2. **Discord** — enable, webhook URL, test webhook (embed/thread options unchanged from AutofocusDiscord)
3. **Telegram** — enable, bot token, chat ID, test Telegram
4. **Graph** — live preview, overlays, hints (shared renderer for all destinations)

Enable one or both destinations. Per-run posts and digests fan out to every enabled, configured channel.

## Relationship to AutofocusDiscord

| | AutofocusDiscord | AutofocusGraphs |
| --- | --- | --- |
| Focus | Discord webhook only | Multi-channel graph delivery |
| Plugin ID | `a7c3e91f-…` | `b8d4f02a-…` (separate install) |
| Maturity | Released (v1.3.1.x) | Experimental (v0.1.0.0) |

AutofocusDiscord remains the stable Discord-only plugin until AutofocusGraphs is proven out.

## License

MIT — Chris Flory @starjunkie
