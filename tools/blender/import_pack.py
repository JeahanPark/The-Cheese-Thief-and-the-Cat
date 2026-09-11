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

# child -> parent (None = 뿌리). 이름 집합만 맞고 부모가 틀리면 트랜스폼 경로
# 문자열이 바뀌는 건 이름 오타와 똑같다 — 에러 없이 커브가 죽는다.
EXPECTED_PARENTS = {
    "root": None,
    "body": "root",
    "head": "body",
    "ear.L": "head",
    "ear.R": "head",
    "leg.F.L": "body",
    "leg.F.R": "body",
    "leg.B.L": "body",
    "leg.B.R": "body",
    "tail.01": "body",
    "tail.02": "tail.01",
}

REQUIRED_ACTIONS = {"Walk"}


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

    if not os.path.isfile(src):
        raise SystemExit(f"입력 FBX가 없다: {src}")

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
        raise SystemExit(
            f"아마추어가 {len(armatures)}개다 — 1개여야 한다: "
            f"{[a.name for a in armatures]}"
        )

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

    # 이름 집합만 맞고 부모-자식이 어긋나면 트랜스폼 경로 문자열이 바뀐다 —
    # 이것도 이름 오타와 같은 종류의 무증상 실패라 따로 검사한다.
    parent_mismatches = []
    for bone_name, expected_parent in EXPECTED_PARENTS.items():
        bone = arm.data.bones[bone_name]
        actual_parent = bone.parent.name if bone.parent is not None else None
        if actual_parent != expected_parent:
            parent_mismatches.append((bone_name, expected_parent, actual_parent))
    if parent_mismatches:
        lines = "\n".join(
            f"  {name}: 기대={expected!r} 실제={actual!r}"
            for name, expected, actual in parent_mismatches
        )
        raise SystemExit(f"뼈 부모-자식 관계가 어긋났다\n{lines}")

    # Rig|Walk 처럼 액션 이름에 아마추어 이름이 접두로 붙어 들어온다 — 유니티
    # 클립 이름은 'Walk'라서, arm.name을 Rig로 못박은 것과 같은 이유로 여기서도
    # 접두를 뗀다. '|'가 없는 이름은 그대로 둔다.
    rename_map = {}
    for action in bpy.data.actions:
        if "|" in action.name:
            rename_map[action] = action.name.rsplit("|", 1)[-1]

    stripped_names = [rename_map.get(a, a.name) for a in bpy.data.actions]
    dupes = {n for n in stripped_names if stripped_names.count(n) > 1}
    if dupes:
        raise SystemExit(f"접두를 떼면 액션 이름이 충돌한다: {sorted(dupes)}")

    for action, new_name in rename_map.items():
        action.name = new_name

    actions = sorted(a.name for a in bpy.data.actions)
    missing_actions = REQUIRED_ACTIONS - set(actions)
    if missing_actions:
        raise SystemExit(f"필수 액션이 없다: {sorted(missing_actions)} (있음: {actions})")

    print(f"BONES {len(got)} OK")
    print(f"PARENTS OK")
    print(f"ACTIONS {len(actions)}: {actions}")

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=dst)
    print(f"SAVED {dst}")


if __name__ == "__main__":
    # 블렌더는 -P 스크립트에서 SystemExit 말고는 종료 코드를 안 남긴다.
    # RuntimeError 등 다른 예외는 트레이스백만 찍고 프로세스가 0으로 끝나서,
    # 이 스크립트 자체가 지키려는 "에러 없이 조용히 실패" 문제에 똑같이 걸린다.
    # 그래서 SystemExit이 아닌 모든 예외를 SystemExit으로 승격한다.
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        raise SystemExit(f"예기치 못한 오류: {e}") from e
