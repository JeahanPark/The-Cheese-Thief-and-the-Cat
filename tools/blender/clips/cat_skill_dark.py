"""CatSkillDark - 고양이 전직 「어둠」 스킬(Q). 이 파일이 이 클립의 원본이다. (리그: art/blender/cat.blend)

어둠은 **쥐들의 화면을 어둡게 한다** — 몸을 낮게 가라앉히고 머리를 파묻고, 귀를 눕히고 꼬리를 배 밑으로
말아 넣는다(「그림자 속으로 웅크린다」). 느리게 가라앉아(2~5) 버티고(5~8) 천천히 편다.

**첫 시안(앞발로 얼굴 가리기)은 렌더에서 죽었다**(2026-09-13). 고양이 머리가 커서 앞발이 얼굴까지 안 닿고
**앞으로 뻗은 발**로만 보였다 — 빛(앞발 하나 들고 감지)과 같은 실루엣이 됐다.
광분과도 갈린다: 광분은 낮아지되 **꼬리가 곧추서고 고개를 치켜든다**, 어둠은 **꼬리가 말려 들어가고 고개가 묻힌다.**

**꼬리 말기(+)는 여기서만 쓴다** — 고양이 꼬리 메시는 쉬는 자세에서 바닥 위 197mm라 말아도 안 닿는다(measure_clip으로 확인).
부호·풀이 표는 cat_frenzy.py 머리말(고양이 리그 실측, **귀는 -가 뒤로**).
"""
from math import cos, radians, sin

NAME = "CatSkillDark"

FRAME_START = 1
FRAME_END = 11

POSES = {
    "body":     [(1, 0.0), (3, 6.0), (5, 10.0), (8, 10.0), (10, 3.0), (11, 0.0)],
    "leg.F.L":  [(1, 0.0), (3, 21.8), (5, 31.7), (8, 31.7), (10, 12.0), (11, 0.0)],
    "leg.F.R":  [(1, 0.0), (3, 21.8), (5, 31.7), (8, 31.7), (10, 12.0), (11, 0.0)],
    "head":     [(1, 0.0), (3, 14.0), (5, 26.0), (8, 28.0), (10, 8.0), (11, 0.0)],
    "ear.L":    [(1, 0.0), (3, -25.0), (5, -45.0), (8, -48.0), (10, -12.0), (11, 0.0)],
    "ear.R":    [(1, 0.0), (3, -25.0), (5, -45.0), (8, -48.0), (10, -12.0), (11, 0.0)],
    "tail.01":  [(1, 0.0), (3, 10.0), (6, 24.0), (8, 26.0), (10, 8.0), (11, 0.0)],
    "tail.02":  [(1, 0.0), (4, 14.0), (7, 32.0), (9, 30.0), (11, 0.0)],
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
