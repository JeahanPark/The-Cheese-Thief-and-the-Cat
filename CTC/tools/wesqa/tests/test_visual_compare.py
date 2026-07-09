import os

import numpy as np
import cv2

from wesqa.visual_compare import geom_compare, visual_compare, visual_gate


def _ui(fill=0.6, dark=30):
    """합성 화면: 중앙에 면적 fill 비율의 밝은 콘텐츠 박스."""
    W, H = 960, 540
    img = np.full((H, W, 3), dark, np.uint8)
    bw, bh = int(W * fill ** 0.5), int(H * fill ** 0.5)
    x0, y0 = (W - bw) // 2, (H - bh) // 2
    cv2.rectangle(img, (x0, y0), (x0 + bw, y0 + bh), (210, 200, 160), -1)
    return img


def test_geom_same_fill_passes():
    a = _ui(fill=0.6)
    b = _ui(fill=0.6)
    r = geom_compare(a, b)
    assert r["passed"], r          # 같은 레이아웃 → threshold 0.50 통과
    assert r["fill"] >= 0.99       # 채움비율 주축은 동일 시 1.0


def test_geom_different_fill_fails():
    a = _ui(fill=0.7)   # 큰 충실 레이아웃
    b = _ui(fill=0.1)   # 작은 섬
    r = geom_compare(a, b)
    assert not r["passed"], r
    # 같음 > 다름: 게이트가 실제로 변별하는지
    assert geom_compare(a, _ui(fill=0.7))["score"] > r["score"]


def test_geom_deterministic():
    a, b = _ui(fill=0.55), _ui(fill=0.45)
    assert geom_compare(a, b)["score"] == geom_compare(a, b)["score"]


def test_visual_compare_returns_submetrics():
    r = visual_compare(_ui(0.6), _ui(0.6))
    for k in ("score", "fill", "color", "edge", "corr"):
        assert k in r


def test_visual_gate_creates_baseline_then_compares(tmp_path):
    ref = os.path.join(str(tmp_path), "golden", "ingame.png")
    # 1회차: 골든 없음 → baseline 생성 + PASS
    first = visual_gate(_ui(0.6), ref)
    assert first["baseline"] and first["passed"]
    assert os.path.exists(ref)
    # 2회차: 동일 화면 → 회귀 PASS
    second = visual_gate(_ui(0.6), ref)
    assert not second["baseline"] and second["passed"]
    # 3회차: 다른 화면 → 회귀 FAIL
    third = visual_gate(_ui(0.05), ref)
    assert not third["passed"], third
