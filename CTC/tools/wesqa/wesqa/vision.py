# coding=utf-8
"""스크린샷 base64 → numpy 이미지, aircv 템플릿 매칭 헬퍼."""
import base64

import cv2
import numpy as np

from aircv.template_matching import TemplateMatching


def b64_to_image(b64):
    if isinstance(b64, (bytes, bytearray)):
        raw = base64.b64decode(b64)
    else:
        raw = base64.b64decode(b64.encode("ascii") if isinstance(b64, str) else b64)
    arr = np.frombuffer(raw, dtype=np.uint8)
    return cv2.imdecode(arr, cv2.IMREAD_COLOR)


def find_template(screen_img, template_img, threshold=0.8):
    # TemplateMatching(im_search, im_source): template=찾을 것=im_search, screen=원본=im_source
    match = TemplateMatching(template_img, screen_img, threshold=threshold)
    return match.find_best_result()


def template_on_screen(screen_img, template_path, threshold=0.8):
    tpl = cv2.imread(template_path, cv2.IMREAD_COLOR)
    if tpl is None:
        raise FileNotFoundError(template_path)
    return find_template(screen_img, tpl, threshold) is not None


def _detect_boxes(gray, min_area_frac=0.002):
    h, w = gray.shape
    min_area = h * w * min_area_frac
    edges = cv2.Canny(gray, 50, 150)
    edges = cv2.dilate(edges, np.ones((3, 3), np.uint8), iterations=2)
    cnts, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    boxes = []
    for c in cnts:
        x, y, bw, bh = cv2.boundingRect(c)
        if bw * bh >= min_area:
            boxes.append((x / w, y / h, bw / w, bh / h))
    return boxes


def _iou(a, b):
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ix = max(0.0, min(ax + aw, bx + bw) - max(ax, bx))
    iy = max(0.0, min(ay + ah, by + bh) - max(ay, by))
    inter = ix * iy
    union = aw * ah + bw * bh - inter
    return inter / union if union > 0 else 0.0


def _text_mask(img, W=640, H=360):
    g = cv2.cvtColor(cv2.resize(img, (W, H)), cv2.COLOR_BGR2GRAY)
    k = max(9, (H // 24) | 1)
    th = cv2.morphologyEx(g, cv2.MORPH_TOPHAT, cv2.getStructuringElement(cv2.MORPH_RECT, (k, k)))
    _, bw = cv2.threshold(th, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    return bw, W, H


def _row_peaks(mask):
    rs = mask.mean(1).astype(np.float32)
    rs = cv2.GaussianBlur(rs.reshape(-1, 1), (1, 5), 0).ravel()
    if rs.max() < 1e-6:
        return 0
    thr = rs.max() * 0.18
    H = len(rs)
    peaks, i = 0, 1
    while i < H - 1:
        if rs[i] > thr and rs[i] >= rs[i - 1] and rs[i] > rs[i + 1]:
            peaks += 1
            i += int(H * 0.02) + 1
        else:
            i += 1
    return peaks


def _split_frac(img, W=640, H=360):
    """사이드바/메인 경계 x를 동적 검출 (좌 8~45% 내 최강 수직 엣지 = 디바이더)."""
    g = cv2.cvtColor(cv2.resize(img, (W, H)), cv2.COLOR_BGR2GRAY).astype(np.float32)
    gx = np.abs(cv2.Sobel(g, cv2.CV_32F, 1, 0))
    col = cv2.GaussianBlur(gx.sum(0).reshape(-1, 1), (1, 5), 0).ravel()
    lo, hi = int(W * 0.08), int(W * 0.45)
    return (lo + int(np.argmax(col[lo:hi]))) / W


def layout_signature(img):
    """렌더/시안 이미지에서 구조 시그니처 추출 (스타일·해상도 무관, 동적 사이드바 분할)."""
    bw, W, H = _text_mask(img)
    sx = int(W * _split_frac(img, W, H))
    left = bw[:, :sx]
    main = bw[:, sx:]
    return {
        "sidebar_items": _row_peaks(left),
        "main_rows": _row_peaks(main),
        "has_sidebar": float(left.mean()) > 0.5,
        "has_main": float(main.mean()) > 0.5,
    }


def structure_match(img_a, img_b, row_tol=0.4):
    """구조 단언 비교: 시안 vs 빌드의 레이아웃 구조 일치 판정 (픽셀유사도 아님).

    반환 dict: score(0~1), passed(score>=0.75), 각 단언 결과, 양쪽 시그니처.
    """
    sa, sb = layout_signature(img_a), layout_signature(img_b)
    hi = max(sa["main_rows"], sb["main_rows"])
    lo = min(sa["main_rows"], sb["main_rows"])
    checks = {
        "sidebar_items": abs(sa["sidebar_items"] - sb["sidebar_items"]) <= 1,
        "main_rows": hi > 0 and (lo / hi) >= (1 - row_tol),
        "has_sidebar": sa["has_sidebar"] == sb["has_sidebar"],
        "has_main": sa["has_main"] == sb["has_main"],
    }
    score = sum(checks.values()) / len(checks)
    return {
        "score": round(score, 3),
        "passed": score >= 0.75,
        "checks": checks,
        "sig_mockup": sa,
        "sig_build": sb,
    }


def compare_layout(img_a, img_b):
    """레이아웃 구조 유사도 0~1. 시안(그림) vs 실제 렌더 비교용 (픽셀 SSIM 아님)."""
    gray_a = cv2.cvtColor(img_a, cv2.COLOR_BGR2GRAY)
    resized_b = cv2.resize(img_b, (img_a.shape[1], img_a.shape[0]))
    gray_b = cv2.cvtColor(resized_b, cv2.COLOR_BGR2GRAY)
    ba, bb = _detect_boxes(gray_a), _detect_boxes(gray_b)
    if not ba and not bb:
        return 1.0
    if not ba or not bb:
        return 0.0
    used, total = set(), 0.0
    for a in ba:
        best, best_j = 0.0, -1
        for j, b in enumerate(bb):
            if j in used:
                continue
            v = _iou(a, b)
            if v > best:
                best, best_j = v, j
        if best_j >= 0:
            used.add(best_j)
        total += best
    iou_avg = total / max(len(ba), len(bb))
    count_ratio = min(len(ba), len(bb)) / max(len(ba), len(bb))
    return round(iou_avg * count_ratio, 4)
