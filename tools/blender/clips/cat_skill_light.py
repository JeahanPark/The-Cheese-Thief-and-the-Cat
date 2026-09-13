"""CatSkillLight - 고양이 전직 「빛」 스킬(Q). 이 파일이 이 클립의 원본이다. (리그: art/blender/cat.blend)

빛은 **벽 너머의 쥐를 본다** — 사냥감을 감지한 포인터 자세. 앞발 하나를 들고 멈춰, 고개를 치켜들고
귀를 세우고 꼬리를 곧게 편다. 4~8프레임을 **거의 안 움직여** 「멈춰서 살핀다」가 읽히게 한다.
부호·풀이 표는 cat_frenzy.py 머리말(고양이 리그 실측).
"""
from math import cos, radians, sin

NAME = "CatSkillLight"

FRAME_START = 1
FRAME_END = 11

POSES = {
    # 꼿꼿이 선다(-12). 2를 따로 찍는다 — 1→3을 한 번에 보간하면 2에서 뒷발이 -8.8mm 잠겼다.
    # 나올 때도 같다 — 9를 찍지 않으면 9.5에서 -7.8mm였다.
    # 뒷발은 앞쪽 가지 풀이값(-10 → 20.7, -16 → 23.4 사이).
    "body":     [(1, 0.0), (2, -6.0), (3, -12.0), (8, -12.0), (9, -8.0), (10, -3.0), (11, 0.0)],
    # 든 앞발은 **높게**(+75) — 어둠·바람과 갈리는 이 동작의 표식이다. 딛는 발은 젖힘 때문에 살짝 뜬다.
    "leg.F.L":  [(1, 0.0), (3, 62.0), (4, 75.0), (8, 72.0), (10, 15.0), (11, 0.0)],
    "leg.F.R":  [(1, 0.0), (3, 0.0), (8, 0.0), (11, 0.0)],
    "leg.B.L":  [(1, 0.0), (2, 16.7), (3, 21.8), (8, 21.8), (9, 19.0), (10, 11.0), (11, 0.0)],
    "leg.B.R":  [(1, 0.0), (2, 16.7), (3, 21.8), (8, 21.8), (9, 19.0), (10, 11.0), (11, 0.0)],
    "head":     [(1, 0.0), (3, -14.0), (4, -20.0), (8, -18.0), (10, -4.0), (11, 0.0)],
    "ear.L":    [(1, 0.0), (3, 10.0), (8, 12.0), (10, 2.0), (11, 0.0)],
    "ear.R":    [(1, 0.0), (3, 10.0), (8, 12.0), (10, 2.0), (11, 0.0)],
    # 꼬리는 곧게 **위로** 편다(-) — 경계. 어둠의 말린 꼬리와 정반대.
    "tail.01":  [(1, 0.0), (3, -38.0), (5, -48.0), (8, -46.0), (10, -12.0), (11, 0.0)],
    "tail.02":  [(1, 0.0), (4, -18.0), (6, -28.0), (9, -18.0), (11, 0.0)],
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
