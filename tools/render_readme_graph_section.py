"""Render assets/readme-graph-options-section.png for the README Graph section."""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets" / "readme-graph-options-section.png"
GRAPH_TEMP = ROOT / "assets" / "_readme-graph-temp.png"
RENDER_TOOL = ROOT / "tools" / "RenderReadmeGraph" / "RenderReadmeGraph.csproj"

W = 1000
PAD_X = 20
MAX_GRAPH_W = 960
GRAPH_MAX_H = 540

PANEL = (37, 37, 38)
TEXT = (255, 255, 255)
SUB = (176, 176, 176)
GRAPH_BG = (54, 57, 63)
BTN = (62, 62, 66)
BTN_BORDER = (102, 102, 102)
CHECK_ON = (88, 101, 242)
CHECK_OFF = (62, 62, 66)
CHECK_BORDER = (140, 140, 140)

OVERLAYS = [
    ("Minimal graph", False),
    ("HFR point labels", True),
    ("Hyperbolic fit", True),
    ("Parabolic fit", True),
    ("Trend lines", True),
    ("Trend segment labels", True),
    ("Focus position line", True),
    ("Context strip", True),
    ("Previous AF marker", True),
    ("Trend R² in legend", True),
    ("Initial focus marker", True),
    ("HFR error bars", True),
    ("Graph analysis hints", True),
]


def font(size: int, bold: bool = False):
    names = ["segoeui.ttf", "Segoe UI.ttf", "arial.ttf"]
    if bold:
        names = ["segoeuib.ttf", "Segoe UI Bold.ttf", "arialbd.ttf"] + names
    for name in names:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def render_graph_png() -> Path:
    cmd = [
        "dotnet",
        "run",
        "--project",
        str(RENDER_TOOL),
        "-c",
        "Release",
        "--",
        str(GRAPH_TEMP),
    ]
    result = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(
            "Graph render failed:\n"
            + result.stdout
            + result.stderr
        )
    if not GRAPH_TEMP.exists():
        raise RuntimeError(f"Graph render did not create {GRAPH_TEMP}")
    return GRAPH_TEMP


def draw_checkbox(draw: ImageDraw.ImageDraw, x: int, y: int, checked: bool):
    size = 14
    box = (x, y, x + size, y + size)
    fill = CHECK_ON if checked else CHECK_OFF
    draw.rounded_rectangle(box, radius=2, fill=fill, outline=CHECK_BORDER, width=1)
    if checked:
        draw.line([(x + 3, y + 7), (x + 6, y + 10), (x + 11, y + 4)], fill=TEXT, width=2)


def draw_button(draw: ImageDraw.ImageDraw, xy, label: str):
    x0, y0, x1, y1 = xy
    draw.rounded_rectangle(xy, radius=3, fill=BTN, outline=BTN_BORDER, width=1)
    tf = font(11)
    cx = (x0 + x1) // 2
    cy = (y0 + y1) // 2
    draw.text((cx, cy), label, fill=TEXT, font=tf, anchor="mm")


def build_section(graph_path: Path) -> Image.Image:
    graph = Image.open(graph_path).convert("RGBA")
    scale = min(MAX_GRAPH_W / graph.width, GRAPH_MAX_H / graph.height, 1.0)
    graph_w = int(graph.width * scale)
    graph_h = int(graph.height * scale)
    graph = graph.resize((graph_w, graph_h), Image.Resampling.LANCZOS)

    row_h = 28
    grid_rows = (len(OVERLAYS) + 1) // 2
    col_w = (W - PAD_X * 2 - 16) // 2
    h = 16 + 28 + 22 + graph_h + 24 + 32 + 12 + grid_rows * row_h + 20

    img = Image.new("RGB", (W, h), PANEL)
    draw = ImageDraw.Draw(img)
    y = 16

    draw.text((PAD_X, y), "Graph", fill=TEXT, font=font(16, bold=True))
    y += 28
    draw.text(
        (PAD_X, y),
        "Live preview — toggle overlays below; same PNG Discord receives.",
        fill=SUB,
        font=font(11),
    )
    y += 22

    frame_x0 = PAD_X
    frame_y0 = y
    frame_x1 = PAD_X + graph_w + 12
    frame_y1 = y + graph_h + 12
    draw.rounded_rectangle(
        (frame_x0, frame_y0, frame_x1, frame_y1),
        radius=4,
        fill=GRAPH_BG,
    )
    img.paste(graph, (frame_x0 + 6, frame_y0 + 6), graph)
    y = frame_y1 + 12

    draw_button(draw, (PAD_X, y, PAD_X + 118, y + 28), "All overlays on")
    draw_button(draw, (PAD_X + 126, y, PAD_X + 252, y + 28), "All overlays off")
    draw_button(draw, (PAD_X + 260, y, PAD_X + 386, y + 28), "Expand preview")
    y += 40

    label_font = font(11)
    for i, (label, checked) in enumerate(OVERLAYS):
        col = i % 2
        row = i // 2
        x = PAD_X + col * (col_w + 16)
        row_y = y + row * row_h
        draw.text((x, row_y + 1), label, fill=TEXT, font=label_font)
        draw_checkbox(draw, x + col_w - 18, row_y + 2, checked)

    return img


def main() -> int:
    graph_path = render_graph_png()
    try:
        section = build_section(graph_path)
        OUT.parent.mkdir(parents=True, exist_ok=True)
        section.save(OUT, format="PNG", optimize=True)
        print(f"Wrote {OUT}")
    finally:
        if graph_path.exists():
            graph_path.unlink()
    return 0


if __name__ == "__main__":
    sys.exit(main())
