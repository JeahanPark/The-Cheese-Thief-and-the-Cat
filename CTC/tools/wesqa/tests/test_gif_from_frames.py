# -*- coding: utf-8 -*-
"""gif_from_frames.py 단위 테스트 — 게임·에디터 불필요, 합성 PNG만으로 돈다.

gif_from_frames.py는 tools/wesqa 옆(tools/)에 있어 wesqa 패키지가 아니다 — conftest.py는
`tools/wesqa`만 sys.path에 넣어 주므로 그 한 단계 위(`tools/`)는 여기서 직접 잡는다.
"""
import os
import sys

import pytest
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS_DIR = os.path.dirname(os.path.dirname(HERE))  # tests -> wesqa -> tools
sys.path.insert(0, TOOLS_DIR)

from gif_from_frames import (  # noqa: E402  (경로 세팅 뒤에 임포트)
    GifAssemblyError,
    build_gif,
    find_frames,
    frame_index,
    resolve_pattern,
)


def _make_png(path, size=(20, 20), color=(255, 0, 0)):
    Image.new("RGB", size, color).save(path)


# ---------- frame_index / 정렬 ----------

def test_frame_index_reads_the_zero_padded_token():
    assert frame_index("ctc-clip-preview-roundtrip-ShootDraw-02-bf03.png") == 2
    assert frame_index("ctc-clip-preview-roundtrip-ShootDraw-10-bf11.png") == 10


def test_frame_index_raises_when_no_numeric_token():
    with pytest.raises(GifAssemblyError):
        frame_index("no-numbers-here.png")


def test_find_frames_sorts_numerically_not_lexically(tmp_path):
    # 0채움 없이 만든다 — 사전순이면 문자 '1' < '2'라 "...-10-..."이 "...-2-..." 앞에
    # 온다("ctc-clip-preview-roundtrip-ShootDraw-1..." vs "...-2..." 비교 지점에서 갈린다).
    # 실제 ClipPreviewRender는 항상 0채움(:D2)이라 이 사고가 안 나지만, frame_index 자체는
    # 0채움에 기대지 않고 숫자값으로 비교해야 한다 — 그게 이 테스트의 요구사항이다.
    names = [
        "ctc-clip-preview-roundtrip-ShootDraw-10-bf11.png",
        "ctc-clip-preview-roundtrip-ShootDraw-2-bf3.png",
        "ctc-clip-preview-roundtrip-ShootDraw-0-bf1.png",
    ]
    for n in names:
        _make_png(os.path.join(str(tmp_path), n))

    pattern = os.path.join(str(tmp_path), "*ShootDraw*.png")
    frames = find_frames(pattern)

    indices = [frame_index(p) for p in frames]
    assert indices == [0, 2, 10]  # 사전순이었다면 [0, 10, 2]로 나왔을 것


# ---------- resolve_pattern ----------

def test_resolve_pattern_prefers_explicit_glob():
    assert resolve_pattern("*.png", None, None) == "*.png"


def test_resolve_pattern_builds_from_dir_and_clip():
    pattern = resolve_pattern(None, "/tmp/frames", "Shoot")
    assert pattern == os.path.join("/tmp/frames", "*Shoot*.png")


def test_resolve_pattern_raises_without_enough_info():
    with pytest.raises(GifAssemblyError):
        resolve_pattern(None, None, None)


# ---------- find_frames — 개수 부족 ----------

def test_find_frames_too_few_raises_with_pattern_named(tmp_path):
    _make_png(os.path.join(str(tmp_path), "ctc-clip-preview-roundtrip-Walk-00-bf01.png"))
    pattern = os.path.join(str(tmp_path), "*Walk*.png")

    with pytest.raises(GifAssemblyError) as exc_info:
        find_frames(pattern)

    assert pattern in str(exc_info.value)


# ---------- build_gif — 크기 불일치 ----------

