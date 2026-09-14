"""BipedRun - 깡패 쥐의 두 발 달리기(루프). 원본은 이 파일이다.

biped_walk.py와 같은 짜임에 **발을 크게 차고 빠르게** 돈다. 10프레임 한 바퀴(0.42초).
몸을 조금 더 세우고(-36) 빈 앞발을 크게 흔든다 — 뛰는 인상은 팔이 만든다.

────────────────────────────────────────────────────────────────────────────
부호(dash.py·fortress.py 머리말과 같다):
  body - 뒤로 젖힘 · head + 코 내림 · leg.F + 앞발 앞·위로(- 뒤로) · leg.B + 뒷발 앞으로 · shin.B - 무릎 굽힘
  tail - 위·뒤로 · (X, Z) 키는 X를 먼저 돌리고 Z로 벌린다. 오른발은 Z 부호가 반대(바깥이 -)
────────────────────────────────────────────────────────────────────────────
**2차 자세(2026-09-14)** — `_biped.py` 머리말. 뒷다리 허벅지 각 = 몸 각(다리가 세로로 선다) + 걸음 흔들기,
몸 높이는 root 들어올림으로 **딛은 발에 맞춰 푼다**(손으로 적지 않는다). 1차(몸 -48 · 허벅지 +25)는 다리가 배 앞으로 뻗어
「앉은 쥐」로 보였고 엉덩이가 바닥을 -17mm 뚫었다(사용자: 다리 위치 자세가 이상해).
"""
NAME = "BipedRun"

FRAME_START = 1
FRAME_END = 11

BODY = -36.0

_L_SWING = [(1, 24.0), (2, 14.0), (4, -4.0), (6, -24.0), (8, -4.0), (10, 18.0), (11, 24.0)]
_L_SHIN = [(1, 0.0), (2, 0.0), (4, 0.0), (6, -10.0), (8, -55.0), (10, -20.0), (11, 0.0)]


def _shift(_keys, _half, _end):
    """반 바퀴 늦춘 오른발 — 1..end-1을 돌려 붙이고 end에 1을 복사한다."""
    out = {}
    period = _end - 1
    for key in _keys:
        if key[0] == _end:
            continue
        out[(key[0] - 1 + _half) % period + 1] = key[1:]
    keys = [(f,) + v for f, v in sorted(out.items())]
    keys.append((_end,) + out[1])
    return keys


def _legs(_body, _offsets):
    """허벅지 = 몸 각 + 흔들기 — 다리가 세로에서 앞뒤로만 흔들린다."""
    return [(f, _body + o) for f, o in _offsets]


POSES = {
    "body":     [(1, BODY, 7.0), (3, BODY, 2.0), (6, BODY, -7.0), (8, BODY, -2.0), (11, BODY, 7.0)],
    "leg.B.L":  _legs(BODY, _L_SWING),
    "shin.B.L": _L_SHIN,
    "leg.B.R":  _legs(BODY, _shift(_L_SWING, 5, 11)),
    "shin.B.R": _shift(_L_SHIN, 5, 11),
    "head":     [(1, 28.0, -4.0), (6, 28.0, 4.0), (11, 28.0, -4.0)],
    # 왼 앞발(빈손): 크게 흔든다 — 30을 넘기면 눈 원판을 뚫는다(2026-09-14 렌더).
    "leg.F.L":  [(1, -10.0, 20.0), (6, 30.0, 16.0), (11, -10.0, 20.0)],
    # 오른 앞발(몽둥이): 허리 옆.
    "leg.F.R":  [(1, 6.0, -45.0), (6, 2.0, -41.0), (11, 6.0, -45.0)],
    "tail.01":  [(1, -60.0, -14.0), (6, -60.0, 14.0), (11, -60.0, -14.0)],
    "tail.02":  [(1, -14.0, 10.0), (3, -16.0, -8.0), (6, -14.0, -10.0), (8, -16.0, 8.0), (11, -14.0, 10.0)],
    "ear.L":    [(1, 14.0), (6, 26.0), (11, 14.0)],
    "ear.R":    [(1, 14.0), (6, 26.0), (11, 14.0)],
}


def build(_armature_object):
    """회전 키 → 키 프레임마다 몸 높이(root)를 딛은 발에 맞춰 푼다(_biped.py)."""
    import os
    import sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import _biped
    _biped.key_rotations(_armature_object, POSES)
    _biped.key_root_lift(_armature_object, _biped.all_key_frames(POSES))
