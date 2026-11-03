#!/usr/bin/env python3
"""Compose App Store screenshots and banners from the raw simulator captures.

    python3 Tools/store-screens.py

Reads Docs/AppStore/raw/<lang>_<device>_<shot>.png and writes
Docs/AppStore/<lang>/<device>/NN-<shot>.png plus Docs/AppStore/banners/*.png.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "Docs/AppStore/raw"
OUT = ROOT / "Docs/AppStore"
FONT = str(ROOT / "Assets/Resources/Fonts/RammettoOne-Regular.ttf")
ICON = ROOT / "Assets/Resources/Art/app-icon.png"

SKY_TOP, SKY_BOTTOM = (111, 214, 255), (72, 103, 240)
INK, GOLD, WHITE = (31, 37, 80), (255, 200, 58), (255, 255, 255)
BLOCKS = [(242, 71, 77), (63, 140, 255), (255, 200, 58), (61, 203, 108),
          (154, 92, 245), (255, 145, 53), (255, 110, 180), (35, 196, 196)]

# Canvas sizes App Store Connect accepts for the largest iPhone and iPad classes.
SIZES = {"iphone": (1320, 2868), "ipad": (2064, 2752)}

# (shot, headline: | breaks the line, *gold* words; subline)
SCREENS = {
    "en": [
        ("action1", "TAP &|*LOOP!*", "Send blocks around the loop"),
        ("clear", "MATCH|*COLORS*", "Fill a column and watch it pop"),
        ("start", "*MYSTERY*|BLOCKS", "What color hides inside?"),
        ("action3", "SMART|*BOOSTERS*", "Hammer, extra slot and more"),
        ("win", "HUNDREDS|OF *LEVELS*", "Relaxing, satisfying puzzles"),
        ("home", "CUTE *BLOCK*|BUDDIES", "Easy to learn, fun to master"),
    ],
    "tr": [
        ("action1", "DOKUN &|*DÖNDÜR!*", "Blokları döngüye gönder"),
        ("clear", "RENKLERİ|*EŞLE!*", "Sütunu doldur, patlasın"),
        ("start", "*GİZEMLİ*|BLOKLAR", "İçinde hangi renk saklı?"),
        ("action3", "SÜPER|*GÜÇLER*", "Çekiç, ekstra yer ve dahası"),
        ("win", "YÜZLERCE|*BÖLÜM*", "Rahatlatıcı bulmacalar"),
        ("home", "SEVİMLİ|*DOSTLAR*", "Öğrenmesi kolay, bırakması zor"),
    ],
}


def gradient(size):
    w, h = size
    img = Image.new("RGB", size)
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(SKY_TOP, SKY_BOTTOM)))
    # Faint brick studs, like the in-game ground.
    studs = Image.new("RGBA", size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(studs)
    step = w // 11
    r = step * 0.16
    for gy in range(step // 2, h, step):
        for gx in range(step // 2, w + step, step):
            sd.ellipse([gx - r, gy - r, gx + r, gy + r], fill=(255, 255, 255, 28))
    img.paste(studs, (0, 0), studs)
    return img


def rounded(img, radius):
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, img.width - 1, img.height - 1], radius, fill=255)
    out = img.convert("RGBA")
    out.putalpha(mask)
    return out


def shadow(canvas, box, radius, blur, offset, alpha=110):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    x0, y0, x1, y1 = box
    ImageDraw.Draw(layer).rounded_rectangle([x0, y0 + offset, x1, y1 + offset], radius, fill=(18, 22, 60, alpha))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    canvas.alpha_composite(layer)


def block(canvas, cx, cy, size, color, angle):
    """A little faced brick, echoing the in-game blocks."""
    s = int(size)
    tile = Image.new("RGBA", (s * 2, s * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(tile)
    o = s // 2
    dark = tuple(int(c * 0.78) for c in color)
    d.rounded_rectangle([o, o + s * 0.08, o + s, o + s * 1.08], s * 0.22, fill=dark)
    d.rounded_rectangle([o, o, o + s, o + s], s * 0.22, fill=color)
    for sx in (0.3, 0.7):
        d.ellipse([o + s * sx - s * 0.12, o + s * 0.1, o + s * sx + s * 0.12, o + s * 0.3], fill=tuple(min(255, c + 40) for c in color))
    for ex in (0.36, 0.64):
        d.ellipse([o + s * ex - s * 0.05, o + s * 0.48, o + s * ex + s * 0.05, o + s * 0.6], fill=INK)
    d.arc([o + s * 0.4, o + s * 0.55, o + s * 0.6, o + s * 0.75], 20, 160, fill=INK, width=max(2, s // 28))
    tile = tile.rotate(angle, resample=Image.BICUBIC)
    canvas.alpha_composite(tile, (int(cx - s), int(cy - s)))


def fit_font(text, max_w, start):
    size = start
    while size > 20:
        f = ImageFont.truetype(FONT, size)
        if f.getlength(text.replace("*", "")) <= max_w:
            return f
        size -= 4
    return ImageFont.truetype(FONT, size)


def headline(canvas, text, top, max_w, start):
    """Draws each |-separated line centred; returns the y below the last line."""
    lines = text.split("|")
    f = fit_font(max(lines, key=lambda l: ImageFont.truetype(FONT, start).getlength(l.replace("*", ""))), max_w, start)
    d = ImageDraw.Draw(canvas)
    stroke = max(4, f.size // 9)
    gold = False
    cy = top + f.size * 0.55
    for line in lines:
        x = (canvas.width - f.getlength(line.replace("*", ""))) / 2
        for part in line.split("*"):
            if part:
                color = GOLD if gold else WHITE
                d.text((x, cy + stroke * 0.9), part, font=f, fill=INK, anchor="lm", stroke_width=stroke, stroke_fill=INK)
                d.text((x, cy), part, font=f, fill=color, anchor="lm", stroke_width=stroke, stroke_fill=INK)
                x += f.getlength(part)
            gold = not gold
        gold = False
        cy += f.size * 1.12
    return cy - f.size * 0.55


def subline(canvas, text, cy, size):
    f = ImageFont.truetype(FONT, size)
    d = ImageDraw.Draw(canvas)
    w = f.getlength(text)
    pad_x, pad_y = size * 0.9, size * 0.55
    box = [canvas.width / 2 - w / 2 - pad_x, cy - size / 2 - pad_y, canvas.width / 2 + w / 2 + pad_x, cy + size / 2 + pad_y]
    d.rounded_rectangle(box, (box[3] - box[1]) / 2, fill=(31, 37, 80, 210))
    d.text((canvas.width / 2, cy), text, font=f, fill=WHITE, anchor="mm")


def compose(device, shot_path, head, sub, seed):
    W, H = SIZES[device]
    canvas = gradient((W, H)).convert("RGBA")
    shot = Image.open(shot_path).convert("RGB")

    # Decorative bricks peeking from the sides.
    import random
    rng = random.Random(seed)
    unit = W / 11
    for side in (0, 1):
        for _ in range(2):
            cx = (rng.uniform(-0.2, 0.6) if side == 0 else rng.uniform(10.4, 11.2)) * unit
            cy = rng.uniform(0.32, 0.92) * H
            block(canvas, cx, cy, unit * rng.uniform(1.1, 1.5), rng.choice(BLOCKS), rng.uniform(-25, 25))

    sub_size = int(W * (0.042 if device == "iphone" else 0.032))
    below = headline(canvas, head, H * 0.035, W * 0.88, int(W * (0.12 if device == "iphone" else 0.085)))
    subline(canvas, sub, below + sub_size * 1.3, sub_size)
    top = below + sub_size * 3.2

    # The capture sits in a chunky ink frame, like a toy device.
    bottom_margin = H * 0.035
    frame_h = H - top - bottom_margin
    border = int(W * 0.022)
    inner_h = frame_h - 2 * border
    inner_w = inner_h * shot.width / shot.height
    if inner_w + 2 * border > W * 0.86:
        inner_w = W * 0.86 - 2 * border
        inner_h = inner_w * shot.height / shot.width
    inner_w, inner_h = int(inner_w), int(inner_h)
    fw, fh = inner_w + 2 * border, inner_h + 2 * border
    fx, fy = (W - fw) // 2, int(top)
    radius = int(fw * (0.11 if device == "iphone" else 0.05))
    shadow(canvas, (fx, fy, fx + fw, fy + fh), radius, W * 0.02, W * 0.02)
    frame = Image.new("RGBA", (fw, fh), INK + (255,))
    frame = rounded(frame, radius)
    canvas.alpha_composite(frame, (fx, fy))
    screen = rounded(shot.resize((inner_w, inner_h), Image.LANCZOS), radius - border)
    canvas.alpha_composite(screen, (fx + border, fy + border))
    return canvas.convert("RGB")


def banner(size, title, tagline, raw_shots, path):
    W, H = size
    canvas = gradient((W, H)).convert("RGBA")
    rng = __import__("random").Random(7)
    unit = H / 5
    placed = 0
    while placed < 7:
        x, y = rng.uniform(0, W * 0.58), rng.uniform(0, H)
        # Keep the icon and the title readable: bricks only around them.
        if x < W * 0.5 and H * 0.08 < y < H * 0.92:
            continue
        block(canvas, x, y, unit * rng.uniform(0.6, 1.0), rng.choice(BLOCKS), rng.uniform(-30, 30))
        placed += 1
    icon = Image.open(ICON).convert("RGBA")
    isz = int(H * 0.36)
    icon = rounded(icon.resize((isz, isz), Image.LANCZOS), int(isz * 0.22))
    ix, iy = int(W * 0.06), int(H * 0.16)
    shadow(canvas, (ix, iy, ix + isz, iy + isz), int(isz * 0.22), H * 0.02, H * 0.02)
    canvas.alpha_composite(icon, (ix, iy))
    d = ImageDraw.Draw(canvas)
    f = fit_font(title, W * 0.5, int(H * 0.16))
    tx, ty = ix, iy + isz + H * 0.14
    d.text((tx, ty + f.size * 0.08), title, font=f, fill=INK, anchor="lm", stroke_width=f.size // 9, stroke_fill=INK)
    d.text((tx, ty), title, font=f, fill=WHITE, anchor="lm", stroke_width=f.size // 9, stroke_fill=INK)
    g = fit_font(tagline, W * 0.5, int(H * 0.075))
    d.text((tx, ty + f.size * 0.95), tagline, font=g, fill=GOLD, anchor="lm", stroke_width=max(3, g.size // 8), stroke_fill=INK)
    # Two phones on the right, tilted.
    ph = int(H * 1.05)
    for i, shot_path in enumerate(raw_shots):
        shot = Image.open(shot_path).convert("RGB")
        pw = int(ph * shot.width / shot.height)
        border = max(6, pw // 22)
        frame = Image.new("RGBA", (pw + 2 * border, ph + 2 * border), INK + (255,))
        frame = rounded(frame, int(pw * 0.14))
        frame.alpha_composite(rounded(shot.resize((pw, ph), Image.LANCZOS), int(pw * 0.12)), (border, border))
        frame = frame.rotate(8 - i * 12, expand=True, resample=Image.BICUBIC)
        x = int(W * (0.6 + i * 0.19))
        y = int(H * (0.12 + i * 0.1))
        canvas.alpha_composite(frame, (x, y)) if x + frame.width > 0 else None
    canvas.convert("RGB").save(path)


def main():
    for lang, screens in SCREENS.items():
        for device in SIZES:
            folder = OUT / lang / device
            folder.mkdir(parents=True, exist_ok=True)
            n = 0
            for i, (shot, head, sub) in enumerate(screens):
                src = RAW / f"{lang}_{device}_{shot}.png"
                if not src.exists():
                    src = RAW / f"en_{device}_{shot}.png"
                if not src.exists():
                    continue
                n += 1
                compose(device, src, head, sub, seed=i).save(folder / f"{n:02d}-{shot}.png")
            print(lang, device, n)
    banners = OUT / "banners"
    banners.mkdir(exist_ok=True)
    for lang, tagline in (("en", "Tap. Loop. Match!"), ("tr", "Dokun. Döndür. Eşle!")):
        shots = [RAW / f"{lang}_iphone_action1.png", RAW / f"{lang}_iphone_clear.png"]
        banner((1024, 500), "Parkology", tagline, shots, banners / f"feature-1024x500-{lang}.png")
        banner((1920, 1080), "Parkology", tagline, shots, banners / f"header-1920x1080-{lang}.png")
        banner((1500, 500), "Parkology", tagline, shots, banners / f"header-1500x500-{lang}.png")
    print("banners ok")


if __name__ == "__main__":
    main()
