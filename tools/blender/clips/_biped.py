"""두 발 클립 공용 — **몸 높이(root 들어올림)를 딛은 발에 맞춰 풀어 키로 박는다**(2026-09-14 2차).

왜 있나: 1차 두 발 자세는 몸만 -48도 젖히고 뒷다리를 앞으로(+) 돌려 발을 딛게 했다. 그러면 다리가 **배 앞으로 뻗어**
「앉은 쥐」로 보였고, 엉덩이가 바닥을 -17mm 뚫었다(사용자: "다리 위치 자세가 이상해").
2차는 **허벅지 각 = 몸 각**(다리가 세로로 선다 — 뼈가 몸의 자식이라 몸을 젖힌 만큼 되돌리면 수직이다)으로 두고,
**몸 전체를 root로 들어 올려** 발을 바닥에 딛게 한다. B -42면 약 83mm — 엉덩이가 바닥에서 뜨고 다리가 보인다.

들어올림은 손으로 적지 않는다: 회전 키를 다 박은 뒤 **키 프레임마다 root를 0에 두고 뒷발 메시 최저점을 재서**
`FOOT_CLEARANCE_MM`에 오도록 한 값을 root 위치 키로 넣는다. 걸음 중 한 발은 들려 있으므로 **낮은 쪽 발**이 기준이다.
"""
import bpy
from mathutils import Vector

FOOT_CLEARANCE_MM = 1.5
FOOT_GROUPS = ("leg.B.L", "shin.B.L", "leg.B.R", "shin.B.R")


def _owners(_mesh):
    owner = []
    for v in _mesh.data.vertices:
        best, weight = None, 0.0
        for ge in v.groups:
            if ge.weight > weight:
                best, weight = _mesh.vertex_groups[ge.group].name, ge.weight
        owner.append(best)
    return owner


def key_root_lift(_armature_object, _frames, _extra_mm=None, _fixed_mm=None):
    """회전 키를 다 박은 **뒤에** 부른다. `_frames`의 각 프레임에 root 위치 키를 넣는다.
    `_extra_mm`는 {프레임: 추가 mm} — 벌떡 뛰어오르는 순간처럼 발을 일부러 띄울 때만.
    `_fixed_mm`는 {프레임: mm} — 풀지 않고 그 값을 쓴다. **네 발 구간**용: 뒷발만 보고 풀면 웅크릴 때(뒷발 들림)
    몸을 끌어내려 앞발이 바닥을 뚫는다(변신 5프레임 -22mm, 2026-09-14)."""
    mesh = bpy.data.objects.get("Mesh")
    if mesh is None:
        raise SystemExit("'Mesh' 오브젝트가 없다 — 몸 높이를 못 푼다")
    root = _armature_object.pose.bones.get("root")
    if root is None:
        raise SystemExit("'root' 뼈가 없다 — 몸 높이를 못 푼다")
    owner = _owners(mesh)
    feet = [i for i, o in enumerate(owner) if o in FOOT_GROUPS]

    # 월드 +Z를 root 뼈 로컬 축으로 — 뼈 축이 월드와 다르다.
    to_local = (_armature_object.matrix_world @ root.bone.matrix_local).to_3x3().normalized().inverted()
    scene = bpy.context.scene
    lifts = {}
    for frame in _frames:
        if _fixed_mm and frame in _fixed_mm:
            lifts[frame] = _fixed_mm[frame]
            continue
        scene.frame_set(frame)
        root.location = (0.0, 0.0, 0.0)       # 아직 위치 커브가 없어서 frame_set이 덮지 않는다
        bpy.context.view_layer.update()
        ev = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        me = ev.to_mesh()
        mw = mesh.matrix_world
        low = min((mw @ me.vertices[i].co).z for i in feet) * 1000.0
        ev.to_mesh_clear()
        lifts[frame] = FOOT_CLEARANCE_MM - low + (_extra_mm or {}).get(frame, 0.0)

    for frame, lift in lifts.items():
        scene.frame_set(frame)
        root.location = to_local @ Vector((0.0, 0.0, lift / 1000.0))
        root.keyframe_insert(data_path="location", frame=frame)
    print("ROOT LIFT mm " + " ".join("%d:%.1f" % (f, lifts[f]) for f in sorted(lifts)))
    return lifts


def key_rotations(_armature_object, _poses):
    """fortress.py와 같다 — `(프레임, X)` 또는 `(프레임, X, Z)` · Qz @ Qx."""
    from math import radians
    from mathutils import Quaternion

    for bone_name in _poses:
        if _armature_object.pose.bones.get(bone_name) is None:
            raise SystemExit(f"포즈 본 '{bone_name}'이 없다 - 리그가 바뀌었다")
    for bone_name, frames in _poses.items():
        bone = _armature_object.pose.bones[bone_name]
        for key in frames:
            frame, x_deg = key[0], key[1]
            z_deg = key[2] if len(key) > 2 else 0.0
            bpy.context.scene.frame_set(frame)
            bone.rotation_quaternion = Quaternion((0.0, 0.0, 1.0), radians(z_deg)) @ Quaternion((1.0, 0.0, 0.0), radians(x_deg))
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame)


def all_key_frames(_poses):
    return sorted({key[0] for keys in _poses.values() for key in keys})
