"""팩 리그와 동작을 빈 블렌더 파일에 들인다.

usage:
  blender -b --factory-startup -P tools/blender/import_pack.py -- <in.fbx> <out.blend>

들인 뒤 아마추어 오브젝트 이름을 반드시 'Rig'로 맞춘다 — 유니티 Generic 리그는
트랜스폼 경로 문자열로 뼈를 찾고, 팩 프리팹의 그 자리 이름이 'Rig'다.
어긋나면 에러 없이 모든 커브가 죽는다.
"""
import bpy
import os
import sys

ARMATURE_NAME = "Rig"

EXPECTED_BONES = {
    "root", "body", "head", "ear.L", "ear.R",
    "leg.F.L", "leg.F.R", "leg.B.L", "leg.B.R",
    "tail.01", "tail.02",
}


def args_after_dashdash():
    if "--" not in sys.argv:
        raise SystemExit("usage: ... -P import_pack.py -- <in.fbx> <out.blend>")
    return sys.argv[sys.argv.index("--") + 1:]


def main():
    argv = args_after_dashdash()
    if len(argv) != 2:
        raise SystemExit("usage: ... -P import_pack.py -- <in.fbx> <out.blend>")

    src = os.path.abspath(argv[0])
    dst = os.path.abspath(argv[1])

    # NOTE (measured 2026-09-12): for THIS pack FBX, ignore_leaf_bones=True is wrong.
    # A diagnostic dump with the flag off shows the file has no invented "_end" bones
    # at all -- Blender's leaf-removal heuristic instead strips the real, legitimately
    # animated 'tail.02' bone (it has no children, so it looks like an invented leaf
    # marker to the heuristic, but it isn't one). With the flag off, exactly the 11
    # expected bones come through with no extras. So the flag is off here; if a future
    # pack FBX DOES carry invented leaf bones, EXPECTED_BONES below will catch it as
    # an 'extra' and the flag can be revisited per-file.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(
        filepath=src,
        ignore_leaf_bones=False,
        automatic_bone_orientation=False,
    )

    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(armatures) != 1:
        raise SystemExit(f"아마추어가 {len(armatures)}개다 — 1개여야 한다")

    arm = armatures[0]
    arm.name = ARMATURE_NAME
    arm.data.name = ARMATURE_NAME

    got = {b.name for b in arm.data.bones}
    missing = EXPECTED_BONES - got
    extra = got - EXPECTED_BONES
    if missing or extra:
        raise SystemExit(
            "뼈 이름이 어긋났다\n"
            f"  빠짐:   {sorted(missing)}\n"
            f"  늘어남: {sorted(extra)}"
        )

    actions = sorted(a.name for a in bpy.data.actions)
    print(f"BONES {len(got)} OK")
    print(f"ACTIONS {len(actions)}: {actions}")

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=dst)
    print(f"SAVED {dst}")


main()