def test_build_gif_rejects_mismatched_sizes(tmp_path):
    p1 = os.path.join(str(tmp_path), "a-00-bf01.png")
    p2 = os.path.join(str(tmp_path), "a-01-bf02.png")
    _make_png(p1, size=(20, 20))
    _make_png(p2, size=(30, 30))

    with pytest.raises(GifAssemblyError):
        build_gif([p1, p2], os.path.join(str(tmp_path), "out.gif"))


# ---------- build_gif — 실제 GIF 생성 ----------

def _make_varied_frames(directory, count=5, size=(24, 24)):
    """프레임마다 실제로 다른 그림을 만든다 — "11장이 다 같은 그림"이면 실패로 잡기 위해."""
    paths = []
    for i in range(count):
        path = os.path.join(directory, f"ctc-clip-preview-gif-Test-{i:02d}-bf{i + 1:02d}.png")
        img = Image.new("RGB", size, (10, 10, 10))
        # 인덱스에 따라 흰 사각형 위치를 옮긴다 — 프레임 간 실제 픽셀 차이를 만든다.
        for x in range(i * 4, i * 4 + 4):
            for y in range(4):
                if x < size[0]:
                    img.putpixel((x, y), (255, 255, 255))
        img.save(path)
        paths.append(path)
    return paths


def test_build_gif_produces_readable_gif_with_expected_frame_count(tmp_path):
    frame_paths = _make_varied_frames(str(tmp_path), count=5)
    out_path = os.path.join(str(tmp_path), "out.gif")

    count = build_gif(frame_paths, out_path, fps=24, loop=0, scale=1)
    assert count == 5

    # 쓴 쪽 리턴값을 믿지 않고 Pillow로 다시 읽어 확인한다.
    with Image.open(out_path) as gif:
        assert gif.is_animated
        assert gif.n_frames == 5

        # GIF 포맷 자체가 지속시간을 1/100초(센티초) 단위로만 저장한다 — 41.67ms를
        # 요청해도 파일에는 10ms 배수로 반올림된 값이 남는다. 그 양자화까지 감안한 값과 비교한다.
        requested_ms = round(1000 / 24)
        expected_stored_ms = round(requested_ms / 10) * 10
        gif.seek(0)
        assert gif.info["duration"] == expected_stored_ms

        gif.seek(0)
        first_bytes = gif.convert("RGB").tobytes()
        gif.seek(4)
        last_bytes = gif.convert("RGB").tobytes()
        assert first_bytes != last_bytes, "프레임이 전부 같은 그림이면 애니메이션이 아니다"


def test_build_gif_upscales_with_nearest_neighbour(tmp_path):
    frame_paths = _make_varied_frames(str(tmp_path), count=3, size=(10, 10))
    out_path = os.path.join(str(tmp_path), "out_scaled.gif")

    build_gif(frame_paths, out_path, scale=2)

    with Image.open(out_path) as gif:
        assert gif.size == (20, 20)


# ---------- CLI 진입점 ----------

def test_main_end_to_end_via_dir_and_clip(tmp_path):
    from gif_from_frames import main

    _make_varied_frames(str(tmp_path), count=4)
    out_path = os.path.join(str(tmp_path), "cli_out.gif")

    exit_code = main([
        "--dir", str(tmp_path),
        "--clip", "Test",
        "--out", out_path,
    ])

    assert exit_code == 0
    assert os.path.exists(out_path)


def test_main_returns_nonzero_on_too_few_frames(tmp_path, capsys):
    from gif_from_frames import main

    _make_png(os.path.join(str(tmp_path), "ctc-clip-preview-gif-Lonely-00-bf01.png"))
    out_path = os.path.join(str(tmp_path), "wont_exist.gif")

    exit_code = main([
        "--dir", str(tmp_path),
        "--clip", "Lonely",
        "--out", out_path,
    ])

    assert exit_code == 1
    assert not os.path.exists(out_path)
    captured = capsys.readouterr()
    assert "에러" in captured.err
