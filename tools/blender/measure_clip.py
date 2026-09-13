"""액션 하나를 1/4프레임 간격으로 돌려 **메시** 최저점(바닥 관통)을 잰다.

usage:
  blender -b <file.blend> -P tools/blender/measure_clip.py -- <액션이름>

**뼈로 재지 마라.** 발 메시는 발목뼈 끝보다 37mm 아래까지 있고 꼬리 메시는 뼈보다
19mm 굵다 — 뼈 좌표로 여유가 있어 보이는 자리가 실제로는 잠긴다(2026-09-12~13).
**정수 프레임만 재지 마라.** 키 사이 베지어가 얕은 쪽으로 처진다 — Fortress는
정수 프레임 전부 +1mm인데 6.5에서 -12.8mm였다(2026-09-13).

기준선(팩 자신): Walk -9.1 · Bounce -8.7 · Run -4.7 mm.
"""
import bpy
import sys

GROUPS = ["body", "tail.01", "tail.02", "leg.F.L", "leg.B.L", "leg.F.R", "leg.B.R", "shin.B.L", "shin.B.R",
          "head", "ear.L", "ear.R"]


def main():
    if "--" not in sys.argv:
        raise SystemExit("usage: ... -P measure_clip.py -- <action>")
    name = sys.argv[sys.argv.index("--") + 1]

    arm, mesh = bpy.data.objects.get("Rig"), bpy.data.objects.get("Mesh")
    if arm is None or mesh is None:
        raise SystemExit("'Rig'/'Mesh' 오브젝트가 없다")
    act = bpy.data.actions.get(name)
    if act is None:
        raise SystemExit(f"액션 '{name}'이 없다")

    # 버텍스마다 가중치가 가장 큰 그룹 하나에 귀속시킨다 — 어디가 뚫는지 이름으로 말하려고.
    index_to_group = {mesh.vertex_groups[g].index: g for g in GROUPS if g in mesh.vertex_groups}
    owner = []
    for v in mesh.data.vertices:
        best, weight = None, 0.0
        for ge in v.groups:
            if ge.group in index_to_group and ge.weight > weight:
                best, weight = index_to_group[ge.group], ge.weight
        owner.append(best)

    arm.animation_data.action = act
    if act.slots:
        arm.animation_data.action_slot = act.slots[0]

    scene = bpy.context.scene
    start, end = int(act.frame_range[0]), int(act.frame_range[1])
    worst = (1e9, None, None)
    f = float(start)
    while f <= end + 1e-6:
        scene.frame_set(int(f), subframe=f - int(f))
        ev = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        me = ev.to_mesh()
        mw = mesh.matrix_world
        lo = (1e9, None)
        for i, v in enumerate(me.vertices):
            z = (mw @ v.co).z * 1000.0
            if z < lo[0]:
                lo = (z, owner[i])
        ev.to_mesh_clear()
        if abs(f - round(f)) < 1e-6:
            print("FRAME %2d  min %.1fmm (%s)" % (round(f), lo[0], lo[1]))
        if lo[0] < worst[0]:
            worst = (lo[0], f, lo[1])
        f += 0.25
    print("WORST %.1fmm at frame %.2f (%s)" % worst)


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        raise SystemExit(f"예기치 못한 오류: {e}") from e
