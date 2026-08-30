"""스팀 스토어 캡슐 · 앱 아이콘 빌더.

입력  : docs/store-assets/raw/KEY_Wide.png · KEY_Tall.png · KEY_Hero.png · KEY_Icon.png
출력  : docs/store-assets/*.png|jpg  (+ Assets/Materials/UI/Icons/AppIcon_1024.png)

하는 일
  1. Gemini 반짝이 워터마크(우하단) 제거 — 전 장 예외 없이
  2. 목표 비율로 cover-crop
  3. 제목 워드마크(Canadian Mouse) 합성 — 폰트 렌더, AI 아님
  4. 규격 픽셀에 LANCZOS로 정확히 리샘플

사양의 원본은 docs/design-vault/스토어 아트 프롬프트.md 이다.
"""
from __future__ import annotations

import os
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "docs", "store-assets", "raw")
OUT = os.path.join(ROOT, "docs", "store-assets")
ICON_UNITY = os.path.join(ROOT, "Assets", "Materials", "UI", "Icons")

FONT_PATH = r"C:\Windows\Fonts\ariblk.ttf"
TITLE_LINES = ["CANADIAN", "MOUSE"]

CHEESE = (255, 216, 77)
INK = (10, 15, 22)
TRACKING = 0.04          # 자간 (em 비율)
STROKE = 0.075           # 외곽선 굵기 (em 비율)
LINE_GAP = 0.14          # 줄 간격 (em 비율)


# ---------------------------------------------------------------- 워터마크

def strip_watermark(img: Image.Image) -> Image.Image:
    """우하단 Gemini 반짝이(✦) 제거. 밝기 이상치를 마스크로 잡아 inpaint."""
    rgb = img.convert("RGB")
    w, h = rgb.size
    # 워터마크는 우하단 모서리에서 "고정 픽셀 오프셋"에 찍힌다(비율 아님).
    # 2048px·2752px 출력 실측: 오른쪽에서 약 230px, 아래에서 230~260px.
    s = max(w, h) / 2048.0
    x0, x1 = max(0, w - int(360 * s)), w - int(90 * s)
    y0, y1 = max(0, h - int(400 * s)), h - int(90 * s)

    arr = np.array(rgb)
    box = arr[y0:y1, x0:x1]
    gray = cv2.cvtColor(box, cv2.COLOR_RGB2GRAY).astype(np.float32)
    # 별은 "국소적으로 밝은 이상치"다. 저주파 베이스라인과의 차로 잡는다.
    # 중앙값 블러는 커널이 별보다 작으면 별 내부가 통째로 베이스라인이 돼 구멍이 뚫린다.
    base = cv2.GaussianBlur(gray, (0, 0), max(box.shape) / 6.0)
    mask = ((gray - base) > 15).astype(np.uint8) * 255
    k = max(9, int(21 * s) | 1)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((k, k), np.uint8))
    cnts, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    cv2.drawContours(mask, cnts, -1, 255, cv2.FILLED)      # 구멍 메우기
    ratio = mask.mean() / 255.0
    if ratio < 0.002 or ratio > 0.45:      # 검출 실패 → 별 예상 위치만 고정 박스로
        mask = np.zeros(gray.shape, np.uint8)
        cy, cx = mask.shape[0] - int(170 * s), mask.shape[1] - int(140 * s)
        r = int(110 * s)
        cv2.rectangle(mask, (cx - r, cy - r), (cx + r, cy + r), 255, cv2.FILLED)
    mask = cv2.dilate(mask, np.ones((int(13 * s) | 1, int(13 * s) | 1), np.uint8), 2)

    full = np.zeros((h, w), np.uint8)
    full[y0:y1, x0:x1] = mask
    fixed = cv2.inpaint(arr, full, int(16 * s), cv2.INPAINT_TELEA)
    fixed = cv2.inpaint(fixed, full, int(16 * s), cv2.INPAINT_TELEA)
    return Image.fromarray(fixed)


# ---------------------------------------------------------------- 워드마크

def _line_layout(font: ImageFont.FreeTypeFont, text: str, track: float):
    xs, x = [], 0.0
    for ch in text:
        xs.append(x)
        x += font.getlength(ch) + track
    return xs, x - track


