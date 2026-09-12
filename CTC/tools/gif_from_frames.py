# -*- coding: utf-8 -*-
"""ClipPreviewRender(유니티)가 뽑은 프레임 PNG를 애니메이션 GIF로 묶는다.

**왜 이게 유니티 쪽과 분리된 파이썬인가**: 정지 이미지 6~11장으로는 타이밍(리코일이
박자로 읽히는지, 뽑기가 너무 빠른지, 1번과 11번 프레임이 정말 같은지)을 판단할 수
없다 — 그건 움직여야만 보인다. 유니티는 프레임을 굽는 것까지만 하고, 그걸 움직이는
그림으로 조립하는 건 이미지 처리 전용 도구(Pillow)가 더 간단하고 빠르다.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import sys

from PIL import Image

# 파일명 규약(ClipPreviewRender.cs): "<prefix>-<index:D2>-<suffix>.png" 형태로
# 0채움 인덱스가 접미사(bf03, a025 등) 바로 앞 하이픈 구간에 온다. 접미사는 문자를
# 섞어 쓰므로(bf, a) "'-'로 나눈 조각 중 순수 숫자인 마지막 조각"이 곧 인덱스다 —
# 특정 접미사 이름에 의존하지 않아 다른 렌더러가 다른 접미사를 붙여도 그대로 통한다.
_DIGIT_TOKEN = re.compile(r"^\d+$")


class GifAssemblyError(Exception):
    """프레임 찾기·검증 단계에서 사람이 읽을 이유를 달아 던지는 예외."""


def frame_index(path: str) -> int:
    """파일명에서 재생 순서 인덱스를 뽑는다. **사전순이 아니라 숫자순**이 목적이다 —
    사전순이면 "10"이 "2"보다 앞에 온다(문자 '1' < '2')."""
    stem = os.path.splitext(os.path.basename(path))[0]
    numeric_tokens = [tok for tok in stem.split("-") if _DIGIT_TOKEN.match(tok)]
    if not numeric_tokens:
        raise GifAssemblyError(f"파일명에서 숫자 인덱스를 못 찾았다: {path}")
    return int(numeric_tokens[-1])


def resolve_pattern(_glob: str | None, _dir: str | None, _clip: str | None) -> str:
    """글롭을 직접 받거나, 디렉터리+클립명으로 글롭을 만든다."""
    if _glob:
        return _glob
    if _dir and _clip:
        return os.path.join(_dir, f"*{_clip}*.png")
    raise GifAssemblyError("--glob 하나, 또는 --dir + --clip 조합이 필요하다")


def find_frames(pattern: str) -> list[str]:
    """패턴에 맞는 프레임을 숫자 인덱스 순으로 정렬해 돌려준다.
    2장 미만이면 애니메이션이 성립하지 않으므로 여기서 확실히 에러를 낸다."""
    paths = glob.glob(pattern)
    if len(paths) < 2:
        raise GifAssemblyError(
            f"프레임을 2장도 못 찾았다 (검색 패턴: {pattern}, 찾은 개수: {len(paths)})"
        )
    return sorted(paths, key=frame_index)


def build_gif(
    frame_paths: list[str],
    out_path: str,
    fps: float = 24.0,
    loop: int = 0,
    scale: int = 1,
) -> int:
    """정렬된 프레임 목록을 애니메이션 GIF 한 장으로 굽는다. 성공하면 프레임 수를 돌려준다."""
    if len(frame_paths) < 2:
        raise GifAssemblyError(f"프레임이 2장 미만이다: {frame_paths}")

    images = [Image.open(p) for p in frame_paths]

    sizes = {im.size for im in images}
    if len(sizes) > 1:
        # 크기가 다르면 letterbox 등으로 조용히 맞추지 않는다 — 어느 프레임이 다른
        # 해상도로 나왔다는 뜻이고, 그건 렌더 쪽 버그이지 조립 쪽에서 감출 문제가 아니다.
        detail = ", ".join(f"{os.path.basename(p)}={im.size}" for p, im in zip(frame_paths, images))
        raise GifAssemblyError(f"프레임 크기가 서로 다르다: {detail}")

    if scale and scale != 1:
        # NEAREST로 확대한다 — 이 렌더는 평면 음영이라(조명 없는 실루엣 판정용) 부드러운
        # 리샘플은 가장자리를 흐려 오히려 읽기 어려워진다. 정수배 확대 + 최근접이 픽셀
        # 경계를 그대로 유지해 384px 렌더도 768px 화면에서 또렷하게 읽힌다.
        images = [im.resize((im.width * scale, im.height * scale), Image.NEAREST) for im in images]

    # 팔레트 256색 고정 — GIF 포맷 자체의 한계이자, 이 렌더가 평면 음영(그라데이션이
    # 거의 없다)이라 애초에 손실이 적다. **첫 프레임에서 뽑은 팔레트를 전체 프레임에
    # 강제로 씌운다**(quantize(palette=...)) — 프레임마다 따로 양자화하면 팔레트가
    # 미세하게 달라져, 안 움직이는 배경까지 프레임마다 색이 흔들리는 것처럼 보인다
    # (양자화 플리커). 같은 팔레트를 쓰면 실제로 바뀐 픽셀만 바뀐 것처럼 보인다.
    base_palette = images[0].convert("RGB").quantize(colors=256, method=Image.MEDIANCUT)
    quantized = [im.convert("RGB").quantize(palette=base_palette) for im in images]

    duration_ms = round(1000.0 / fps)
    quantized[0].save(
        out_path,
        save_all=True,
        append_images=quantized[1:],
        duration=duration_ms,
        loop=loop,
        disposal=2,
    )
    return len(quantized)


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--glob", dest="glob_pattern", help="프레임 PNG를 찾을 글롭 패턴")
    parser.add_argument("--dir", help="프레임이 있는 디렉터리 (--clip과 같이 쓴다)")
    parser.add_argument("--clip", help="클립 이름 — --dir과 합쳐 글롭을 만든다")
    parser.add_argument("--out", required=True, help="출력 GIF 경로")
    parser.add_argument("--fps", type=float, default=24.0, help="초당 프레임 (기본 24 — 클립과 동일)")
    parser.add_argument("--loop", type=int, default=0, help="반복 횟수, 0=무한 (기본 0)")
    parser.add_argument("--scale", type=int, default=1, help="정수배 최근접 확대 (기본 1=원본 크기)")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        pattern = resolve_pattern(args.glob_pattern, args.dir, args.clip)
        frame_paths = find_frames(pattern)
        count = build_gif(
            frame_paths, args.out, fps=args.fps, loop=args.loop, scale=args.scale
        )
    except GifAssemblyError as e:
        print(f"[gif_from_frames] 에러: {e}", file=sys.stderr)
        return 1

    print(f"[gif_from_frames] 완료: {args.out} ({count}프레임)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
