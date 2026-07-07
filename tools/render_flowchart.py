"""Render assets/flowchart.png — horizontal v1.3-style layout (Pillow-only)."""

from pathlib import Path



from PIL import Image, ImageDraw, ImageFont



ROOT = Path(__file__).resolve().parents[1]

OUT = ROOT / "assets" / "flowchart.png"



W, H = 1180, 460

BG = (47, 49, 54)

BOX = (54, 57, 63)

BOX_BORDER = (114, 118, 125)

NINA = (88, 101, 242)

TEXT = (255, 255, 255)

SUB = (185, 187, 190)

ARROW = (185, 187, 190)

CONTAINER_BORDER = (114, 118, 125)



# Accent borders — dark fill + colored outline (readable white text)

BORDER_SEQUENCE = (87, 242, 135)   # green — new sequence digest

BORDER_SESSION = (155, 89, 182)     # purple — session digest

BORDER_FAILURE = (237, 66, 69)      # red — failure / bad JSON

BORDER_DISCORD = (87, 242, 135)     # green — Discord webhook posts

BORDER_GRAPH = (0, 212, 255)       # cyan — V-curve PNG

BORDER_QUALITY = (243, 156, 18)    # amber — quality gate

BORDER_DELAY = (149, 165, 166)     # grey-blue — optional delay





def font(size: int, bold: bool = False):

    names = ["segoeui.ttf", "Segoe UI.ttf", "arial.ttf"]

    if bold:

        names = ["segouib.ttf", "Segoe UI Bold.ttf", "arialbd.ttf"] + names

    for name in names:

        try:

            return ImageFont.truetype(name, size)

        except OSError:

            continue

    return ImageFont.load_default()





def box(draw, xy, fill=BOX, outline=BOX_BORDER, width=2):

    draw.rounded_rectangle(xy, radius=8, fill=fill, outline=outline, width=width)





def center_text(draw, xy, title, subtitle=None, title_fill=TEXT, sub_fill=SUB):

    x0, y0, x1, y1 = xy

    cx = (x0 + x1) // 2

    tf = font(12, bold=True)

    draw.text((cx, y0 + 15), title, fill=title_fill, font=tf, anchor="mm")

    if subtitle:

        sf = font(10)

        lines = subtitle.split("\n")

        start_y = y0 + 32 if len(lines) == 1 else y0 + 28

        for i, line in enumerate(lines):

            draw.text((cx, start_y + i * 13), line, fill=sub_fill, font=sf, anchor="mm")





def arrow_h(draw, x0, y0, x1, y1):

    draw.line([x0, y0, x1, y1], fill=ARROW, width=2)

    tip = 8 if x1 > x0 else -8

    draw.polygon([(x1, y1), (x1 - tip, y1 - 5), (x1 - tip, y1 + 5)], fill=ARROW)





def branch(draw, x_from, y_from, x_to, y_to):

    mid_x = x_from + 36

    draw.line([x_from, y_from, mid_x, y_from], fill=ARROW, width=2)

    if y_from != y_to:

        draw.line([mid_x, y_from, mid_x, y_to], fill=ARROW, width=2)

    arrow_h(draw, mid_x, y_to, x_to, y_to)





def styled_box(draw, xy, title, subtitle=None, outline=BOX_BORDER, width=2):

    box(draw, xy, outline=outline, width=width)

    center_text(draw, xy, title, subtitle)





def main():

    img = Image.new("RGB", (W, H), BG)

    draw = ImageDraw.Draw(img)



    draw.rounded_rectangle((72, 28, W - 16, H - 16), radius=12, outline=CONTAINER_BORDER, width=2)

    draw.text((88, 38), "v1.3.1.4", fill=SUB, font=font(12, bold=True))



    # External trigger

    box(draw, (8, 178, 72, 242), NINA)

    center_text(draw, (8, 178, 72, 242), "NINA\nwrites\nAF JSON")

    arrow_h(draw, 72, 210, 88, 210)



    # Entry chain (core — neutral grey)

    watcher = (88, 182, 198, 238)

    settle = (208, 182, 318, 238)

    parse = (328, 182, 478, 238)



    styled_box(draw, watcher, "Folder watcher")

    arrow_h(draw, watcher[2], 210, settle[0], 210)

    styled_box(draw, settle, "File settle", "wait for JSON")

    arrow_h(draw, settle[2], 210, parse[0], 210)

    styled_box(draw, parse, "Parse + ReportStore", "session + sequence")

    hub_x, hub_y = parse[2], 210



    # Parallel outputs (top → bottom) — each path has its own accent

    targets = [

        ((960, 48, 1150, 100), "Sequence digest", "at sequence end\nname · wait → Discord", BORDER_SEQUENCE, 3),

        ((960, 118, 1150, 170), "Session digest", "NINA exit · this session only", BORDER_SESSION, 3),

        ((960, 188, 1150, 240), "Failure posts", "bad JSON · live AF (no file)", BORDER_FAILURE, 3),

    ]

    for xy, title, sub, outline, width in targets:

        cy = (xy[1] + xy[3]) // 2

        branch(draw, hub_x, hub_y, xy[0], cy)

        styled_box(draw, xy, title, sub, outline=outline, width=width)



    # Per-run chain — color steps toward Discord post

    chain_y = 308

    delay = (328, chain_y, 448, chain_y + 56)

    qg = (468, chain_y, 588, chain_y + 56)

    vc = (608, chain_y, 768, chain_y + 56)

    pr = (788, chain_y, 1150, chain_y + 56)



    branch(draw, hub_x, hub_y, delay[0], chain_y + 28)

    styled_box(draw, delay, "Upload delay", "optional", outline=BORDER_DELAY, width=2)

    arrow_h(draw, delay[2], chain_y + 28, qg[0], chain_y + 28)

    styled_box(draw, qg, "Quality gate", "R² · HFR", outline=BORDER_QUALITY, width=3)

    arrow_h(draw, qg[2], chain_y + 28, vc[0], chain_y + 28)

    styled_box(draw, vc, "V-curve PNG", "overlays · hints · preview", outline=BORDER_GRAPH, width=3)

    arrow_h(draw, vc[2], chain_y + 28, pr[0], chain_y + 28)

    styled_box(draw, pr, "Per-run Discord post", "embed · graph · JSON", outline=BORDER_DISCORD, width=3)



    OUT.parent.mkdir(parents=True, exist_ok=True)

    img.save(OUT, format="PNG", optimize=True)

    print(f"Wrote {OUT}")





if __name__ == "__main__":

    main()

