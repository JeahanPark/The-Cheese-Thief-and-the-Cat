"""블렌더 동작을 유니티가 읽는 FBX로 내보낸다.

usage:
  blender -b art/blender/mouse.blend -P tools/blender/export_clips.py -- <out.fbx>

설정을 코드에 두는 이유: 체크박스 하나가 조용히 파이프라인을 죽인다. 내보내기 창에서
손으로 찍으면 다음 사람이 같은 조합을 다시 맞출 방법이 없고, 틀려도 에러가 안 난다 —
유니티 Generic 리그는 트랜스폼 경로 문자열로 뼈를 찾으므로 경로가 어긋나면 그냥
안 움직인다. 그래서 중요한 인자는 전부 여기에 박고 이유를 적는다.

  add_leaf_bones            켜져 있으면 없던 말단 뼈(`ear.L_end` 따위)가 생겨 Generic
                            경로가 어긋난다. 블렌더 기본값이 True라 반드시 꺼야 한다.
  bake_anim_simplify_factor 0이 아니면 키를 솎아내 원본과 값이 달라진다. 기본값 1.0은
                            "적당히 줄여라"라서, 왕복이 같은지 재는 이 일에는 독이다.
  bake_anim_step            1.0 = 한 프레임마다 굽는다. 키를 건너뛰면 원본 곡선 모양이
                            바뀐다.
  use_armature_deform_only  유니티가 원하는 뼈대만 내보낸다 — 변형에 안 쓰는 컨트롤
                            뼈가 섞이면 그것도 경로가 된다.
  primary/secondary_bone_axis, axis_forward/up
                            뼈 축·좌표계 규약. 팩 FBX가 들어온 규약 그대로 돌려보내야
                            회전 커브 값이 보존된다.
  apply_scale_options       **이게 왕복을 갈랐다**(2026-09-12 실측). 100배 단위 환산을
                            누가 떠안는지 정하는 스위치다. FBX_SCALE_ALL이면 환산이
                            FBX 전역 스케일로 가서 유니티가 상쇄해 버린다 —
                            `Rig` 스케일 1 · 뼈 위치 미터. 팩 클립은 반대로
                            `Rig` 스케일 100 · 뼈 위치 1/100이다.
                            **합성 트랜스폼은 같지만 뼈의 월드 스케일이 100배 다르다** →
                            그 클립을 팩 프리팹에 얹으면 스키닝이 1/100로 쭈그러든다.
                            에러는 안 난다. FBX_SCALE_NONE이 환산을 오브젝트 트랜스폼에
                            실어 팩과 같은 배치를 만든다.
"""
import bpy
import os
import sys

ARMATURE_NAME = "Rig"


def args_after_dashdash():
    if "--" not in sys.argv:
        raise SystemExit("usage: ... -P export_clips.py -- <out.fbx>")
    return sys.argv[sys.argv.index("--") + 1:]


def reset_pose(_arm):
    """포즈 본을 쉬는 자세로 되돌린다 — **액션이 키를 안 찍은 뼈는 이 값으로 구워진다.**

    2026-09-19 실측: 저장된 .blend의 포즈가 마지막으로 평가한 두 발 클립(뿌리 83mm 들어올림 ·
    뒷다리 두 발 각도)에 머물러 있었고, 뿌리·귀를 안 건드리는 자작 클립(Shoot·ShootDraw·Dash·Fortress·
    Sticky·Warehouse)이 **그 자세를 물려받아 83mm 뜬 채로** 내보내졌다(9-14 내보내기부터). 에러는 없다 —
    FBX를 다시 들여 첫 프레임 발 높이를 재서야 보였다(뒷발 84mm · 앞발 115mm, 정상은 1 · 32).
    내보내기 도구는 액션 사이에 **처음 포즈**로 되돌리므로, 처음 포즈만 쉬는 자세면 된다.
    """
    if _arm.animation_data is None:
        _arm.animation_data_create()
    _arm.animation_data.action = None
    for pb in _arm.pose.bones:
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def main():
    argv = args_after_dashdash()
    if len(argv) != 1:
        raise SystemExit("usage: ... -P export_clips.py -- <out.fbx>")

    out = os.path.abspath(argv[0])
    os.makedirs(os.path.dirname(out), exist_ok=True)

    arm = bpy.data.objects.get(ARMATURE_NAME)
    if arm is None:
        raise SystemExit(f"아마추어 '{ARMATURE_NAME}'이 없다 — 이 이름이 곧 유니티 경로다")

    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm

    reset_pose(arm)

    bpy.ops.export_scene.fbx(
        filepath=out,
        use_selection=True,
        object_types={"ARMATURE"},
        add_leaf_bones=False,
        use_armature_deform_only=True,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        axis_forward="-Z",
        axis_up="Y",
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
    )

    print(f"EXPORTED {out}")


if __name__ == "__main__":
    # 블렌더는 -P 스크립트에서 SystemExit 말고는 종료 코드를 안 남긴다.
    # RuntimeError 등 다른 예외는 트레이스백만 찍고 프로세스가 0으로 끝나서,
    # "에러 없이 조용히 성공처럼 보이는" 실패가 된다 — 이 과제가 잡으려는 바로 그 종류다.
    # 그래서 SystemExit이 아닌 모든 예외를 SystemExit으로 승격한다.
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        raise SystemExit(f"예기치 못한 오류: {e}") from e
