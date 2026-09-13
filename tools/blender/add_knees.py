"""뒷다리에 무릎을 넣는다 — `leg.B.L/R` 아래에 `shin.B.L/R`을 붙이고 발 쪽 정점 가중치를 나눠 준다.

usage:
  blender -b art/blender/mouse.blend -P tools/blender/add_knees.py -- <out.blend>

**왜**: 팩 리그의 뒷다리는 외뼈라 「웅크리기」가 안 된다. 몸통을 젖히면(Shoot·Fortress)
발을 바닥에 두려고 다리 **전체**를 앞으로 55도 기울여야 했다 — 발이 엉덩이보다 127mm 앞에
놓여 「딛고 선다」가 아니라 버팀목으로 보였다(shoot.py 뒷다리 주석). 무릎이 있으면
허벅지는 앞으로, 정강이는 수직으로 세워 발을 엉덩이 밑에 둘 수 있다.

**무릎 자리는 메시가 정했다**(2026-09-13 실측). 뒷다리 메시는 링 셋짜리 원기둥이다 —
z = 188 · **97** · 6mm(+ 발끝 0). 가운데 링이 곧 관절이 접힐 수 있는 유일한 자리다.
뼈 머리(192.7)에서 발목 끝(37.5)까지 155.2mm 중 **0.617** 지점이다.

**기존 동작은 1도도 안 바뀌어야 한다.** 무릎 뼈는 허벅지와 같은 방향으로 이어 붙이므로
(연결·같은 롤) 무릎을 안 굽히는 클립에서는 정강이가 허벅지를 그대로 따라가고, 가중치를
어떻게 나눴든 정점 위치가 원래와 같다. 이 스크립트가 끝에 **액션 전부를 돌려 정점을 대조**한다 —
하나라도 0.01mm를 넘게 달라지면 저장하지 않고 죽는다.

**가중치 나누기 공식은 유니티와 같아야 한다**(`KneeWeightLogic.ShinShare`). 블렌더에서 잰
바닥·렌더와 게임 화면이 갈리면 안 되기 때문이다. 둘 다 뼈 축 위의 정규화 위치 s로 계산한다:
    share = smoothstep(KNEE - HALF, KNEE + HALF, s)    KNEE 0.617 · HALF 0.129(= 20mm)
가운데 링(s≈0.617)은 반반, 아래 링·발끝은 정강이 전부, 위 링은 허벅지 전부다.
"""
import bpy
import os
import sys

ARMATURE = "Rig"
MESH = "Mesh"
SIDES = ("L", "R")
KNEE = 0.617
HALF = 0.129
TOLERANCE_MM = 0.01


def smoothstep(_e0, _e1, _x):
    t = min(1.0, max(0.0, (_x - _e0) / (_e1 - _e0)))
    return t * t * (3.0 - 2.0 * t)


def shin_share(_s):
    return smoothstep(KNEE - HALF, KNEE + HALF, _s)


def snapshot(_arm, _mesh):
    """액션마다 모든 프레임의 정점 월드 좌표."""
    scene = bpy.context.scene
    out = {}
    for action in sorted(bpy.data.actions, key=lambda a: a.name):
        _arm.animation_data.action = action
        if action.slots:
            _arm.animation_data.action_slot = action.slots[0]
        start, end = int(action.frame_range[0]), int(action.frame_range[1])
        frames = []
        for f in range(start, end + 1):
            scene.frame_set(f)
            ev = _mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            me = ev.to_mesh()
            frames.append([(_mesh.matrix_world @ v.co).copy() for v in me.vertices])
            ev.to_mesh_clear()
        out[action.name] = frames
    return out


def main():
    if "--" not in sys.argv:
        raise SystemExit("usage: ... -P add_knees.py -- <out.blend>")
    dst = os.path.abspath(sys.argv[sys.argv.index("--") + 1])

    arm, mesh = bpy.data.objects.get(ARMATURE), bpy.data.objects.get(MESH)
    if arm is None or mesh is None:
        raise SystemExit("'Rig'/'Mesh'가 없다")
    if arm.data.bones.get("shin.B.L") is not None:
        raise SystemExit("이미 무릎이 있다 — 두 번 돌리지 마라(가중치가 두 번 나뉜다)")

    if arm.animation_data is None:
        arm.animation_data_create()
    before = snapshot(arm, mesh)

    # ── 뼈 ──
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    params = {}
    for side in SIDES:
        thigh = eb[f"leg.B.{side}"]
        head, tail, roll = thigh.head.copy(), thigh.tail.copy(), thigh.roll
        knee = head.lerp(tail, KNEE)
        params[side] = (head.copy(), tail.copy())
        thigh.tail = knee
        thigh.roll = roll
        shin = eb.new(f"shin.B.{side}")
        shin.head = knee
        shin.tail = tail
        shin.roll = roll
        shin.parent = thigh
        shin.use_connect = True
        shin.use_deform = True
    bpy.ops.object.mode_set(mode="POSE")
    for side in SIDES:
        pb = arm.pose.bones[f"shin.B.{side}"]
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
    bpy.ops.object.mode_set(mode="OBJECT")

    # ── 가중치 ── 뼈 축(머리→원래 발목 끝) 위의 정규화 위치로 나눈다.
    inv_arm = arm.matrix_world.inverted()
    for side in SIDES:
        head, tail = params[side]
        axis = tail - head
        length_sq = axis.length_squared
        thigh_group = mesh.vertex_groups[f"leg.B.{side}"]
        shin_group = mesh.vertex_groups.new(name=f"shin.B.{side}")
        moved = 0
        for v in mesh.data.vertices:
            w = None
            for g in v.groups:
                if g.group == thigh_group.index:
                    w = g.weight
            if w is None or w <= 0.0:
                continue
            p = inv_arm @ (mesh.matrix_world @ v.co)
            s = (p - head).dot(axis) / length_sq
            share = shin_share(s)
            if share <= 0.0:
                continue
            thigh_group.add([v.index], w * (1.0 - share), "REPLACE")
            shin_group.add([v.index], w * share, "REPLACE")
            moved += 1
        print(f"KNEE {side}: 정점 {moved}개를 정강이와 나눴다")

    # ── 대조 ── 무릎을 안 굽히는 기존 액션은 정점이 그대로여야 한다.
    after = snapshot(arm, mesh)
    worst = (0.0, None)
    for name, frames in before.items():
        for fi, verts in enumerate(frames):
            for a, b in zip(verts, after[name][fi]):
                d = (a - b).length * 1000.0
                if d > worst[0]:
                    worst = (d, f"{name} f{fi + 1}")
    print("PARITY worst %.4fmm at %s (%d actions)" % (worst[0], worst[1], len(before)))
    if worst[0] > TOLERANCE_MM:
        raise SystemExit(f"무릎을 넣었더니 기존 동작이 바뀌었다({worst[0]:.4f}mm) — 저장하지 않는다")

    bpy.ops.wm.save_as_mainfile(filepath=dst)
    print(f"SAVED {dst}")


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        raise SystemExit(f"예기치 못한 오류: {e}") from e
