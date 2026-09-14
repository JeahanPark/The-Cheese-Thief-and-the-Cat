"""ClubSwing - 깡패 쥐가 몽둥이로 내려친다(원샷). 원본은 이 파일이다.

"들었다가 → 한 박 멈추고 → 몸째 쾅" 으로 읽혀야 한다. 판정 프레임은 7(몸이 앞으로 쏠린 순간).
**팔을 머리 위로 올리지 않는다**(fortress.py 경위 — 앞·위 방향은 볼·턱에 먹힌다).
들어 올릴 때는 **바깥으로 벌려(Z)** 몸 옆에서 보이게 하고, 내려칠 때 벌림을 접으며 앞으로 떨어뜨린다.

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

NAME = "ClubSwing"

FRAME_START = 1
FRAME_END = 12

POSES = {
    # 선 자세(-48) → 젖혀 모은다(-54, 4~5) → 몸째 앞으로(-40, 7) → 여운 → 복귀
    "body":     [(1, -48.0), (4, -54.0, 5.0), (5, -54.0, 5.0), (7, -40.0, -7.0), (9, -42.0, -3.0), (12, -48.0)],
    "leg.B.L":  [(1, 25.0), (12, 25.0)],
    "leg.B.R":  [(1, 25.0), (12, 25.0)],
    "shin.B.L": [(1, -20.2), (4, -28.9), (5, -28.9), (7, -10.9), (9, -13.0), (12, -20.2)],
    "shin.B.R": [(1, -20.2), (4, -28.9), (5, -28.9), (7, -10.9), (9, -13.0), (12, -20.2)],
    "leg.F.R":  [(1, 44.0, -30.0), (4, 60.0, -80.0), (5, 62.0, -84.0), (6, 30.0, -45.0), (7, -5.0, -10.0),
                 (9, 0.0, -12.0), (12, 44.0, -30.0)],
    "leg.F.L":  [(1, 12.0, 20.0), (4, 24.0, 34.0), (7, 0.0, 12.0), (12, 12.0, 20.0)],
    "head":     [(1, 28.0), (4, 34.0, 6.0), (5, 34.0, 6.0), (7, 30.0, -5.0), (9, 26.0), (12, 28.0)],
    "tail.01":  [(1, -100.0), (5, -108.0, 12.0), (7, -94.0, -14.0), (10, -100.0, 4.0), (12, -100.0)],
    "tail.02":  [(1, -20.0), (6, -24.0, 10.0), (8, -14.0, -16.0), (11, -20.0, 2.0), (12, -20.0)],
    "ear.L":    [(1, 0.0), (5, -14.0), (7, 20.0), (9, 26.0), (12, 0.0)],
    "ear.R":    [(1, 0.0), (5, -14.0), (7, 20.0), (9, 26.0), (12, 0.0)],
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
