"""Shoot - 쥐 사격. 이 파일이 이 클립의 원본이다.

네발 짐승이라 팔이 없다. 쏘려면 뒷다리로 서고 앞다리를 손처럼 쓴다.
root와 뒷다리는 건드리지 않는다 - 발이 미끄러져 보인다.
1프레임과 11프레임이 같은 포즈여야 원샷이 끝나고 이동 상태로 돌아갈 때 안 튄다.

각도는 설계 문서(2026-09-12-mouse-shoot-clip-design.md) 3절의 출발값이다.
미리보기 렌더를 보며 굴린다.
"""
from math import radians

NAME = "Shoot"

# 블렌더는 1부터 센다. 팩 액션 19개가 전부 프레임 1~11이고, 유니티 메타는 그걸
# firstFrame 0 / lastFrame 10으로 받는다 - 1프레임 어긋나면 클립이 밀리거나 잘린다.
FRAME_START = 1
FRAME_END = 11

# 뼈마다 (프레임, X각도). 전부 뼈 로컬 X축 회전이다.
# 음수가 「뒤로 젖힘」(일어섬), 양수가 「앞으로 숙임」.
POSES = {
    "body":     [(1, 0.0), (3, -25.0), (5, -35.0), (7, -20.0), (11, 0.0)],
    "leg.F.L":  [(1, 0.0), (3, -45.0), (5, -70.0), (7, -25.0), (11, 0.0)],
    "leg.F.R":  [(1, 0.0), (3, -45.0), (5, -70.0), (7, -25.0), (11, 0.0)],
    "head":     [(1, 0.0), (3,  -8.0), (5, -18.0), (7,  -5.0), (11, 0.0)],
    "tail.01":  [(1, 0.0), (3,  18.0), (5,  30.0), (7,  12.0), (11, 0.0)],
    "tail.02":  [(1, 0.0), (3,  12.0), (5,  20.0), (7,   8.0), (11, 0.0)],
}


def build(_armature_object):
    """포즈 본을 프레임마다 세우고 키를 찍는다. apply_clip.py가 부른다."""
    import bpy

    for bone_name in POSES:
        bone = _armature_object.pose.bones.get(bone_name)
        if bone is None:
            raise SystemExit(f"포즈 본 '{bone_name}'이 없다 - 리그가 바뀌었다")
        bone.rotation_mode = "XYZ"

    for bone_name, frames in POSES.items():
        bone = _armature_object.pose.bones[bone_name]
        for frame, degrees in frames:
            bpy.context.scene.frame_set(frame)
            bone.rotation_euler = (radians(degrees), 0.0, 0.0)
            bone.keyframe_insert(data_path="rotation_euler", frame=frame)
