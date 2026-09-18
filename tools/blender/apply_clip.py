"""clips/<이름>.py 하나를 .blend에 적용하고 저장한다.

usage:
  blender -b <file.blend> -P tools/blender/apply_clip.py -- <clips/x.py> <out.blend>

액션 이름은 접두 없이 둔다(`Shoot`). 내보낼 때 블렌더가 `Rig|Shoot`으로 바꿔 쓰고,
유니티 쪽 .meta의 clipAnimations가 그걸 다시 `Shoot`으로 맞춘다.
"""
import bpy
import importlib.util
import os
import sys

ARMATURE_NAME = "Rig"


def args_after_dashdash():
    if "--" not in sys.argv:
        raise SystemExit("usage: ... -P apply_clip.py -- <clips/x.py> <out.blend>")
    return sys.argv[sys.argv.index("--") + 1:]


def load_clip_module(_path):
    spec = importlib.util.spec_from_file_location("clipdef", _path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    for attr in ("NAME", "FRAME_START", "FRAME_END", "build"):
        if not hasattr(mod, attr):
            raise SystemExit(f"{_path}에 {attr}가 없다")
    return mod


def main():
    argv = args_after_dashdash()
    if len(argv) != 2:
        raise SystemExit("usage: ... -P apply_clip.py -- <clips/x.py> <out.blend>")

    clip_path, dst = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    if not os.path.isfile(clip_path):
        raise SystemExit(f"클립 정의가 없다: {clip_path}")

    mod = load_clip_module(clip_path)

    arm = bpy.data.objects.get(ARMATURE_NAME)
    if arm is None:
        raise SystemExit(f"아마추어 '{ARMATURE_NAME}'이 없다 - 이 이름이 곧 유니티 경로다")

    # 같은 이름이 이미 있으면 지우고 새로 만든다 - 굴릴 때마다 덧쌓이면 안 된다.
    old = bpy.data.actions.get(mod.NAME)
    if old is not None:
        bpy.data.actions.remove(old)

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")

    if arm.animation_data is None:
        arm.animation_data_create()
    action = bpy.data.actions.new(mod.NAME)
    action.use_fake_user = True          # 쓰는 데가 없어도 저장될 때 살아남는다
    arm.animation_data.action = action

    bpy.context.scene.frame_start = mod.FRAME_START
    bpy.context.scene.frame_end = mod.FRAME_END

    mod.build(arm)

    # 클립 정의가 rotation_mode를 건드리면 그 순간 다른 액션 19개의 베이크가 깨진다.
    # 액션이 아니라 아마추어의 속성이라서다 - 저장되면 전부에 걸린다.
    bad = [b.name for b in arm.pose.bones if b.rotation_mode != "QUATERNION"]
    if bad:
        raise SystemExit(
            f"포즈 본의 rotation_mode가 바뀌었다: {bad}\n"
            "클립 정의는 rotation_mode를 건드리면 안 된다 - "
            "rotation_quaternion으로 키를 찍어라."
        )

    bpy.ops.object.mode_set(mode="OBJECT")

    got = bpy.data.actions.get(mod.NAME)
    if got is None:
        raise SystemExit(f"'{mod.NAME}' 액션이 안 만들어졌다")

    print(f"APPLIED {mod.NAME} frames={mod.FRAME_START}..{mod.FRAME_END}")
    print(f"ACTIONS {sorted(a.name for a in bpy.data.actions)}")

    # 저장 전에 포즈를 쉬는 자세로 — 안 그러면 이 파일을 여는 다음 도구(내보내기·측정)가 이 액션의
    # 마지막 프레임 자세를 「키 없는 뼈의 값」으로 물려받는다(export_clips.reset_pose 머리말).
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)

    bpy.ops.wm.save_as_mainfile(filepath=dst)
    print(f"SAVED {dst}")


if __name__ == "__main__":
    # 블렌더는 SystemExit 말고는 종료 코드를 안 남긴다 - 실패가 성공으로 보인다.
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        raise SystemExit(f"예기치 못한 오류: {e}") from e
