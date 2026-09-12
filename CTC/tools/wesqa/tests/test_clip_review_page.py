# -*- coding: utf-8 -*-
"""clip_review_page.py 단위 테스트 — 게임·유니티·블렌더 불필요.

클립 정의는 tmp_path에 직접 쓴다. **진짜 클립 파일(tools/blender/clips/*.py)에
의존하지 않는 이유**: 그 파일들은 각도를 굴리라고 있는 것이라 값이 계속 바뀐다.
테스트가 그 값을 붙들면 클립을 손볼 때마다 빨개진다 — 그건 검수 도구의 버그가 아니다.
다만 **두 진짜 파일이 여전히 파싱되는지**만 마지막에 한 번 확인한다(계약 검사).

clip_review_page.py는 tools/wesqa 옆(tools/)에 있어 wesqa 패키지가 아니다 —
conftest.py가 넣어 주는 건 tools/wesqa뿐이라 그 한 단계 위를 여기서 직접 잡는다.
"""
import io
import os
import re
import sys

import pytest
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS_DIR = os.path.dirname(os.path.dirname(HERE))  # tests -> wesqa -> tools
sys.path.insert(0, TOOLS_DIR)

from clip_review_page import (  # noqa: E402  (경로 세팅 뒤에 임포트)
    ClipReviewError,
    blender_frames_for,
    bone_comments,
    build_page,
    column_frames,
    embed_frames,
    module_imports_bpy_at_top_level,
    parse_clip,
    render_table,
)

REPO_ROOT = os.path.dirname(os.path.dirname(TOOLS_DIR))  # tools -> CTC -> repo root
REAL_CLIPS = os.path.join(REPO_ROOT, "tools", "blender", "clips")


# ---------- 픽스처 ----------

CLIP_A = '''"""SampleA - 표본 클립. 첫 줄이 페이지의 설명문이 된다."""
NAME = "SampleA"
FRAME_START = 1
FRAME_END = 11

BEATS = {3: "발이 가장 뒤", 7: "발사"}

POSES = {
    # 몸통은 **일어선다** - 음수가 젖힘이다.
    "body": [(1, 0.0), (3, -26.0), (7, -32.0), (11, 0.0)],
    "tail": [(1, 0.0), (3, -6.0), (11, 0.0)],
}


def build(_armature_object):
    import bpy
    bpy.foo()
'''

CLIP_B = '''"""SampleB - 다른 프레임에 키를 찍는 표본."""
NAME = "SampleB"
FRAME_START = 1
FRAME_END = 11
POSES = {
    "body": [(1, 0.0), (4, -16.0), (11, 0.0)],
}


def build(_armature_object):
    import bpy
    bpy.foo()
'''


def _write(tmp_path, name, text):
    p = tmp_path / name
    io.open(str(p), "w", encoding="utf-8").write(text)
    return str(p)


def _frames(tmp_path, stem, count, size=(32, 24)):
    """ClipPreviewRender 파일명 규약대로 합성 PNG를 깐다."""
    out = []
    for i in range(count):
        path = str(tmp_path / ("ctc-clip-preview-gif-%s-%02d-bf%02d.png" % (stem, i, i + 1)))
        Image.new("RGB", size, (10 * i, 40, 80)).save(path)
        out.append(path)
    return out


# ---------- 클립 파싱 ----------

def test_parse_clip_reads_bones_frames_and_values(tmp_path):
    clip = parse_clip(_write(tmp_path, "a.py", CLIP_A))
    assert clip["name"] == "SampleA"
    assert clip["frame_start"] == 1 and clip["frame_end"] == 11
    assert sorted(clip["bones"]) == ["body", "tail"]
    assert clip["bones"]["body"] == {1: 0.0, 3: -26.0, 7: -32.0, 11: 0.0}
    assert clip["bones"]["tail"] == {1: 0.0, 3: -6.0, 11: 0.0}
    # 한 클립 안에서도 뼈마다 키 프레임이 다르다 — frames는 그 합집합이다.
    assert clip["frames"] == [1, 3, 7, 11]
    assert clip["note"].startswith("SampleA -")


def test_parse_clip_without_poses_fails_with_a_readable_reason(tmp_path):
    path = _write(tmp_path, "nope.py", 'NAME = "Nope"\nFRAME_START = 1\nFRAME_END = 2\n')
    with pytest.raises(ClipReviewError) as e:
        parse_clip(path)
    assert "POSES" in str(e.value)


def test_parse_clip_with_malformed_pose_pairs_fails(tmp_path):
    path = _write(tmp_path, "bad.py", 'NAME = "Bad"\nPOSES = {"body": [1, 2, 3]}\n')
    with pytest.raises(ClipReviewError):
        parse_clip(path)


