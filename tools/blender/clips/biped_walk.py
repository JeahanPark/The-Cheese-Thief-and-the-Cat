"""BipedWalk - 깡패 쥐의 두 발 걷기(루프). 이 파일이 이 클립의 원본이다.

**왜 있나**: 2페이즈에서 깡패 편 쥐는 뒷다리로 서서 걷는다(2026-09-14 사용자 요청 — 설계
docs/superpowers/specs/2026-09-14-gang-mouse-look-design.md). 오른 앞발에는 몽둥이가 들려 있다.
**제자리 걷기다** — 루트는 안 건드린다. 몸은 CharacterController가 옮기고 클립은 다리만 굴린다.
보폭이 작은 대신 **몸을 좌우로 흔들어(Z) 뒤뚱거림**을 준다 — "어깨에 힘주고 걷는 깡패"의 인상.

────────────────────────────────────────────────────────────────────────────
부호(dash.py·fortress.py 머리말과 같다):
  body - 뒤로 젖힘 · head + 코 내림 · leg.F + 앞발 앞·위로 · leg.B + 뒷발 앞으로 · shin.B - 무릎 굽힘
  tail - 위·뒤로 · (X, Z) 키는 X를 먼저 돌리고 Z로 벌린다. 오른발은 Z 부호가 반대(바깥이 -)
────────────────────────────────────────────────────────────────────────────

**뒷다리 값은 풀었다**(2026-09-14, 발 메시 최저점 기준 — scratchpad bipedsolve.py). 발의 앞뒤 위치 y는 **-가 앞**이다.
첫 시안은 body -32였는데 렌더에서 **"쪼그려 앉은 쥐"**로 읽혔다. -55는 두 발로 선 그림이 됐지만
**엉덩이 메시가 바닥을 -23mm 뚫었다**(발을 딛게 두면 엉덩이가 내려앉는다 — 꼬리를 아무리 들어도 body가 남는다).
-48이 두 조건의 타협점이다: 선 그림이 읽히고 엉덩이는 -17mm(팩 Walk 기준선 -9.1보다 깊다 — 알고 둔다).

    body -48  딛기(z 1.5mm)                 들기(z 30mm)                   높이 들기(z 50mm)
              thigh 20 → shin  -8.1 y -84   thigh 30 → shin  -8.2 y -99   thigh 35 → shin  -4.9 y -107
              thigh 25 → shin -20.2 y -78   thigh 35 → shin -19.5 y -94   thigh 40 → shin -15.7 y -103
              thigh 30 → shin -33.9 y -68   thigh 40 → shin -31.6 y -86   thigh 45 → shin -26.9 y  -97
              thigh 35 → shin -51.4 y -51   thigh 45 → shin -44.8 y -76   thigh 50 → shin -38.7 y  -89
    body -54  thigh 25 → shin -28.9 (들어 올리기)      body -40  thigh 25 → shin -10.9 (내려치기 쏠림)

딛는 구간 thigh 20(앞) → 35(뒤) — 보폭 약 33 mm. tail.01은 -100이면 꼬리가 바닥에서 떨어진다(-80은 -17mm 잠김).
"""
from math import radians

NAME = "BipedWalk"

FRAME_START = 1
FRAME_END = 17   # 16프레임 한 바퀴(0.67초) — 17은 1과 같은 포즈(루프 이음매)

BODY = -48.0

# 왼발: 앞에서 딛고(1) → 뒤로 밀고(7) → 들어서(9) → 앞으로(13) → 내려와(15) → 딛기(17=1)
_L_THIGH = [(1, 20.0), (3, 25.0), (5, 30.0), (7, 35.0), (9, 45.0), (11, 40.0), (13, 35.0), (15, 28.0), (17, 20.0)]
_L_SHIN = [(1, -8.1), (3, -20.2), (5, -33.9), (7, -51.4), (9, -44.8), (11, -31.6), (13, -19.5), (15, -8.0), (17, -8.1)]


def _shift(_keys, _half, _end):
    """반 바퀴 늦춘 오른발 — 1..end-1을 돌려 붙이고 end에 1을 복사한다."""
    out = {}
    period = _end - 1
    for f, v in _keys:
        if f == _end:
            continue
        out[(f - 1 + _half) % period + 1] = v
    keys = sorted(out.items())
    keys.append((_end, out[1]))
    return keys


POSES = {
    # 딛는 순간(1·9) 한 번씩 앞으로(+2) 실린다. Z는 딛는 발 쪽으로 — 뒤뚱거림의 주인.
    "body":     [(1, BODY + 2.0, 6.0), (5, BODY, 2.0), (9, BODY + 2.0, -6.0), (13, BODY, -2.0), (17, BODY + 2.0, 6.0)],
    "leg.B.L":  _L_THIGH,
    "shin.B.L": _L_SHIN,
    "leg.B.R":  _shift(_L_THIGH, 8, 17),
    "shin.B.R": _shift(_L_SHIN, 8, 17),
    # 머리: 젖힌 몸만큼 코를 내려 앞을 본다(+40). 몸의 Z를 반쯤 되받는다.
    "head":     [(1, 36.0, -4.0), (5, 32.0, -1.0), (9, 36.0, 4.0), (13, 32.0, 1.0), (17, 36.0, -4.0)],
    # 왼 앞발(빈손): 오른 뒷발 박자로 앞뒤로 흔든다.
    "leg.F.L":  [(1, 10.0, 16.0), (9, 45.0, 10.0), (17, 10.0, 16.0)],
    # 오른 앞발(몽둥이): 들어 어깨 앞에 받친다. 흔들림은 작게.
    "leg.F.R":  [(1, 40.0, -28.0), (5, 44.0, -30.0), (9, 40.0, -28.0), (13, 44.0, -30.0), (17, 40.0, -28.0)],
    "tail.01":  [(1, -100.0, -10.0), (9, -100.0, 10.0), (17, -100.0, -10.0)],
    "tail.02":  [(1, -20.0, 8.0), (5, -24.0, -6.0), (9, -20.0, -8.0), (13, -24.0, 6.0), (17, -20.0, 8.0)],
    "ear.L":    [(1, -6.0), (5, 4.0), (9, -6.0), (13, 4.0), (17, -6.0)],
    "ear.R":    [(1, -6.0), (5, 4.0), (9, -6.0), (13, 4.0), (17, -6.0)],
}


def build(_armature_object):
    """fortress.py와 같다 — `(프레임, X)` 또는 `(프레임, X, Z)` · Qz @ Qx."""
    import bpy
    from mathutils import Quaternion

    for bone_name in POSES:
        if _armature_object.pose.bones.get(bone_name) is None:
            raise SystemExit(f"포즈 본 '{bone_name}'이 없다 - 리그가 바뀌었다")
    for bone_name, frames in POSES.items():
        bone = _armature_object.pose.bones[bone_name]
        for key in frames:
            frame, x_deg = key[0], key[1]
            z_deg = key[2] if len(key) > 2 else 0.0
            bpy.context.scene.frame_set(frame)
            bone.rotation_quaternion = Quaternion((0.0, 0.0, 1.0), radians(z_deg)) @ Quaternion((1.0, 0.0, 0.0), radians(x_deg))
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame)
