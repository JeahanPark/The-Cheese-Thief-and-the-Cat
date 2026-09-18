"""Stunned - 용사 칼에 맞아 굳은 쥐(반복 13프레임 · 0.5초). 원본은 이 파일이다.

기획: docs/superpowers/specs/2026-09-19-hero-sword-design.md §4·§5-3. 스턴은 0.8초(`StunSeconds`)지만 CSV로 바뀔 수 있어
**짧은 반복**으로 만든다(메타 `loopTime: 1`) — 몇 초가 되든 그동안 돈다. 1프레임과 13프레임이 같은 자세다.

**「굳었다」는 멈춘 그림이 아니라 어지러운 그림이다.** 딱 멈춘 쥐는 렉으로 읽힌다. 그래서 몸은 제자리에 두고
**머리가 원을 그리며 휘청이고**, 몸통이 좌우로 흔들리고, 귀가 뒤로 처진다. 머리 위 별(`FxId.Stunned`)과
어지러운 눈(`Eyes_Spin`)이 같이 돈다.

발은 **움직이지 않는다** — 이동 입력이 잠겨 있는데 발이 걸으면 거짓말이다. 몸통 흔들기(Z, 옆으로 기울기)는
±3도로 묶는다: 옆으로 기울면 한쪽 발이 가라앉는다(쟀다 — 아래 WORST).

부호(dash.py 머리말): body Z = 옆으로 기울기 · head + 코 내림, Z + 오른쪽으로 돌림 · ear + 뒤로 눕힘 · tail + 배 밑으로
"""
from math import radians

NAME = "Stunned"

FRAME_START = 1
FRAME_END = 13

POSES = {
    # 몸통은 좌우로만 흔들린다(X 0 — 앞뒤로 숙이면 발이 뜨거나 가라앉는다).
    "body":     [(1, 0.0, 0.0), (4, 0.0, 3.0), (7, 0.0, 0.0), (10, 0.0, -3.0), (13, 0.0, 0.0)],
    # 머리가 원을 그린다 — 코 내림(X)과 돌림(Z)을 90도 어긋나게.
    "head":     [(1, 10.0, 0.0), (4, 2.0, 18.0), (7, -6.0, 0.0), (10, 2.0, -18.0), (13, 10.0, 0.0)],
    # 귀는 뒤로 처져 흐늘거린다(한 박 늦게).
    "ear.L":    [(1, 34.0), (3, 42.0), (6, 30.0), (9, 44.0), (12, 32.0), (13, 34.0)],
    "ear.R":    [(1, 38.0), (4, 30.0), (7, 44.0), (10, 30.0), (13, 38.0)],
    # 꼬리는 힘이 빠져 살짝 처진다(+는 배 밑 — 메시가 뼈보다 19mm 굵어 조금만).
    "tail.01":  [(1, 4.0), (7, 6.0), (13, 4.0)],
    "tail.02":  [(1, 6.0), (4, 10.0), (10, 2.0), (13, 6.0)],
}


def build(_armature_object):
    """키는 (프레임, X[, Z])다 — X를 먼저 돌린 뒤 Z로 돌린다(Qz · Qx)."""
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
            bone.rotation_quaternion = (Quaternion((0.0, 0.0, 1.0), radians(z_deg))
                                        @ Quaternion((1.0, 0.0, 0.0), radians(x_deg)))
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame)