def render_wordmark(em: int = 420) -> Image.Image:
    """Canadian Mouse 워드마크를 RGBA로 렌더한다 (그림자 포함, 여백 크롭됨)."""
    font = ImageFont.truetype(FONT_PATH, em)
    track = TRACKING * em
    stroke = max(2, int(STROKE * em))
    ascent, descent = font.getmetrics()
    line_h = ascent + descent
    gap = int(LINE_GAP * em)

    layouts = [_line_layout(font, t, track) for t in TITLE_LINES]
    width = int(max(l[1] for l in layouts)) + stroke * 4
    height = line_h * len(TITLE_LINES) + gap * (len(TITLE_LINES) - 1) + stroke * 4

    art = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    for pas in (0, 1):                      # 0=외곽선 전체, 1=글자면
        for i, text in enumerate(TITLE_LINES):
            xs, lw = layouts[i]
            ox = (width - lw) / 2
            oy = stroke * 2 + i * (line_h + gap)
            for ch, x in zip(text, xs):
                if pas == 0:
                    d.text((ox + x, oy), ch, font=font, fill=INK + (255,),
                           stroke_width=stroke, stroke_fill=INK + (255,))
                else:
                    d.text((ox + x, oy), ch, font=font, fill=CHEESE + (255,))

    # 그림자
    pad = int(em * 0.22)
    canvas = Image.new("RGBA", (width + pad * 2, height + pad * 2), (0, 0, 0, 0))
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 190), (pad, pad + int(em * 0.05)), art.getchannel("A"))
    shadow = shadow.filter(ImageFilter.GaussianBlur(em * 0.045))
    canvas = Image.alpha_composite(canvas, shadow)
    canvas.paste(art, (pad, pad), art)
    return canvas.crop(canvas.getbbox())


WORDMARK = None


def place_title(img: Image.Image, box: tuple[float, float, float, float]) -> Image.Image:
    """box = (x0,y0,x1,y1) 정규화 비율. 그 안에 워드마크를 최대 크기로 넣는다."""
    global WORDMARK
    if WORDMARK is None:
        WORDMARK = render_wordmark()
    w, h = img.size
    bx0, by0, bx1, by1 = box
    bw, bh = int((bx1 - bx0) * w), int((by1 - by0) * h)
    s = min(bw / WORDMARK.width, bh / WORDMARK.height)
    m = WORDMARK.resize((max(1, int(WORDMARK.width * s)),
                         max(1, int(WORDMARK.height * s))), Image.LANCZOS)
    px = int(bx0 * w + (bw - m.width) / 2)
    py = int(by0 * h + (bh - m.height) / 2)
    out = img.convert("RGBA")
    out.alpha_composite(m, (px, py))
    return out.convert("RGB")


# ---------------------------------------------------------------- 크롭

def cover(img: Image.Image, tw: int, th: int, anchor: float = 0.5) -> Image.Image:
    """목표 비율로 cover-crop 후 정확히 tw x th 로 리샘플. anchor = 세로 크롭 기준."""
    w, h = img.size
    s = max(tw / w, th / h)
    nw, nh = int(round(w * s)), int(round(h * s))
    r = img.resize((nw, nh), Image.LANCZOS)
    x = int((nw - tw) / 2)
    y = int((nh - th) * anchor)
    return r.crop((x, y, x + tw, y + th))


def darken_left(img: Image.Image, frac: float = 0.60, strength: float = 0.55) -> Image.Image:
    """워드마크가 앉을 좌측을 그라데이션으로 눌러 글자 대비를 확보한다."""
    w, h = img.size
    ramp = np.clip(1.0 - np.linspace(0, 1, w) / frac, 0, 1) ** 1.4
    veil = (ramp * strength * 255).astype(np.uint8)
    layer = np.zeros((h, w, 4), np.uint8)
    layer[..., 3] = np.tile(veil, (h, 1))
    out = img.convert("RGBA")
    out.alpha_composite(Image.fromarray(layer))
    return out.convert("RGB")


