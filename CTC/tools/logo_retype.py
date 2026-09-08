from PIL import Image, ImageFilter
import numpy as np

SRC = "docs/store-assets/raw/T_GameLogo_original.png"   # 원본 — 결과물을 다시 넣고 돌리면 간격이 두 번 적용된다
OUT = "C:/tmp/logo_final.png"

OUTLINE_R = 14
OUTLINE_RGB = (10, 15, 22)
SHADOW_BLUR, SHADOW_DY, SHADOW_PEAK = 9, 10, 0.75
LINE_GAP = 40                      # 두 줄 외곽선 사이 세로 간격 (원래 126)

im = Image.open(SRC).convert("RGBA")
a = np.array(im).astype(int)
H, W = a.shape[:2]
r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
yellow = (al > 128) & (r > 200) & (g > 150) & (b < 140)

def dilate(mask, R):
    h, w = mask.shape
    out = np.zeros_like(mask)
    for dy in range(-R, R + 1):
        span = int((R * R - dy * dy) ** 0.5)
        ys0, ys1 = max(0, dy), min(h, h + dy)
        yd0, yd1 = max(0, -dy), min(h, h - dy)
        row = mask[ys0:ys1]
        for dx in range(-span, span + 1):
            xs0, xs1 = max(0, dx), min(w, w + dx)
            xd0, xd1 = max(0, -dx), min(w, w - dx)
            out[yd0:yd1, xd0:xd1] |= row[:, xs0:xs1]
    return out

def col_runs(mask, y0, y1, minw=4):
    sub = mask[y0:y1 + 1]; ch = sub.any(axis=0); out = []; s = None
    for x, v in enumerate(ch):
        if v and s is None: s = x
        if not v and s is not None: out.append((s, x - 1)); s = None
    if s is not None: out.append((s, len(ch) - 1))
    return [t for t in out if t[1] - t[0] + 1 >= minw]

TOP = (114, 330)     # CANADIAN 줄
BOT = (395, 620)     # MOUSE 줄
solid = al > 200

topY = np.where(solid[TOP[0]:TOP[1]].any(axis=1))[0]; top_solid_bottom = TOP[0] + topY.max()
topX = np.where(solid[TOP[0]:TOP[1]].any(axis=0))[0]
top_yellow_cols = np.where(yellow[TOP[0]:TOP[1]].any(axis=0))[0]
TGT_X0, TGT_X1 = int(top_yellow_cols.min()), int(top_yellow_cols.max())

letters = col_runs(yellow, *BOT)
botY = np.where(solid[BOT[0]:BOT[1]].any(axis=1))[0]; bot_solid_top = BOT[0] + botY.min()
print("CANADIAN 노랑 x %d..%d  솔리드 아래 y=%d" % (TGT_X0, TGT_X1, top_solid_bottom))
print("MOUSE 글자 %d개 %s  솔리드 위 y=%d" % (len(letters), letters, bot_solid_top))
assert len(letters) == 5

widths = [c1 - c0 + 1 for c0, c1 in letters]
gap = (TGT_X1 - TGT_X0 + 1 - sum(widths)) / (len(letters) - 1)
dy = (top_solid_bottom + LINE_GAP) - bot_solid_top
print("자간 %.1fpx  세로 이동 %+dpx" % (gap, dy))

# ── MOUSE 재조판: 노랑 픽셀은 원본 그대로 옮긴다(하이라이트 보존) ──
newY = np.zeros((H, W), bool)
newC = np.zeros((H, W, 3), int)
x = float(TGT_X0)
for (c0, c1), w in zip(letters, widths):
    nx = int(round(x))
    src_m = yellow[BOT[0]:BOT[1] + 1, c0:c1 + 1]
    src_c = a[BOT[0]:BOT[1] + 1, c0:c1 + 1, :3]
    y0 = BOT[0] + dy
    ys, xs = np.where(src_m)
    newY[y0 + ys, nx + xs] = True
    newC[y0 + ys, nx + xs] = src_c[ys, xs]
    x += w + gap

body = dilate(newY, OUTLINE_R)
outline = body & ~newY
sh = np.array(Image.fromarray((body * 255).astype(np.uint8))
              .filter(ImageFilter.GaussianBlur(SHADOW_BLUR))).astype(float) / 255.0
sh = np.roll(sh, SHADOW_DY, axis=0) * SHADOW_PEAK

# ── 합성: CANADIAN은 원본 픽셀 그대로, 그 아래는 새로 그린다 ──
CUT = 340
out = a.copy()
out[CUT:] = 0
la = np.zeros((H, W), float); lc = np.zeros((H, W, 3), float)
la[:] = sh; lc[:] = 0.0
la = np.where(outline, 1.0, la); lc[outline] = OUTLINE_RGB
la = np.where(newY, 1.0, la);    lc[newY] = newC[newY]

dst_a = out[..., 3] / 255.0
res_a = la + dst_a * (1 - la)
safe = np.where(res_a > 0, res_a, 1)
res_c = (lc * la[..., None] + out[..., :3] * (dst_a * (1 - la))[..., None]) / safe[..., None]
res = np.zeros_like(out)
res[..., :3] = np.clip(res_c, 0, 255); res[..., 3] = np.clip(res_a * 255, 0, 255)

# ── 세로 중앙 정렬 ──
rows = np.where(res[..., 3].max(axis=1) > 128)[0]   # 그림자 꼬리 말고 실제 글자 기준
shift = int(round((H - 1) / 2 - (rows.min() + rows.max()) / 2))
res = np.roll(res, shift, axis=0)
print("블록 y %d..%d → 중앙 정렬 %+dpx" % (rows.min(), rows.max(), shift))

Image.fromarray(res.astype(np.uint8)).save(OUT)
print("saved", OUT)
