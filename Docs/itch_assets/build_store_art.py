"""Compose the itch.io store art for Cassette Dash! (cover + normalised screenshots)."""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = r"D:\unityproject\Cassette Dash!"
OUT = os.path.join(ROOT, "Docs", "itch_assets")
os.makedirs(OUT, exist_ok=True)

FONT_DIR = r"C:\Windows\Fonts"


def font(names, size):
    for n in names:
        p = os.path.join(FONT_DIR, n)
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


BOLD = ["arialbd.ttf", "seguisb.ttf", "segoeuib.ttf", "calibrib.ttf"]
REG = ["arial.ttf", "segoeui.ttf", "calibri.ttf", "seguisb.ttf"]
CJK = ["msyhbd.ttc", "msyh.ttc", "simhei.ttf", "msjhbd.ttc", "simsun.ttc"]


def cover():
    W, H = 630, 500
    src = Image.open(os.path.join(ROOT, "screenshot_crossbow_aimline.png")).convert("RGB")
    # centre-crop to 630x500 aspect, then downscale
    target_ratio = W / H
    sw, sh = src.size
    if sw / sh > target_ratio:
        nw = int(sh * target_ratio)
        src = src.crop(((sw - nw) // 2, 0, (sw - nw) // 2 + nw, sh))
    else:
        nh = int(sw / target_ratio)
        src = src.crop((0, max(0, (sh - nh) // 2), sw, max(0, (sh - nh) // 2) + nh))
    img = src.resize((W, H), Image.LANCZOS)

    # darken the whole thing a touch + heavy gradient at the top/bottom for text legibility
    img = img.point(lambda v: int(v * 0.86))
    grad = Image.new("L", (1, H))
    for y in range(H):
        t = y / (H - 1)
        if t < 0.55:
            a = int(150 * (1 - t / 0.55))
        else:
            a = int(170 * ((t - 0.55) / 0.45))
        grad.putpixel((0, y), max(0, min(255, a)))
    grad = grad.resize((W, H))
    shade = Image.new("RGB", (W, H), (6, 8, 14))
    img = Image.composite(shade, img, grad)

    d = ImageDraw.Draw(img)
    # accent bar
    d.rectangle([0, 0, W, 6], fill=(250, 208, 56))

    title = font(BOLD, 76)
    sub = font(REG, 25)
    tag = font(CJK, 24)

    def center_text(text, f, y, fill, shadow=(3, 3)):
        l, t, r, b = d.textbbox((0, 0), text, font=f)
        x = (W - (r - l)) / 2 - l
        d.text((x + shadow[0], y + shadow[1]), text, font=f, fill=(0, 0, 0))
        d.text((x, y), text, font=f, fill=fill)

    center_text("CASSETTE", title, 74, (255, 214, 60))
    center_text("DASH!", title, 150, (255, 214, 60))
    center_text("2.5D parkour inside a cassette tape", sub, 250, (238, 242, 255))
    center_text("逃出磁带内部 · 登上列车", tag, H - 92, (255, 236, 170))
    center_text("5 languages · keyboard", sub, H - 54, (206, 214, 232))

    path = os.path.join(OUT, "cover_630x500.png")
    img.save(path, "PNG")
    print("cover ->", path, img.size)


def shots():
    picks = [
        ("screenshot_crossbow_aimline.png", "shot_1_crossbow_aimline.png"),
        ("screenshot_start_chinese.png", "shot_2_title_screen.png"),
        ("screenshot_hud_japanese.png", "shot_3_hud_japanese.png"),
        ("screenshot_game.png", "shot_4_gameplay.png"),
    ]
    for src_name, dst_name in picks:
        p = os.path.join(ROOT, src_name)
        if not os.path.exists(p):
            print("missing", p)
            continue
        im = Image.open(p).convert("RGB")
        # normalise to 1280x720 (16:9) for a consistent store page
        tw, th = 1280, 720
        sw, sh = im.size
        if abs(sw / sh - tw / th) > 0.01:
            if sw / sh > tw / th:
                nw = int(sh * tw / th)
                im = im.crop(((sw - nw) // 2, 0, (sw - nw) // 2 + nw, sh))
            else:
                nh = int(sw * th / tw)
                im = im.crop((0, max(0, (sh - nh) // 2), sw, max(0, (sh - nh) // 2) + nh))
        im = im.resize((tw, th), Image.LANCZOS)
        out = os.path.join(OUT, dst_name)
        im.save(out, "PNG")
        print("shot  ->", out, im.size)


if __name__ == "__main__":
    cover()
    shots()