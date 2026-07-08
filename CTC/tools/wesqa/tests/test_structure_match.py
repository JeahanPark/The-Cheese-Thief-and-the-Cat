import numpy as np
import cv2
from wesqa.vision import structure_match, layout_signature


def _ui(sidebar_n=3, rows=9, scale=1.0, dark=30):
    """합성 UI: 좌 사이드바 + 우 메인 리스트."""
    W, H = int(960 * scale), int(540 * scale)
    img = np.full((H, W, 3), dark, np.uint8)
    sx = int(W * 0.05)
    for i in range(sidebar_n):
        y = int(H * 0.15) + i * int(H * 0.18)
        cv2.putText(img, "NAV", (sx, y), cv2.FONT_HERSHEY_SIMPLEX, 0.7 * scale, (210, 200, 160), max(1, int(2 * scale)))
    mx = int(W * 0.36)
    for i in range(rows):
        y = int(H * 0.12) + i * int(H * 0.09)
        cv2.putText(img, "Item Row Text", (mx, y), cv2.FONT_HERSHEY_SIMPLEX, 0.6 * scale, (200, 190, 150), max(1, int(2 * scale)))
    return img


def test_same_structure_passes():
    a = _ui(sidebar_n=3, rows=9)
    b = _ui(sidebar_n=3, rows=9, scale=2.0)  # different resolution, same structure
    r = structure_match(a, b)
    assert r["passed"], r


def test_missing_sidebar_fails():
    a = _ui(sidebar_n=3, rows=9)
    b = np.full((540, 960, 3), 30, np.uint8)  # blank
    r = structure_match(a, b)
    assert not r["passed"], r


def test_signature_detects_sidebar_items():
    sig = layout_signature(_ui(sidebar_n=3, rows=8))
    assert sig["sidebar_items"] >= 2
    assert sig["has_main"]
