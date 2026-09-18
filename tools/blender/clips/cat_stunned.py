"""Stunned - 용사 칼에 맞아 굳은 고양이(반복 13프레임 · 0.5초). 원본은 이 파일이다. (리그: art/blender/cat.blend)

쥐의 `Stunned`(stunned.py)와 **같은 이름·같은 박자**다 — 스턴은 진영을 가리지 않는 공통 상태라
두 컨트롤러가 같은 이름의 상태를 갖는다(`AnimationStateLogic.CustomShared`). 칼의 주 표적이 고양이다.

머리가 원을 그리며 휘청이고, 몸통이 좌우로 흔들리고, 귀가 **뒤로 눕는다**. 발은 움직이지 않는다
(이동이 잠겨 있는데 발이 걸으면 거짓말이다). 고양이는 몸이 넓어(앞발 ±143mm) 옆으로 기울면 발이 쥐보다 깊이
가라앉으므로 몸통 흔들기를 ±2도로 묶는다.

부호(cat_frenzy.py 머리말): body Z = 옆으로 기울기 · head + 코 내림, Z + 오른쪽으로 돌림 ·
**ear - 뒤로 눕힘**(쥐와 반대) · tail + 배 밑으로
"""
from math import radians

NAME = "Stunned"

FRAME_START = 1
FRAME_END = 13

POSES = {
    "body":     [(1, 0.0, 0.0), (4, 0.0, 2.0), (7, 0.0, 0.0), (10, 0.0, -2.0), (13, 0.0, 0.0)],
    "head":     [(1, 10.0, 0.0), (4, 2.0, 18.0), (7, -6.0, 0.0), (10, 2.0, -18.0), (13, 10.0, 0.0)],
    "ear.L":    [(1, -34.0), (3, -42.0), (6, -30.0), (9, -44.0), (12, -32.0), (13, -34.0)],
    "ear.R":    [(1, -38.0), (4, -30.0), (7, -44.0), (10, -30.0), (13, -38.0)],
    "tail.01":  [(1, 6.0), (7, 10.0), (13, 6.0)],
    "tail.02":  [(1, 8.0), (4, 14.0), (10, 4.0), (13, 8.0)],
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
