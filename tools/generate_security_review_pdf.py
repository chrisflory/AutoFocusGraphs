"""Generate AutoFocusGraphs security & bug review PDF for whitepapers folder."""

from __future__ import annotations

from datetime import date
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUT = Path(r"Z:\Backups\Cursor\Whitepapers\AutoFocusGraphs-Security-Bug-Review-2026-07-08.pdf")


def build_pdf(output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    doc = SimpleDocTemplate(
        str(output_path),
        pagesize=letter,
        leftMargin=0.85 * inch,
        rightMargin=0.85 * inch,
        topMargin=0.75 * inch,
        bottomMargin=0.75 * inch,
        title="AutoFocusGraphs Security & Bug Review",
    )

    styles = getSampleStyleSheet()
    title = ParagraphStyle(
        "TitleCustom",
        parent=styles["Title"],
        fontSize=18,
        spaceAfter=10,
    )
    h2 = ParagraphStyle(
        "H2Custom",
        parent=styles["Heading2"],
        fontSize=13,
        spaceBefore=14,
        spaceAfter=8,
        textColor=colors.HexColor("#111111"),
    )
    body = ParagraphStyle(
        "BodyCustom",
        parent=styles["BodyText"],
        fontSize=10,
        leading=14,
        spaceAfter=8,
    )
    small = ParagraphStyle(
        "SmallCustom",
        parent=styles["BodyText"],
        fontSize=9,
        leading=12,
        textColor=colors.HexColor("#555555"),
    )

    story = []
    story.append(Paragraph("AutoFocusGraphs — Security, Bug &amp; Error Review", title))
    story.append(
        Paragraph(
            "<b>Project:</b> AutoFocusGraphs NINA Plugin<br/>"
            "<b>Version reviewed:</b> 0.1.0.0<br/>"
            f"<b>Review date:</b> {date.today().strftime('%B %d, %Y')}<br/>"
            "<b>Repository:</b> https://github.com/chrisflory/AutoFocusGraphs<br/>"
            "<b>Reviewer:</b> Automated code review + manual verification",
            small,
        )
    )
    story.append(Spacer(1, 8))
    story.append(
        Paragraph(
            "<b>Executive summary:</b> Security posture is <b>sound</b> for the plugin threat model "
            "(local NINA user, user-owned outbound credentials). A deep scrub on July 8, 2026 "
            "hardened API response handling, outbound URL validation, SMTP TLS, failure-post logging, "
            "and digest edge cases. Release build succeeds with zero errors.",
            body,
        )
    )

    story.append(Paragraph("1. Scope &amp; Methodology", h2))
    story.append(
        Paragraph(
            "Full review of 49 C# source files covering HTTP clients (Discord, Telegram, Slack), "
            "SMTP email, file watcher I/O, JSON parsing, threading/async, digest orchestration, "
            "and WPF options UI. Threat model: single-user Windows desktop; watches "
            "<font face='Courier'>%LocalAppData%\\NINA\\AutoFocus</font>; posts only to "
            "user-configured destinations.",
            body,
        )
    )

    story.append(Paragraph("2. Security Fixes Applied (July 8, 2026)", h2))
    sec_rows = [
        ["Area", "Fix"],
        [
            "Slack upload URLs",
            "New HttpsFetchUrlValidator blocks private/loopback hosts; upload URLs must be *.slack.com / slack-edge / slack-files.",
        ],
        [
            "Discord avatar fetch",
            "Custom AvatarUrl fetches allowlisted to GitHub raw + Discord CDNs only; private networks blocked.",
        ],
        [
            "Telegram / Slack API",
            "HTTP 200 with missing ok:true or unparseable JSON now fails instead of silent success.",
        ],
        [
            "Remote SMTP TLS",
            "Changed from StartTlsWhenAvailable to required StartTls for non-local hosts.",
        ],
    ]
    t = Table(sec_rows, colWidths=[1.35 * inch, 4.85 * inch])
    t.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#f0f0f0")),
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 9),
                ("GRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#bbbbbb")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    story.append(t)

    story.append(Paragraph("3. Bug &amp; Reliability Fixes Applied", h2))
    bug_rows = [
        ["Severity", "Area", "Fix"],
        ["Medium", "Failure posts", "Slack/Email/Telegram log success and PostStatusTracker only after send completes."],
        ["Medium", "Sequence digest", "GetSequenceNameForDigest() uses pending or last completed sequence name."],
        ["Low", "DigestIncludeTodayFromDisk", "Wired into session digest merge (setting defaults false)."],
        ["Low", "AutofocusRunTracker", "HasNewReportSince() reads session state under single lock."],
        ["Low", "Graph preview CTS", "CancellationTokenSource disposed on refresh and plugin teardown."],
    ]
    t2 = Table(bug_rows, colWidths=[0.75 * inch, 1.35 * inch, 4.1 * inch])
    t2.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#f0f0f0")),
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 9),
                ("GRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#bbbbbb")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    story.append(t2)

    story.append(Paragraph("4. Security Controls Already in Place", h2))
    for item in [
        "Discord webhook SSRF whitelist (WebhookUrlValidator): HTTPS, discord.com hosts, webhook path shape.",
        "Autofocus JSON reads path-confined to AutoFocus folder; 2 MB max; 500 measure-point cap.",
        "Telegram/Slack token format validation; Discord role ping ID validation.",
        "Independent per-destination posting — one channel failing does not block others.",
    ]:
        story.append(Paragraph(f"• {item}", body))

    story.append(Paragraph("5. Accepted Risks (Not Blockers)", h2))
    risk_rows = [
        ["Risk", "Notes"],
        [
            "Secrets in user.config",
            "Webhook URL, bot tokens, SMTP password stored in NINA user settings (standard for plugins).",
        ],
        [
            "Proton Bridge TLS bypass",
            "Certificate validation disabled only for 127.0.0.1 / localhost (required for local bridge).",
        ],
        [
            "SMTP host unrestricted",
            "Users may point at internal mail servers; intentional for self-hosted SMTP.",
        ],
        [
            "Transitive package advisories",
            "MailKit/MimeKit/NCalc warnings from NINA dependency tree; not introduced by this plugin.",
        ],
    ]
    t3 = Table(risk_rows, colWidths=[1.5 * inch, 4.7 * inch])
    t3.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#f0f0f0")),
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 9),
                ("GRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#bbbbbb")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    story.append(t3)

    story.append(Paragraph("6. Build Verification", h2))
    story.append(
        Paragraph(
            "Release configuration builds successfully with <font face='Courier'>dotnet build -c Release</font>. "
            "Close NINA before local deploy to avoid DLL file locks.",
            body,
        )
    )

    story.append(Paragraph("7. Overall Verdict", h2))
    verdict_rows = [
        ["Category", "Status"],
        ["Security", "Ship-ready for beta catalog (0.1.0.0)"],
        ["Core happy path", "Verified — multi-destination per-run posts, digests, graph preview"],
        ["Edge cases", "Addressed in July 8 scrub"],
    ]
    t4 = Table(verdict_rows, colWidths=[1.5 * inch, 4.7 * inch])
    t4.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#f0f0f0")),
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 9),
                ("GRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#bbbbbb")),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    story.append(t4)

    story.append(Spacer(1, 16))
    story.append(
        Paragraph(
            "Generated for AutoFocusGraphs · Chris Flory · MIT License. "
            "This report documents automated and manual review findings; it is not a formal penetration test or certification.",
            small,
        )
    )

    doc.build(story)


if __name__ == "__main__":
    out = DEFAULT_OUT
    build_pdf(out)
    print(f"Wrote {out}")
