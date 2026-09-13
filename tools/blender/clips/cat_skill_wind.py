"""CatSkillWind - 고양이 전직 「바람」 스킬(Q). 이 파일이 이 클립의 원본이다. (리그: art/blender/cat.blend)

바람은 **이속**이다 — 몸을 앞뒤로 쭉 늘여 튀어나가는 발진 자세. 앞다리는 멀리 앞으로, 뒷다리는 뒤로 차고,
귀·꼬리가 뒤로 눕는다(쥐 Dash와 같은 문법 — 「빠르다」는 실루엣이 길어지는 것으로 읽힌다).
부호·풀이 표는 cat_frenzy.py 머리말(고양이 리그 실측, **귀는 -가 뒤로**).
"""
from math import cos, radians, sin

NAME = "CatSkillWind"

FRAME_START = 1
FRAME_END = 11

POSES = {
    "body":     [(1, 0.0), (3, 6.0), (5, -4.0), (6, -4.0), (8, 2.0), (11, 0.0)],
    "leg.F.L":  [(1, 0.0), (3, 21.8), (5, 48.0), (6, 50.0), (8, 12.0), (10, 4.0), (11, 0.0)],
    "leg.F.R":  [(1, 0.0), (3, 21.8), (5, 48.0), (6, 50.0), (8, 12.0), (10, 4.0), (11, 0.0)],
    "leg.B.L":  [(1, 0.0), (3, 0.0), (5, -36.0), (6, -38.0), (8, -8.0), (11, 0.0)],
    "leg.B.R":  [(1, 0.0), (3, 0.0), (5, -36.0), (6, -38.0), (8, -8.0), (11, 0.0)],
    "head":     [(1, 0.0), (3, 6.0), (5, -6.0), (6, -8.0), (8, 0.0), (11, 0.0)],
    "ear.L":    [(1, 0.0), (4, -20.0), (6, -45.0), (8, -35.0), (10, -10.0), (11, 0.0)],
    "ear.R":    [(1, 0.0), (4, -20.0), (6, -45.0), (8, -35.0), (10, -10.0), (11, 0.0)],
    "tail.01":  [(1, 0.0), (4, -20.0), (6, -40.0), (8, -35.0), (10, -10.0), (11, 0.0)],
    "tail.02":  [(1, 0.0), (5, -15.0), (7, -30.0), (9, -20.0), (11, 0.0)],
}


def build(_armature_object):
    """dash.py와 같다 — rotation_mode·보간을 건드리지 않는다."""
    import bpy

    for bone_name in POSES:
        if _armature_object.pose.bones.get(bone_name) is None:
            raise SystemExit(f"포즈 본 '{bone_name}'이 없다 - 리그가 바뀌었다")

    for bone_name, frames in POSES.items():
        bone = _armature_object.pose.bones[bone_name]
        for frame, degrees in frames:
            bpy.context.scene.frame_set(frame)
            half = radians(degrees) * 0.5
            bone.rotation_quaternion = (cos(half), sin(half), 0.0, 0.0)  # (w, x, y, z)
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame)