def test_top_level_bpy_import_is_detected_and_refused(tmp_path):
    """`import bpy`가 최상단에 있으면 importlib이 못 읽는다 — 시도하기 전에 잡아야
    ModuleNotFoundError 대신 고칠 방법이 적힌 메시지가 나온다."""
    assert module_imports_bpy_at_top_level("import bpy\nPOSES = {}\n") is True
    assert module_imports_bpy_at_top_level("from bpy.types import Object\n") is True
    assert module_imports_bpy_at_top_level("def build(o):\n    import bpy\n") is False

    path = _write(tmp_path, "top.py", "import bpy\nNAME='X'\nPOSES={'body':[(1,0.0)]}\n")
    with pytest.raises(ClipReviewError) as e:
        parse_clip(path)
    assert "bpy" in str(e.value)


def test_bone_comments_do_not_leak_from_the_previous_bone(tmp_path):
    """주석은 바로 위에 붙은 것만 가져간다. 앞 뼈의 주석이 뒤로 새면 「왜 이 각도인가」가
    엉뚱한 줄에 붙어 오히려 거짓 정보가 된다."""
    comments = bone_comments(CLIP_A)
    assert "일어선다" in comments["body"]
    assert comments["tail"] == ""


# ---------- 열 = 프레임 합집합 / 빈칸 ----------

def test_column_frames_is_the_sorted_union_across_clips(tmp_path):
    a = parse_clip(_write(tmp_path, "a.py", CLIP_A))
    b = parse_clip(_write(tmp_path, "b.py", CLIP_B))
    assert column_frames([a]) == [1, 3, 7, 11]
    assert column_frames([b]) == [1, 4, 11]
    assert column_frames([a, b]) == [1, 3, 4, 7, 11]


def test_missing_key_renders_a_blank_cell_not_a_zero(tmp_path):
    """0과 「키 없음」은 다른 사실이다. tail은 7프레임에 키가 없으므로 그 칸은 비어야 한다 —
    거기 0°가 찍히면 '7프레임에 0도로 눌러 놨다'는 **없는 사실**을 읽게 된다."""
    clip = parse_clip(_write(tmp_path, "a.py", CLIP_A))
    axis = column_frames([clip])
    table = render_table(clip, axis)

    rows = re.findall(r"<tr>.*?</tr>", table, re.DOTALL)
    tail_row = next(r for r in rows if ">tail<" in r)
    cells = re.findall(r'<td data-col="(\d+)"[^>]*>(.*?)</td>', tail_row, re.DOTALL)
    by_frame = {int(f): txt for f, txt in cells}

    assert by_frame[7] == "", "키가 없는 7프레임 칸에 뭔가 찍혔다: %r" % by_frame[7]
    assert 'data-col="7" class="none"' in tail_row
    # 같은 행의 진짜 0은 0°로 남아 있어야 한다 — 빈칸과 구별되는지가 이 테스트의 핵심이다.
    assert by_frame[1] == "0°"
    assert by_frame[3] == "-6°"


def test_a_clip_that_skips_a_frame_another_clip_uses_shows_blanks(tmp_path):
    """Shoot(4프레임 없음) 옆에 ShootDraw(4프레임 있음)를 놓는 실제 상황."""
    a = parse_clip(_write(tmp_path, "a.py", CLIP_A))
    b = parse_clip(_write(tmp_path, "b.py", CLIP_B))
    axis = column_frames([a, b])
    b_table = render_table(b, axis)
    assert 'data-col="3" class="none"' in b_table  # SampleB는 3프레임에 키가 없다
    assert 'data-col="4"' in b_table and ">-16°<" in b_table


# ---------- 프레임 → 블렌더 프레임 대응 ----------

def test_blender_frames_map_one_to_one_when_counts_match(tmp_path):
    clip = parse_clip(_write(tmp_path, "a.py", CLIP_A))
    assert blender_frames_for(clip, 11) == list(range(1, 12))


def test_blender_frames_scale_when_render_count_differs(tmp_path):
    """렌더 장수가 클립 길이와 안 맞아도 하이라이트가 엉뚱한 열을 짚지 않게 비율로 환산한다."""
    clip = parse_clip(_write(tmp_path, "a.py", CLIP_A))
    assert blender_frames_for(clip, 6) == [1, 3, 5, 7, 9, 11]


# ---------- 페이지 ----------

def _page_for(tmp_path, sources, counts):
    clips = [parse_clip(_write(tmp_path, "c%d.py" % i, s)) for i, s in enumerate(sources)]
    uris = [embed_frames(_frames(tmp_path, "C%d" % i, n), 0) for i, n in enumerate(counts)]
    return clips, build_page(clips, uris, 24.0, "테스트 검수")


