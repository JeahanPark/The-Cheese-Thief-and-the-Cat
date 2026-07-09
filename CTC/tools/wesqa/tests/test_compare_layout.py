import numpy as np
import cv2
from wesqa.vision import compare_layout


def _canvas():
    return np.full((400, 600, 3), 30, np.uint8)


def test_identical_high():
    a = _canvas()
    for y in (40, 120, 200, 280):
        cv2.rectangle(a, (60, y), (540, y + 50), (200, 200, 200), -1)
    b = a.copy()
    assert compare_layout(a, b) >= 0.9


def test_different_low():
    a = _canvas()
    cv2.rectangle(a, (60, 40), (540, 90), (200, 200, 200), -1)
    b = _canvas()
    for x in (60, 180, 300, 420):
        cv2.rectangle(b, (x, 40), (x + 90, 360), (200, 200, 200), -1)
    assert compare_layout(a, b) <= 0.5


def test_empty_both():
    assert compare_layout(_canvas(), _canvas()) == 1.0
