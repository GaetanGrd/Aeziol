"""Generate Aeziol's SVG, PNG, ICO and MSIX brand assets from one vector drawing.

Development-only dependencies are provided by the Codex workspace runtime:
ReportLab for vector/raster rendering and Pillow for final resizing/ICO output.
The generated files are runtime-independent and remain checked into the project.
"""

from __future__ import annotations

from pathlib import Path
import subprocess
import tempfile

from PIL import Image
from reportlab.graphics import renderSVG
from reportlab.graphics.shapes import Circle, Drawing, Group, Path as VectorPath, Rect
from reportlab.lib.colors import Color, HexColor


ROOT = Path(__file__).resolve().parents[1]
APP_ASSETS = ROOT / "src" / "Aeziol.App" / "Assets"
BRAND_ASSETS = APP_ASSETS / "Brand"
PACKAGE_ASSETS = ROOT / "packaging" / "Assets"

GOLD = HexColor("#DEBD68")
GOLD_LIGHT = HexColor("#F4D98A")
GOLD_DIM = Color(222 / 255, 189 / 255, 104 / 255, alpha=0.28)
GOLD_GHOST = Color(222 / 255, 189 / 255, 104 / 255, alpha=0.10)
INK = HexColor("#090A0C")
SURFACE = HexColor("#101216")
MONO = HexColor("#111111")


def _path(commands: list[tuple], *, fill, stroke, width: float) -> VectorPath:
    path = VectorPath()
    for command in commands:
        name, *values = command
        if name == "M":
            path.moveTo(*values)
        elif name == "L":
            path.lineTo(*values)
        elif name == "C":
            path.curveTo(*values)
        elif name == "Z":
            path.closePath()
        else:
            raise ValueError(f"Unknown path command: {name}")
    path.fillColor = fill
    path.strokeColor = stroke
    path.strokeWidth = width
    path.strokeLineCap = 1
    path.strokeLineJoin = 1
    return path


def _mirror(commands: list[tuple], axis: float = 512) -> list[tuple]:
    mirrored: list[tuple] = []
    for command in commands:
        name, *values = command
        transformed = [axis + (axis - value) if index % 2 == 0 else value for index, value in enumerate(values)]
        mirrored.append((name, *transformed))
    return mirrored


def create_cicada(*, monochrome: bool = False) -> Group:
    primary = MONO if monochrome else GOLD
    light = MONO if monochrome else GOLD_LIGHT
    translucent = None if monochrome else Color(222 / 255, 189 / 255, 104 / 255, alpha=0.08)
    group = Group()

    wing = [
        ("M", 470, 382),
        ("C", 405, 286, 244, 247, 157, 325),
        ("C", 78, 396, 127, 548, 265, 651),
        ("C", 354, 718, 432, 686, 478, 612),
        ("C", 438, 526, 430, 446, 470, 382),
        ("Z",),
    ]
    group.add(_path(wing, fill=translucent, stroke=primary, width=26))
    group.add(_path(_mirror(wing), fill=translucent, stroke=primary, width=26))

    # A single inner curve suggests both a wing fold and a travelling sound.
    fold = [("M", 452, 450), ("C", 352, 365, 236, 350, 168, 402)]
    group.add(_path(fold, fill=None, stroke=primary, width=14))
    group.add(_path(_mirror(fold), fill=None, stroke=primary, width=14))

    head = [
        ("M", 443, 318),
        ("C", 456, 265, 568, 265, 581, 318),
        ("C", 565, 352, 459, 352, 443, 318),
        ("Z",),
    ]
    body = [
        ("M", 512, 340),
        ("C", 462, 340, 447, 420, 459, 508),
        ("C", 470, 604, 490, 702, 512, 770),
        ("C", 534, 702, 554, 604, 565, 508),
        ("C", 577, 420, 562, 340, 512, 340),
        ("Z",),
    ]
    group.add(_path(head, fill=light, stroke=None, width=0))
    group.add(_path(body, fill=light, stroke=None, width=0))

    antenna = [("M", 470, 286), ("C", 421, 226, 367, 225, 329, 252)]
    group.add(_path(antenna, fill=None, stroke=primary, width=18))
    group.add(_path(_mirror(antenna), fill=None, stroke=primary, width=18))

    # ReportLab's coordinate origin is bottom-left. Flip the natural drawing so
    # the cicada's head and antennae face upward in every exported format.
    group.transform = (1, 0, 0, -1, 0, 1024)
    return group


