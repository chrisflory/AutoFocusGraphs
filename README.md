# <img src="assets/webhook-icon-af-graphs.png" alt="AutoFocusGraphs logo" width="96" align="middle" /> AutoFocusGraphs

A N.I.N.A. plugin that watches the AutoFocus report folder, renders a **dark-mode V-curve graph**, and posts per-run reports and digests to **Discord**, **Telegram**, **Slack**, **email**, or any combination.

> **Latest release:** [v0.1.0.4](https://github.com/chrisflory/AutoFocusGraphs/releases/tag/v0.1.0.4) — multi-destination autofocus graph delivery for N.I.N.A. 3.3 nightlies.

---

## How it works

[![AutoFocusGraphs pipeline](assets/flowchart.png)](assets/flowchart.png)

*Click the diagram to open full size.* Color key: **blue** = NINA trigger · **green** = sequence digest / destination post · **purple** = session digest · **blue outline** = AF night pack · **red** = failure posts · **amber** = quality gate · **cyan** = V-curve graph.

1. **NINA writes AF JSON** — Hocus Focus / built-in autofocus saves a report under `%localappdata%\NINA\AutoFocus`.
2. **Folder watcher** — detects new `*.json` files in that folder.
3. **File settle** — waits until the file is fully written and readable.
4. **Parse + ReportStore** — parses the report and tracks it for the session and current sequencer run.
5. From there:
   - **Per-run destination post** (optional) — upload delay → quality gate → **2× V-curve PNG** → every enabled destination (subject to **Per-run send** and **Quiet hours**).
   - **Sequence digest** — when a sequencer run finishes; includes sequence name; or via **Post sequence digest now** / **Post AF sequence digest**.
   - **Session digest** — runs since NINA opened only (**Post session digest when NINA exits**), or via **Post sequence digest now** when no sequence runs are tracked.
   - **Failure posts** — optional alert when a report is empty or unreadable, or when autofocus ends without a JSON file (live AF hook).

Per-run posting, graphs, and JSON attachments can be turned off individually; **digest-only mode** collects runs locally and posts only a digest when a sequence completes and/or when NINA exits.

Graph overlays, hints, digests, and session tracking are **destination-agnostic**. Per-run posts fan out to every enabled destination independently — one channel failing does not block the others.

---

## What it does

When N.I.N.A. writes a new `*.json` autofocus report under `%localappdata%\NINA\AutoFocus`, the plugin:

1. Waits for the JSON file to finish writing, then parses and tracks the run immediately (before any upload delay)
2. Evaluates optional quality gates (R² / final HFR), including per-filter profiles
3. Optionally renders a **2×** V-curve graph (sharp fonts/lines for Discord, Telegram, Slack, email, and night packs) and posts to every enabled destination after the upload delay

Reports are always stored for digests even when per-run posting is disabled.

Quality outcomes are shared across destinations:

- **Success** — clean V-curve within quality thresholds
- **Warning** — R² is low or HFR is high (quality gate)
- **Failure** — report is empty, unreadable, incomplete, or autofocus ends in NINA without a JSON report (optional live hook)

**Sequence digest** — stats + run list for the current sequencer run; shows **Sequence:** name from the saved NINA sequence file.

**Session digest** — stats + run list for **this NINA session only** (not historical disk files); **Sequences:** count and names when sequencer runs completed.

Each digest line uses a **short AF timestamp** (not the long JSON filename):

```text
• #3 **2026-07-04--14-34-19** · **Ha** | HFR 2.85 | Pos 11240 | T 13.25 · 2026-07-04 14:34:19
```

Digests include stats (min/avg/max HFR, best/worst, warnings, by-filter breakdown) and optional charts (HFR trend + focus drift vs temperature). Long sessions may truncate the text list. Automatic digests are skipped when no new AF JSON was written since NINA opened.

**Digest-only mode:** turn off **Post each autofocus run**, enable **Post digest when sequencer sequence completes** and/or **Post session digest when NINA exits** — runs are collected locally and only the digest(s) are posted.

**Per-run send** (when per-run posts are on): post every AF, every Nth successful run, or only quality warnings/failures. Digests still include skipped runs.

**Quiet hours:** when enabled, successful per-run posts are skipped inside the local time window (default 22:00–07:00, may wrap midnight). Warnings, failures, and digests still send.

Message template tokens (per-run posts): `{shortfilename}`, `{filename}`, `{filenamefull}`, `{time}`, `{filter}`, `{prefix}`. On Discord, the full filename always appears in the embed footer.

Per-run history in NINA is left to NINA's own HFR history; this plugin focuses on outbound notifications.

---

## Requirements

- N.I.N.A. **3.3** or newer (nightly builds on .NET 10)
- At least one configured destination (see **Configure** below)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source

---

## Install

**From a release (recommended):**

Download [AutoFocusGraphs.0.1.0.4.zip](https://github.com/chrisflory/AutoFocusGraphs/releases/download/v0.1.0.4/AutoFocusGraphs.0.1.0.4.zip) and extract into:

`%localappdata%\NINA\Plugins\3.0.0\AutoFocusGraphs\`

Restart N.I.N.A.

**From source:**

```powershell
git clone https://github.com/chrisflory/AutoFocusGraphs.git
cd AutoFocusGraphs
dotnet build -c Release
```

Default branch is **main**. Close N.I.N.A. before rebuilding so the deploy copy can overwrite the plugin DLL.

A successful build copies the plugin into `%localappdata%\NINA\Plugins\3.0.0\AutoFocusGraphs\`. Restart N.I.N.A. after installing or rebuilding.

Regenerate the pipeline diagram after flow changes (`assets/flowchart.mmd`, `tools/render_flowchart.py`, and `assets/flowchart.svg` should stay in sync):

```powershell
python tools/render_flowchart.py
```

Regenerate the README graph-options sample image after graph or options layout changes:

```powershell
python tools/render_readme_graph_section.py
```

---

## Configure

Open **Options → Plugins → AutoFocusGraphs**. Tabs match the sections below.

The options page shows **last post** status (time + outcome) and a **digest-only mode** hint when per-run posts are off but a digest is enabled.

### Graph

Monitoring, graph rendering, quality gate, posting, and digests. A **live preview** (960×540, HiDPI-aware) at the top uses the same renderer as outbound posts. **Expand preview** opens a larger pop-out window. **All overlays on** / **All overlays off** apply every overlay at once (off = minimal graph mode).

![Graph overlays section in plugin options — sample preview (not interactive)](assets/readme-graph-options-section.png)

**Preview sample** — dropdown of synthetic V-curves modeled on common Cloudy Nights / NINA forum patterns. Use it with **Graph analysis hints** on to see which rules fire before you rely on live runs.

| Preview sample | What it models |
| --- | --- |
| **Normal** | Clean V-curve (baseline) |
| **Backlash test** | Right-wing plateau after the minimum (overshoot / backlash) |
| **Low R² (erratic curve)** | Poor fit / weak trend on one side |
| **Poor seeing (high HFR)** | High absolute HFR with an otherwise usable shape |
| **Minimum near edge** | Best focus at the first or last scan points |
| **Outlier point** | One measure point flagged as an outlier |
| **Overshoot IN+OUT** | Both backlash directions set (NINA allows one) |
| **Noisy / wind gusts** | High point-to-point HFR scatter |
| **Step too large** | Coarse V: wings ~4× min HFR, steep sides, 1–2 points at the bottom |
| **Step too small** | Narrow scan; edge HFR only ~1.5× min |
| **Zigzag / seesaw** | Alternating HFR on the approach wing (step vs seeing / guiding) |
| **Flat approach wing** | Flat HFR at the scan edge, then a steep drop into focus |
| **Outer measurement cliff** | Outermost point HFR collapses (stars too defocused to measure) |
| **Flat scan (seeing)** | Almost no HFR change across the whole run |

Focus drift chart example lives under **Session digest → Include trend chart in session digest** (not in this dropdown).

| Overlay | What it shows |
| --- | --- |
| **Minimal graph** | Points and final HFR only — hides fits, trends, markers, and other overlays |
| **HFR point labels** | Measured HFR value next to each point on the V-curve |
| **Hyperbolic fit** | NINA's hyperbolic focus curve; R² in the legend when enabled |
| **Parabolic fit** | Parabolic (quadratic) least-squares curve through measure points |
| **Trend lines** | Left and right linear trend segments, split at the calculated focus position |
| **Trend segment labels** | Left/right trend labels drawn on the graph |
| **Focus position line** | Vertical dashed line at the calculated focus position |
| **Context strip** | Top-right overlay: temperature, step size, run duration, and Δ focus vs previous AF |
| **Previous AF marker** | Gray dotted vertical line at the previous autofocus position |
| **Compare to last (same filter)** | Ghost previous session V-curve (same filter) behind current points |
| **Trend R² in legend** | R² values for left and right trends in the graph legend |
| **Initial focus marker** | Cyan diamond and dotted **Start pos** vertical line at the starting focuser position |
| **HFR error bars** | Vertical bars from each point's HFR uncertainty (JSON `Error` field); off by default |
| **Graph analysis hints** | Optional rule-based observations on the V-curve (lower-left); same rules on live AF runs and preview |

**Graph analysis hints** — when enabled, up to three short observations are drawn on the PNG (live runs and preview). They describe measured facts and curve shape (not diagnoses). **Conservative graph hints** (default on) hides suggestion-tier text.

Examples of patterns the analyzer can surface:

- Post-minimum wing plateau / outer approach plateau
- Steep coarse V (step too large) or shallow wings (step / scan too small)
- Zigzag / seesaw HFR, flat overall scan, outer measurement cliff
- Minimum near scan edge, low R², weak trend side, outliers, noisy points
- Overshoot set on both IN and OUT; large focus shift vs previous run

**Graph options** (separate from overlays):

| Option | What it does |
| --- | --- |
| **Include filter name in graph title** | Appends filter to the title (e.g. `N.I.N.A. Autofocus Run — Ha`) |
| **Warn when fit minimum ≠ calculated focus** | Corner annotation when hyperbolic fit minimum and calculated focus differ by more than one step (`Fit Δ N steps`) |

| Setting | Purpose |
| --- | --- |
| **Enable autofocus graph posts** | Turns monitoring on or off |
| **Quality gate** | Global R² / HFR limits, plus optional per-filter profile rows |
| **Play sound on quality warning** | System exclamation when a run fails the gate |
| **Post when autofocus ends without a JSON report** | Live hook for cancelled or failed autofocus runs |
| **Post failures / unreadable reports** | Alert when a report file exists but cannot be parsed |
| **Post each autofocus run** | Per-run posts on/off (off = digest-only mode) |
| **Per-run send** | When per-run posts are on: **Every run**, **Every Nth run** (N = 2–50; warnings/failures always send), or **Problems only** |
| **Quiet hours** | Local start/end window; successful per-run posts are skipped (warnings/failures/digests still send) |
| **Include V-curve graph on each run** | Attach the 2× PNG to each per-run post |
| **Attach JSON** | Attach the raw AutoFocus JSON file to each per-run post |
| **Graph analysis hints / Conservative hints** | Optional V-curve observations on live graphs and preview; conservative (default) = facts and patterns only |
| **Post digest when sequencer sequence completes** | Sequence digest after the last per-run post (default on) |
| **Post session digest when NINA exits** | Full-session digest on shutdown |
| **Include trend chart in digests** | 2× HFR trend PNG plus focus-drift chart (position vs temperature) when enough runs have both; drift overlays (summary strip, position/filter/HFR labels, trend line) with **All overlays on/off**, expand preview |
| **Post sequence digest now** | Manual digest: current sequence first, then full session if the sequence is empty |
| **Export AF night pack** | Zip of this session: 2× V-curve PNGs, `runs.csv`, trend/drift charts, README, and original AF JSON (forums / support) |
| **Upload delay / message template** | Timing and template tokens |
| **Watch folder** | Read-only AutoFocus path |

**Advanced Sequencer:** **Post AF sequence digest** (category **AutoFocusGraphs**) — posts a sequence digest mid-run to all enabled destinations.

---

### Discord

Channel webhook delivery with rich embeds. No bot token required — posts use the **name and icon configured on your Discord webhook** (Integrations → Webhooks).

- **Success** — blue embed titled `AutoFocus Details — Filter {filter}`
- **Warning** — yellow embed when R² is low or HFR is high
- **Failure** — red embed for bad reports or live AF failures
- Optional role pings (`<@&roleId>`) on warnings and failures

| Setting | Purpose |
| --- | --- |
| **Enable Discord** | Include Discord in per-run and digest posts |
| **Discord webhook URL** | Channel webhook (`https://discord.com/api/webhooks/...`) |
| **Test webhook** | Sends a short test message (green ✓ / red ✗) |
| **Thread ID / nightly forum thread** | Post into a thread, or `AF yyyy-MM-dd` on forum channels |
| **Embed detail / Attachments** | Detailed or compact embeds; graph+embed, graph-only, or embed-only |
| **Discord alert role ID** | Optional numeric role ID to ping on warnings or failures |
| **Ping role on warning / failure** | Adds `<@&roleId>` to webhook content for mobile alerts |

---

### Telegram

Bot API delivery — per-run graph as a photo; JSON as a document when **Attach JSON** is on. Captions use the shared message template (`**bold**` converted for Telegram).

| Setting | Purpose |
| --- | --- |
| **Enable Telegram** | Include Telegram in per-run and digest posts |
| **Telegram bot token** | From [@BotFather](https://t.me/BotFather) (stored locally only) |
| **Telegram chat ID** | Numeric chat ID (e.g. `-1001234567890`) or `@channelusername`; bot must be a member |
| **Test Telegram** | Sends a short test message |

---

### Slack

Web API delivery — graph uploaded via Slack's external file API. Bot scopes: `chat:write` and `files:write`. Invite the bot to the channel before testing (**Test Slack** sends text only).

| Setting | Purpose |
| --- | --- |
| **Enable Slack** | Include Slack in per-run and digest posts |
| **Slack bot token** | Bot User OAuth Token (`xoxb-...`) from your Slack app |
| **Slack channel ID** | Channel ID (`C...` or `G...`); bot must be invited |
| **Test Slack** | Sends a short test message |

---

### Email

SMTP send-only delivery — graph PNG and optional JSON attachment on per-run posts.

**Email subject** — leave blank for defaults:

- In sequence: `NINA AutoFocus Graphs - {sequence} - {date}`
- Manual AF: `NINA Manual AutoFocus Graphs - {date}`

Custom templates supported — tokens: `{sequence}`, `{date}`, `{time}`, `{filter}`, `{shortfilename}`, `{filename}`, `{reason}`.

| Setting | Purpose |
| --- | --- |
| **Enable email** | Include email in per-run and digest posts |
| **SMTP host / port / SSL** | Outgoing mail server (e.g. Gmail, Proton Bridge on `127.0.0.1:1025`) |
| **SMTP username / password** | Credentials; stored locally only |
| **From / To addresses** | Sender and one or more recipients (comma-separated) |
| **Email subject** | Blank = built-in defaults; custom template optional |
| **Test email** | Sends a short test message |

---

## Security

- Webhook URLs, bot tokens, SMTP passwords, and chat IDs are stored only in local N.I.N.A. user settings.
- Outbound posts go only to user-configured Discord webhooks, Telegram chats, Slack channels, and SMTP recipients.
- Only files under the AutoFocus folder are processed, with size and point-count limits.
- Treat webhook URLs and bot tokens like passwords; regenerate them if they are ever exposed.

---

## License

MIT — see [LICENSE](LICENSE).
