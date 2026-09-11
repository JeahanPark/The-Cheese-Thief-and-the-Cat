"""액션 하나의 커브를 다시 실행 가능한 파이썬으로 뱉는다.

usage:
  blender -b <file.blend> -P tools/blender/dump_action.py -- <액션이름> <out.py>

왜 필요한가: .blend는 바이너리라 diff가 안 된다. 사람이 뷰포트에서 손으로 다듬은 뒤
이걸로 숫자를 다시 구우면 git에 남고 명령 한 줄로 재생성된다.

블렌더 5.x는 action.fcurves가 없다 - 커브가 layers/strips/channelbag 밑으로 내려갔다.
"""
import bpy
import os
import sys


def args_after_dashdash():
    if "--" not in sys.argv:
        raise SystemExit("usage: ... -P dump_action.py -- <action> <out.py>")
    return sys.argv[sys.argv.index("--") + 1:]


def fcurves_of(_action):
    """블렌더 5.x의 레이어드 액션에서 F커브를 전부 끌어낸다. 4.x 배치도 받아 준다."""
    direct = getattr(_action, "fcurves", None)
    if direct is not None and len(direct) > 0:
        return list(direct)

    out = []
    for layer in getattr(_action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for slot in _action.slots:
                bag = strip.channelbag(slot)
                if bag is None:
                    continue
                out.extend(bag.fcurves)
    return out


def main():
    argv = args_after_dashdash()
    if len(argv) != 2:
        raise SystemExit("usage: ... -P dump_action.py -- <action> <out.py>")

    name, out_path = argv[0], os.path.abspath(argv[1])

    action = bpy.data.actions.get(name)
    if action is None:
        have = sorted(a.name for a in bpy.data.actions)
        raise SystemExit(f"액션 '{name}'이 없다. 있는 것: {have}")

    curves = fcurves_of(action)
    if not curves:
        raise SystemExit(f"액션 '{name}'에 커브가 0개다 - 덤프가 잘못됐다")

    lines = [
        '"""%s - dump_action.py가 만든 것. 손으로 고치지 마라."""' % name,
        "",
        "NAME = %r" % name,
        "",
        "# (data_path, array_index, [(frame, value), ...])",
        "CURVES = [",
    ]
    for fc in sorted(curves, key=lambda c: (c.data_path, c.array_index)):
        pts = [(round(k.co[0], 6), round(k.co[1], 6)) for k in fc.keyframe_points]
        lines.append("    (%r, %d, %r)," % (fc.data_path, fc.array_index, pts))
    lines.append("]")
    lines.append("")

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))

    print(f"DUMPED {name} curves={len(curves)} -> {out_path}")


if __name__ == "__main__":
    # 블렌더는 -P 스크립트에서 SystemExit 말고는 종료 코드를 안 남긴다.
    # 트레이스백은 찍히는데 0으로 끝나서 자동화가 실패를 성공으로 읽는다.
    try:
        main()
    except SystemExit:
        raise
    except Exception as e:
        raise SystemExit(f"예기치 못한 오류: {e}") from e