def create_symbol_drawing(*, monochrome: bool = False) -> Drawing:
    drawing = Drawing(1024, 1024)
    drawing.add(create_cicada(monochrome=monochrome))
    return drawing


def create_app_icon() -> Drawing:
    drawing = Drawing(1024, 1024)
    drawing.add(Rect(24, 24, 976, 976, 214, 214, fillColor=INK, strokeColor=None))
    mark = create_cicada()
    mark.scale(0.82, 0.82)
    mark.translate(92, 92)
    drawing.add(mark)
    return drawing


def _write_svg(drawing: Drawing, path: Path, *, title: str) -> None:
    renderSVG.drawToFile(drawing, str(path))
    svg = path.read_text(encoding="utf-8")
    svg = svg.replace("<svg ", f"<svg role=\"img\" aria-label=\"{title}\" ", 1)
    path.write_text(svg, encoding="utf-8", newline="\n")


def _render_png(drawing: Drawing, path: Path, size: tuple[int, int]) -> None:
    edge = Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe")
    if not edge.exists():
        raise RuntimeError("Microsoft Edge is required to rasterize the SVG brand assets.")

    with tempfile.TemporaryDirectory(prefix="aeziol-brand-") as temporary_directory:
        temporary_root = Path(temporary_directory)
        svg_path = temporary_root / "source.svg"
        screenshot_path = temporary_root / "render.png"
        profile_path = temporary_root / "edge-profile"
        renderSVG.drawToFile(drawing, str(svg_path))
        subprocess.run(
            [
                str(edge),
                "--headless=new",
                "--disable-gpu",
                "--hide-scrollbars",
                "--default-background-color=00000000",
                "--force-device-scale-factor=1",
                "--window-size=1024,1024",
                f"--user-data-dir={profile_path}",
                f"--screenshot={screenshot_path}",
                svg_path.as_uri(),
            ],
            check=True,
            capture_output=True,
        )
        with Image.open(screenshot_path) as source:
            output = source.convert("RGBA").resize(size, Image.Resampling.LANCZOS)
            output.save(path, optimize=True)


def main() -> None:
    BRAND_ASSETS.mkdir(parents=True, exist_ok=True)
    PACKAGE_ASSETS.mkdir(parents=True, exist_ok=True)

    symbol = create_symbol_drawing()
    monochrome = create_symbol_drawing(monochrome=True)
    app_icon = create_app_icon()

    _write_svg(symbol, BRAND_ASSETS / "aeziol-cicada.svg", title="Aeziol, cigale lumineuse")
    _write_svg(monochrome, BRAND_ASSETS / "aeziol-cicada-monochrome.svg", title="Aeziol, cigale monochrome")
    _write_svg(app_icon, BRAND_ASSETS / "aeziol-app-icon.svg", title="Icône Aeziol")

    master_png = BRAND_ASSETS / "aeziol-app-icon-1024.png"
    _render_png(app_icon, master_png, (1024, 1024))
    _render_png(app_icon, BRAND_ASSETS / "aeziol-app-icon-256.png", (256, 256))
    _render_png(app_icon, BRAND_ASSETS / "aeziol-discord-icon.png", (1024, 1024))

    with Image.open(master_png) as source:
        source.save(
            APP_ASSETS / "Aeziol.ico",
            format="ICO",
            sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (48, 48), (256, 256)],
        )

    _render_png(app_icon, PACKAGE_ASSETS / "StoreLogo.png", (50, 50))
    _render_png(app_icon, PACKAGE_ASSETS / "Square44x44Logo.png", (44, 44))
    _render_png(app_icon, PACKAGE_ASSETS / "Square150x150Logo.png", (150, 150))

    wide = Drawing(1240, 600)
    wide.add(Rect(0, 0, 1240, 600, fillColor=SURFACE, strokeColor=None))
    wide_mark = create_cicada()
    wide_mark.scale(0.52, 0.52)
    wide_mark.translate(355, 35)
    wide.add(wide_mark)
    _render_png(wide, PACKAGE_ASSETS / "Wide310x150Logo.png", (310, 150))

    print(f"Generated Aeziol brand assets in {BRAND_ASSETS}")


if __name__ == "__main__":
    main()