def test_page_has_one_data_uri_per_frame(tmp_path):
    clips, page = _page_for(tmp_path, [CLIP_A, CLIP_B], [11, 5])
    uris = re.findall(r'"data:image/png;base64,[A-Za-z0-9+/=]+"', page)
    assert len(uris) == 16, "프레임 16장인데 data URI가 %d개다" % len(uris)
    # <img>는 클립당 하나이고 src는 스크립트가 갈아 끼운다 — 그림은 JSON 페이로드에 산다.
    assert page.count("<img ") == 2
    for clip, expected in zip(clips, [11, 5]):
        assert '<section class="clip" data-clip="%s"' % clip["name"] in page
        assert ">%d프레임" % expected in page


def test_page_frame_count_follows_the_frames_given(tmp_path):
    _, page = _page_for(tmp_path, [CLIP_A], [7])
    assert len(re.findall(r'"data:image/png;base64,', page)) == 7
    assert 'max="6"' in page  # 스크러버 범위 = 장수 - 1


def test_beats_present_and_absent_both_build(tmp_path):
    """BEATS는 선택 사항이다. 없으면 자막만 안 뜨고 페이지는 똑같이 성립해야 한다."""
    _, with_beats = _page_for(tmp_path, [CLIP_A], [11])
    assert "발이 가장 뒤" in with_beats
    assert '"3": "발이 가장 뒤"' in with_beats

    no_beats = CLIP_A.replace('BEATS = {3: "발이 가장 뒤", 7: "발사"}', "")
    clip = parse_clip(_write(tmp_path, "nb.py", no_beats))
    assert clip["beats"] == {}
    page = build_page(clip and [clip], [embed_frames(_frames(tmp_path, "NB", 11), 0)], 24.0)
    assert "발이 가장 뒤" not in page
    assert "<script" in page and 'data-beats>{}</script>' in page
    assert len(re.findall(r'"data:image/png;base64,', page)) == 11


def test_page_declares_utf8_before_anything_else(tmp_path):
    """혼자 돌아다니는 한글 페이지라 charset이 없으면 제목부터 깨진다 — 실제로 겪었다
    (「伊 ш꺽」). meta charset은 앞 1024바이트 안에 있어야 브라우저가 본다."""
    _, page = _page_for(tmp_path, [CLIP_A], [3])
    assert page.lstrip().startswith('<meta charset="utf-8">')
    assert page.encode("utf-8").index(b"charset") < 1024


def test_page_defines_every_colour_token_on_bare_root(tmp_path):
    """어두운 쪽 블록에서만 정의된 색이 있으면 시스템 기본값(스탬프 없음)에서 그 색이 빈다."""
    _, page = _page_for(tmp_path, [CLIP_A], [11])
    bare = page.split(":root{", 1)[1].split("}", 1)[0]
    bare_tokens = set(re.findall(r"(--[a-z-]+):", bare))
    for block_start in ['@media (prefers-color-scheme: dark)', ':root[data-theme="dark"]']:
        assert block_start in page
    dark_tokens = set(re.findall(r"(--[a-z-]+):", page.split(':root[data-theme="dark"]{', 1)[1]
                                 .split("}", 1)[0]))
    assert dark_tokens <= bare_tokens, "맨 :root에 없는 토큰: %s" % (dark_tokens - bare_tokens)
    assert "body{background:var(--paper)" in page


def test_mismatched_clip_and_frame_counts_fail(tmp_path):
    clip = parse_clip(_write(tmp_path, "a.py", CLIP_A))
    with pytest.raises(ClipReviewError):
        build_page([clip], [], 24.0)


def test_embed_frames_downscales_to_requested_width(tmp_path):
    paths = _frames(tmp_path, "S", 2, size=(64, 48))
    small = embed_frames(paths, 16)
    big = embed_frames(paths, 0)
    assert len(small[0]) < len(big[0])


# ---------- 진짜 클립 파일 계약 ----------

@pytest.mark.skipif(not os.path.isdir(REAL_CLIPS), reason="클립 원본 폴더가 없다")
def test_the_real_clip_files_still_parse():
    """값은 계속 굴러가므로 붙들지 않는다. **파싱이 되는지**와 1프레임·마지막 프레임이
    0으로 맞물리는지(원샷이 끝나고 이동으로 돌아갈 때 안 튀는 조건)만 본다."""
    found = 0
    for name in sorted(os.listdir(REAL_CLIPS)):
        if not name.endswith(".py") or name.startswith("_"):
            continue
        clip = parse_clip(os.path.join(REAL_CLIPS, name))
        found += 1
        assert clip["bones"], name
        assert clip["frames"][0] == clip["frame_start"], name
        assert clip["frames"][-1] == clip["frame_end"], name
        for bone, keys in clip["bones"].items():
            assert keys[clip["frame_start"]] == 0.0, "%s %s 첫 프레임이 0이 아니다" % (name, bone)
            assert keys[clip["frame_end"]] == 0.0, "%s %s 끝 프레임이 0이 아니다" % (name, bone)
    assert found >= 2, "클립 원본을 %d개만 찾았다" % found