def darken_bottom(img: Image.Image, frac: float = 0.55, strength: float = 0.6) -> Image.Image:
    w, h = img.size
    t = np.linspace(0, 1, h)
    ramp = np.clip((t - (1 - frac)) / frac, 0, 1) ** 1.2
    veil = (ramp * strength * 255).astype(np.uint8)
    layer = np.zeros((h, w, 4), np.uint8)
    layer[..., 3] = np.tile(veil[:, None], (1, w))
    out = img.convert("RGBA")
    out.alpha_composite(Image.fromarray(layer))
    return out.convert("RGB")


# ---------------------------------------------------------------- 빌드

def load(name: str) -> Image.Image | None:
    p = os.path.join(RAW, name + ".png")
    if not os.path.exists(p):
        print(f"  [skip] {name} 원본 없음")
        return None
    return strip_watermark(Image.open(p))


def save(img: Image.Image, name: str, size: tuple[int, int]):
    assert img.size == size, f"{name}: {img.size} != {size}"
    p = os.path.join(OUT, name)
    if name.endswith(".jpg"):
        img.convert("RGB").save(p, quality=95, subsampling=0)
    else:
        img.save(p)
    print(f"  {name:34s} {img.size[0]}x{img.size[1]}")


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(ICON_UNITY, exist_ok=True)

    icon = load("KEY_Icon")
    if icon is not None:
        # 184px 커뮤니티 아이콘에서 쥐가 너무 작아 보인다 → 중앙을 1.28배 당긴다.
        iw, ih = icon.size
        side = int(min(iw, ih) / 1.28)
        icon = icon.crop(((iw - side) // 2, (ih - side) // 2,
                          (iw + side) // 2, (ih + side) // 2))
        sq = cover(icon, 1024, 1024)
        save(sq, "app-icon-1024.png", (1024, 1024))
        sq.save(os.path.join(ICON_UNITY, "AppIcon_1024.png"))
        save(sq.resize((256, 256), Image.LANCZOS), "client-icon-256.png", (256, 256))
        save(sq.resize((184, 184), Image.LANCZOS), "community-icon-184.jpg", (184, 184))

    wide = load("KEY_Wide")
    if wide is not None:
        for name, (tw, th), box in [
            ("header-capsule-920x430.png", (920, 430), (0.028, 0.130, 0.545, 0.870)),
            ("library-header-920x430.png", (920, 430), (0.028, 0.130, 0.545, 0.870)),
            ("main-capsule-1232x706.png", (1232, 706), (0.032, 0.185, 0.535, 0.815)),
            ("small-capsule-462x174.png", (462, 174), (0.030, 0.090, 0.630, 0.910)),
        ]:
            im = darken_left(cover(wide, tw, th, anchor=0.45))
            save(place_title(im, box), name, (tw, th))

    tall = load("KEY_Tall")
    if tall is not None:
        for name, (tw, th), box in [
            ("library-capsule-600x900.png", (600, 900), (0.055, 0.672, 0.945, 0.958)),
            ("vertical-capsule-748x896.png", (748, 896), (0.055, 0.675, 0.945, 0.958)),
        ]:
            im = darken_bottom(cover(tall, tw, th, anchor=0.35))
            save(place_title(im, box), name, (tw, th))

    hero = load("KEY_Hero")
    if hero is not None:
        save(cover(hero, 3840, 1240), "library-hero-3840x1240.png", (3840, 1240))
        save(cover(hero, 1438, 810), "page-background-1438x810.png", (1438, 810))

    # 라이브러리 로고 — 투명 배경, 가로 1280
    global WORDMARK
    if WORDMARK is None:
        WORDMARK = render_wordmark()
    logo = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
    s = min(1280 * 0.96 / WORDMARK.width, 720 * 0.96 / WORDMARK.height)
    m = WORDMARK.resize((int(WORDMARK.width * s), int(WORDMARK.height * s)), Image.LANCZOS)
    logo.alpha_composite(m, ((1280 - m.width) // 2, (720 - m.height) // 2))
    logo.save(os.path.join(OUT, "library-logo-1280x720.png"))
    print(f"  {'library-logo-1280x720.png':34s} 1280x720 (RGBA)")


if __name__ == "__main__":
    sys.exit(main())
