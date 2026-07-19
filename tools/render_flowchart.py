"""Render assets/flowchart.png — horizontal layout at 2x DPI (Pillow-only)."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets" / "flowchart.png"

# Logical layout size (1x). Output is SCALE times larger for HiDPI / expand zoom.
SCALE = 2
BASE_W, BASE_H = 1180, 520
W, H = BASE_W * SCALE, BASE_H * SCALE

BG = (47, 49, 54)
BOX = (54, 57, 63)
BOX_BORDER = (114, 118, 125)
NINA = (88, 101, 242)
TEXT = (255, 255, 255)
SUB = (185, 187, 190)
ARROW = (185, 187, 190)
CONTAINER_BORDER = (114, 118, 125)

BORDER_SEQUENCE = (87, 242, 135)
BORDER_SESSION = (155, 89, 182)
BORDER_PACK = (52, 152, 219)
BORDER_FAILURE = (237, 66, 69)
BORDER_DEST = (87, 242, 135)
BORDER_GRAPH = (0, 212, 255)
BORDER_QUALITY = (243, 156, 18)
BORDER_DELAY = (149, 165, 166)


def s(v: float) -> int:
    return int(round(v * SCALE))


def sx(*vals: float):
    return tuple(s(v) for v in vals)


def font(size: int, bold: bool = False):
    names = ["segoeui.ttf", "Segoe UI.ttf", "arial.ttf"]
    if bold:
        names = ["segouib.ttf", "Segoe UI Bold.ttf", "arialbd.ttf"] + names
    scaled = max(1, s(size))
    for name in names:
        try:
            return ImageFont.truetype(name, scaled)
        except OSError:
            continue
    return ImageFont.load_default()


def box(draw, xy, fill=BOX, outline=BOX_BORDER, width=2):
    draw.rounded_rectangle(sx(*xy), radius=s(8), fill=fill, outline=outline, width=max(1, s(width)))


def center_text(draw, xy, title, subtitle=None, title_fill=TEXT, sub_fill=SUB):
    x0, y0, x1, y1 = sx(*xy)
    cx = (x0 + x1) // 2
    tf = font(12, bold=True)
    draw.text((cx, y0 + s(15)), title, fill=title_fill, font=tf, anchor="mm")
    if subtitle:
        sf = font(10)
        lines = subtitle.split("\n")
        start_y = y0 + s(32 if len(lines) == 1 else 28)
        for i, line in enumerate(lines):
            draw.text((cx, start_y + i * s(13)), line, fill=sub_fill, font=sf, anchor="mm")


def arrow_h(draw, x0, y0, x1, y1):
    x0, y0, x1, y1 = s(x0), s(y0), s(x1), s(y1)
    draw.line([x0, y0, x1, y1], fill=ARROW, width=max(1, s(2)))
    tip = s(8) if x1 > x0 else -s(8)
    draw.polygon([(x1, y1), (x1 - tip, y1 - s(5)), (x1 - tip, y1 + s(5))], fill=ARROW)


def branch(draw, x_from, y_from, x_to, y_to):
    mid_x = x_from + 36
    draw.line(sx(x_from, y_from, mid_x, y_from), fill=ARROW, width=max(1, s(2)))
    if y_from != y_to:
        draw.line(sx(mid_x, y_from, mid_x, y_to), fill=ARROW, width=max(1, s(2)))
    arrow_h(draw, mid_x, y_to, x_to, y_to)


def styled_box(draw, xy, title, subtitle=None, outline=BOX_BORDER, width=2):
    box(draw, xy, outline=outline, width=width)
    center_text(draw, xy, title, subtitle)


def main():
    img = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(img)

    draw.rounded_rectangle(
        sx(72, 28, BASE_W - 16, BASE_H - 16),
        radius=s(12),
        outline=CONTAINER_BORDER,
        width=max(1, s(2)),
    )
    draw.text(sx(88, 38), "v0.1.0.2", fill=SUB, font=font(12, bold=True))

    box(draw, (8, 208, 72, 272), NINA)
    center_text(draw, (8, 208, 72, 272), "NINA\nwrites\nAF JSON")
    arrow_h(draw, 72, 240, 88, 240)

    watcher = (88, 212, 198, 268)
    settle = (208, 212, 318, 268)
    parse = (328, 212, 478, 268)

    styled_box(draw, watcher, "Folder watcher")
    arrow_h(draw, watcher[2], 240, settle[0], 240)
    styled_box(draw, settle, "File settle", "wait for JSON")
    arrow_h(draw, settle[2], 240, parse[0], 240)
    styled_box(draw, parse, "Parse + ReportStore", "session + sequence")
    hub_x, hub_y = parse[2], 240

    targets = [
        ((960, 48, 1150, 100), "Sequence digest", "at sequence end\nname / enabled destinations", BORDER_SEQUENCE, 3),
        ((960, 112, 1150, 164), "Session digest", "NINA exit / this session only", BORDER_SESSION, 3),
        ((960, 176, 1150, 228), "AF night pack", "manual zip export\nPNG / CSV / JSON", BORDER_PACK, 3),
        ((960, 240, 1150, 292), "Failure posts", "bad JSON / live AF (no file)", BORDER_FAILURE, 3),
    ]
    for xy, title, sub, outline, width in targets:
        cy = (xy[1] + xy[3]) / 2
        branch(draw, hub_x, hub_y, xy[0], cy)
        styled_box(draw, xy, title, sub, outline=outline, width=width)

    chain_y = 360
    delay = (328, chain_y, 448, chain_y + 56)
    qg = (468, chain_y, 588, chain_y + 56)
    vc = (608, chain_y, 768, chain_y + 56)
    pr = (788, chain_y, 1150, chain_y + 56)

    branch(draw, hub_x, hub_y, delay[0], chain_y + 28)
    styled_box(draw, delay, "Upload delay", "optional", outline=BORDER_DELAY, width=2)
    arrow_h(draw, delay[2], chain_y + 28, qg[0], chain_y + 28)
    styled_box(draw, qg, "Quality gate", "R2 / HFR", outline=BORDER_QUALITY, width=3)
    arrow_h(draw, qg[2], chain_y + 28, vc[0], chain_y + 28)
    styled_box(draw, vc, "V-curve PNG", "overlays / hints / 2x export", outline=BORDER_GRAPH, width=3)
    arrow_h(draw, vc[2], chain_y + 28, pr[0], chain_y + 28)
    styled_box(
        draw,
        pr,
        "Per-run destination post",
        "Discord / Telegram / Slack / Email\nper-run send · quiet hours",
        outline=BORDER_DEST,
        width=3,
    )

    OUT.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT, format="PNG", optimize=True)
    print(f"Wrote {OUT} ({W}x{H}, {SCALE}x)")


if __name__ == "__main__":
    main()
