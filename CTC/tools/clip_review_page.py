# -*- coding: utf-8 -*-
"""ClipPreviewRender(유니티)가 뽑은 프레임 PNG + 클립 원본 .py를 **검수 페이지 한 장**으로 묶는다.

**왜 GIF로 부족한가**: GIF는 움직임은 보여 주지만 세우지를 못한다. 「5프레임 몸통이
-32도인 게 이 그림이 맞나」를 확인하려면 **그 프레임에서 멈추고 숫자를 봐야** 한다.
이 페이지는 재생·한 프레임씩 넘기기·스크러버·속도(6/12/24fps)에 더해, **지금 보고 있는
프레임의 열이 표에서 같이 하이라이트되는 포즈 각도 표**를 붙인다 — 눈에 보이는 포즈와
그 뒤의 숫자를 동시에 본다.

**표는 손으로 옮겨 적지 않는다.** `tools/blender/clips/*.py`가 각 클립의 원본이므로
그 파일을 그대로 읽어 표를 만든다. 손으로 베끼면 원본을 고친 다음부터 페이지가
**조용히 거짓말**을 한다 — 검수 도구가 거짓말을 하면 검수를 안 하느니만 못하다.

클립 파일은 `bpy`를 `build()` **안에서만** 임포트하므로(2026-09-12 두 파일 모두 AST로
확인) 블렌더 없이 평범한 importlib로 읽힌다 — 그래서 `POSES`가 상수 리터럴이 아니라
계산식이어도 그대로 통한다. 다만 그 가정을 믿지 않고 **매번 AST로 먼저 확인**하고,
최상단에 `bpy`가 있으면 import를 시도하지 않고 고치라는 말을 붙여 죽는다:
import 자체가 죽으면 나오는 `ModuleNotFoundError: bpy`는 원인을 안 알려 준다.
"""
from __future__ import annotations

import argparse
import ast
import base64
import glob as globlib
import html
import importlib.util
import io
import json
import os
import re
import sys

from PIL import Image

# 윈도우 콘솔 기본 인코딩이 cp949라, 한글 도움말에 든 em-dash 같은 글자에서 `--help`가
# UnicodeEncodeError로 죽는다(gif_from_frames.py가 먼저 당한 함정). 표준 출력을 UTF-8로.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except (AttributeError, ValueError):
        pass

# gif_from_frames.frame_index와 같은 규약: "'-'로 나눈 조각 중 순수 숫자인 마지막 조각"이
# 재생 순서다. 여기서 다시 정의하는 이유는 이 스크립트가 gif_from_frames에 의존하지
# 않게 하기 위해서다 — 둘은 같은 렌더 산출물을 읽을 뿐 서로를 부르지 않는다.
_DIGIT_TOKEN = re.compile(r"^\d+$")

# 클립 파일 주석에 쓰인 **강조**를 굵게 살린다. 이 주석들이 「왜 이 각도인가」를 담고
# 있어서 표 옆에 그대로 보여 줄 값어치가 있고, 강조가 죽으면 요점이 안 보인다.
_BOLD = re.compile(r"\*\*(.+?)\*\*", re.DOTALL)


class ClipReviewError(Exception):
    """클립 파싱·프레임 찾기 단계에서 사람이 읽을 이유를 달아 던지는 예외."""


# ──────────────────────────────────────────────────────────────────────────────
# 클립 원본(.py) 읽기
# ──────────────────────────────────────────────────────────────────────────────

def module_imports_bpy_at_top_level(_source: str) -> bool:
    """모듈 최상단에서 `bpy`를 임포트하는가. 그렇다면 블렌더 밖에서 import가 죽는다."""
    tree = ast.parse(_source)
    for node in tree.body:
        if isinstance(node, ast.Import):
            if any(a.name.split(".")[0] == "bpy" for a in node.names):
                return True
        elif isinstance(node, ast.ImportFrom):
            if (node.module or "").split(".")[0] == "bpy":
                return True
    return False


def bone_comments(_source: str) -> dict[str, str]:
    """POSES 딕셔너리에서 **각 뼈 키 바로 위에 붙은 주석 덩어리**를 뽑는다.

    주석은 AST에 안 남으므로 소스 줄을 직접 읽는다. 키의 줄 번호는 AST가 알려 주니
    거기서 위로 올라가며 `#`으로 시작하는 줄을 연속으로 모으고, 주석이 아닌 줄
    (`POSES = {`, 앞 항목의 이어지는 값 줄 등)을 만나면 멈춘다. 그래서 앞 뼈에 달린
    주석이 뒷 뼈로 새지 않고, 주석이 없는 뼈는 빈 문자열이 된다.
    """
    lines = _source.splitlines()
    tree = ast.parse(_source)

    poses_dict = None
    for node in ast.walk(tree):
        if isinstance(node, ast.Assign):
            for target in node.targets:
                if isinstance(target, ast.Name) and target.id == "POSES":
                    poses_dict = node.value
    if not isinstance(poses_dict, ast.Dict):
        return {}

    out: dict[str, str] = {}
    for key in poses_dict.keys:
        if not isinstance(key, ast.Constant) or not isinstance(key.value, str):
            continue
        collected: list[str] = []
        i = key.lineno - 2  # lineno는 1부터, 그 바로 윗줄부터 본다
        while i >= 0:
            stripped = lines[i].strip()
            if not stripped.startswith("#"):
                break
            collected.append(stripped.lstrip("#").strip())
            i -= 1
        collected.reverse()
        # 구분선용 박스 문자(────)만 있는 줄은 본문이 아니다 — 버린다.
        collected = [c for c in collected if c.strip("─-—_ ")]
        out[key.value] = " ".join(collected).strip()
    return out


def load_clip_module(_path: str):
    """클립 .py를 블렌더 없이 로드한다. importlib이 막히면 이유를 붙여 던진다."""
    with io.open(_path, "r", encoding="utf-8") as f:
        source = f.read()

    if module_imports_bpy_at_top_level(source):
        raise ClipReviewError(
            f"{_path}는 모듈 최상단에서 bpy를 임포트한다 — 블렌더 밖에서 실행할 수 없다. "
            "bpy 임포트를 build() 안으로 옮겨라(다른 클립 파일들이 그렇게 돼 있다).")

    name = "ctc_clip_" + os.path.splitext(os.path.basename(_path))[0]
    spec = importlib.util.spec_from_file_location(name, _path)
    if spec is None or spec.loader is None:
        raise ClipReviewError(f"클립 파일을 모듈로 못 읽었다: {_path}")
    module = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(module)
    except Exception as e:  # 클립 파일 자체의 문법·실행 오류
        raise ClipReviewError(f"{_path} 로드 실패: {type(e).__name__}: {e}") from e
    return module, source


def parse_clip(_path: str) -> dict:
    """클립 원본에서 검수에 필요한 것만 뽑아 평평한 dict로 돌려준다.

    돌려주는 것: name / frame_start / frame_end / bones(뼈→{프레임:각도}) /
    frames(키가 하나라도 찍힌 프레임의 합집합, 오름차순) / beats / comments / note.
    """
    module, source = load_clip_module(_path)

    poses = getattr(module, "POSES", None)
    if poses is None:
        raise ClipReviewError(
            f"{_path}에 POSES가 없다 — 클립 정의 파일이 아니거나 이름이 바뀌었다. "
            "각 클립 파일은 POSES = {뼈이름: [(프레임, 각도), ...]}를 최상단에 둬야 한다.")
    if not isinstance(poses, dict) or not poses:
        raise ClipReviewError(f"{_path}의 POSES가 비어 있거나 dict가 아니다: {type(poses).__name__}")

    # 키는 `(프레임, X)` 또는 `(프레임, X, Z)`다(2026-09-13 Fortress가 앞발을 옆으로 벌리려고
    # Z축을 처음 썼다). Z가 한 번이라도 나오면 그 뼈는 **행을 하나 더** 세운다 —
    # X 행에 섞어 적으면 「앞발 +20」이 벌림 80도를 숨긴 채 보인다.
    bones: dict[str, dict[int, float]] = {}
    for bone, keys in poses.items():
        x_row: dict[int, float] = {}
        z_row: dict[int, float] = {}
        uses_z = False
        try:
            for key in keys:
                if len(key) not in (2, 3):
                    raise ValueError(f"길이 {len(key)}")
                f = int(key[0])
                x_row[f] = float(key[1])
                z_row[f] = float(key[2]) if len(key) == 3 else 0.0
                uses_z = uses_z or len(key) == 3
        except (TypeError, ValueError) as e:
            raise ClipReviewError(
                f"{_path}의 '{bone}' 키 목록이 (프레임, X[, Z]) 튜플이 아니다: {e}") from e
        bones[str(bone)] = x_row
        if uses_z:
            bones[f"{bone} (벌림 Z)"] = z_row

    # **프레임 열은 합집합이다.** Shoot과 ShootDraw가 서로 다른 프레임에 키를 찍는 것처럼
    # (ShootDraw만 4프레임을 쓴다) 한 클립 안에서도 뼈마다 달라질 수 있다. 어느 뼈 하나가
    # 찍은 프레임은 표에 열로 선다 — 그 프레임에 키가 없는 뼈는 **빈칸**이 되고, 0과는
    # 다르게 보인다. 0은 「이 프레임에 0도로 찍었다」, 빈칸은 「여기엔 키가 없다」다.
    frames = sorted({f for keys in bones.values() for f in keys})
    if not frames:
        raise ClipReviewError(f"{_path}의 POSES에 키프레임이 하나도 없다")

    beats_raw = getattr(module, "BEATS", None) or {}
    beats = {int(k): str(v) for k, v in beats_raw.items()}

    doc = (module.__doc__ or "").strip()
    note = doc.splitlines()[0].strip() if doc else ""

    return {
        "name": str(getattr(module, "NAME", os.path.splitext(os.path.basename(_path))[0])),
        "frame_start": int(getattr(module, "FRAME_START", frames[0])),
        "frame_end": int(getattr(module, "FRAME_END", frames[-1])),
        "bones": bones,
        "frames": frames,
        "beats": beats,
        "comments": bone_comments(source),
        "note": note,
        "source": os.path.basename(_path),
    }


# ──────────────────────────────────────────────────────────────────────────────
# 프레임 PNG
# ──────────────────────────────────────────────────────────────────────────────

def frame_index(_path: str) -> int:
    """파일명에서 재생 순서 인덱스를 뽑는다(사전순이 아니라 숫자순이 목적)."""
    stem = os.path.splitext(os.path.basename(_path))[0]
    tokens = [t for t in stem.split("-") if _DIGIT_TOKEN.match(t)]
    if not tokens:
        raise ClipReviewError(f"파일명에서 숫자 인덱스를 못 찾았다: {_path}")
    return int(tokens[-1])


def find_frames(_pattern: str) -> list[str]:
    """패턴에 맞는 프레임을 숫자 인덱스 순으로. 2장 미만이면 움직임이 성립하지 않는다."""
    paths = globlib.glob(_pattern)
    if len(paths) < 2:
        raise ClipReviewError(
            f"프레임을 2장도 못 찾았다 (패턴: {_pattern}, 찾은 개수: {len(paths)})")
    return sorted(paths, key=frame_index)


def embed_frames(_paths: list[str], _size: int) -> list[str]:
    """프레임을 base64 data URI 목록으로. 페이지 한 장으로 들고 다니려면 외부 파일이
    있으면 안 된다 — 폴더째 옮기다 그림만 빠진 페이지가 나온다.

    축소는 LANCZOS를 쓴다. gif_from_frames는 정수배 **확대**라 NEAREST가 맞지만,
    768→512 같은 비정수배 **축소**에서 NEAREST는 실루엣 가장자리를 톱니로 만든다 —
    이 검수의 판단 대상이 바로 그 실루엣이다.
    """
    uris = []
    for path in _paths:
        if _size and _size > 0:
            with Image.open(path) as im:
                if im.width != _size:
                    im = im.resize((_size, round(im.height * _size / im.width)), Image.LANCZOS)
                buf = io.BytesIO()
                im.save(buf, format="PNG", optimize=True)
                data = buf.getvalue()
        else:
            with io.open(path, "rb") as f:
                data = f.read()
        uris.append("data:image/png;base64," + base64.b64encode(data).decode("ascii"))
    return uris


def blender_frames_for(_clip: dict, _count: int) -> list[int]:
    """i번째 렌더 이미지가 블렌더 몇 프레임인지.

    ClipPreviewRender는 클립 전체를 균등 분할해 굽는다(DenseNormalizedTimes). 보통
    장수 = FRAME_END - FRAME_START + 1로 딱 맞지만, 어긋나도 표 하이라이트가 엉뚱한
    열을 짚지 않도록 **비율로 환산**한다 — 조용히 1:1이라고 가정하지 않는다.
    """
    start, end = _clip["frame_start"], _clip["frame_end"]
    if _count <= 1:
        return [start]
    return [start + round(i * (end - start) / (_count - 1)) for i in range(_count)]


# ──────────────────────────────────────────────────────────────────────────────
# HTML
# ──────────────────────────────────────────────────────────────────────────────

def _rich(_text: str) -> str:
    """주석 한 줄을 HTML로. 먼저 전부 이스케이프한 뒤 **강조**만 <b>로 되살린다 —
    순서를 바꾸면 주석에 든 '<'가 태그로 읽힌다."""
    return _BOLD.sub(r"<b>\1</b>", html.escape(_text))


def _fmt_angle(_v: float) -> str:
    if _v == 0:
        return "0°"
    return ("%+g°" % _v)


def column_frames(_clips: list[dict]) -> list[int]:
    """표의 가로축 — 페이지에 올린 **모든 클립의 모든 뼈**가 찍은 프레임의 합집합.

    왜 클립별로 따로 세우지 않는가: 이 도구의 본래 쓸모가 Shoot과 ShootDraw를 나란히
    놓고 **발사 포즈가 같은지** 보는 것이라, 두 표의 열이 어긋나면 눈으로 비교가 안 된다.
    축을 공유하면 ShootDraw만 쓰는 4프레임 열이 Shoot 쪽에선 통째로 빈칸이 되는데,
    그건 감춰야 할 흠이 아니라 **「Shoot은 4프레임에 키가 없다」는 사실 그 자체**다.

    같은 코드가 한 클립 안에서 뼈마다 키 프레임이 다른 경우도 그대로 처리한다 —
    그 뼈의 그 칸만 빈칸이 된다.
    """
    return sorted({f for c in _clips for f in c["frames"]})


def render_table(_clip: dict, _frames: list[int]) -> str:
    """뼈×프레임 표. 키가 없는 칸은 **빈칸**이다(0이 아니다) —
    0은 「이 프레임에 0도로 찍었다」, 빈칸은 「여기엔 키가 없다」로 서로 다른 사실이다."""
    frames = _frames
    cols = "".join('<th data-col="%d" scope="col">%d</th>' % (f, f) for f in frames)

    rows = []
    for bone, keys in _clip["bones"].items():
        cells = []
        for f in frames:
            if f not in keys:
                # class="none"만 두고 내용은 비운다. 0과 결코 같아 보이면 안 된다.
                cells.append('<td data-col="%d" class="none" title="이 프레임엔 키가 없다"></td>' % f)
                continue
            v = keys[f]
            cls = "zero" if v == 0 else ("neg" if v < 0 else "pos")
            cells.append('<td data-col="%d" class="%s">%s</td>' % (f, cls, _fmt_angle(v)))
        comment = _clip["comments"].get(bone, "")
        why = ('<p class="why">%s</p>' % _rich(comment)) if comment else ""
        rows.append(
            '<tr><th scope="row"><span class="bone">%s</span>%s</th>%s</tr>'
            % (html.escape(bone), why, "".join(cells)))

    return (
        '<div class="tablewrap"><table>'
        '<thead><tr><th scope="col">뼈 / 프레임</th>%s</tr></thead>'
        '<tbody>%s</tbody></table></div>' % (cols, "".join(rows)))


def render_clip_section(_clip: dict, _uris: list[str], _frames: list[int]) -> str:
    bfs = blender_frames_for(_clip, len(_uris))
    keyed = len(_clip["frames"])
    beats_json = json.dumps({str(k): v for k, v in _clip["beats"].items()}, ensure_ascii=False)
    payload = json.dumps({"src": _uris, "bf": bfs}, ensure_ascii=False)

    return """<section class="clip" data-clip="{name}">
  <hgroup>
    <h2>{name} <span>{count}프레임 · 키 {keyed}열</span></h2>
    <p class="note">{note}</p>
  </hgroup>
  <div class="stage">
    <img alt="{name} 프레임" data-img>
    <div class="badge" data-badge></div>
    <div class="beat" data-beat></div>
  </div>
  <div class="transport">
    <button class="play" data-play aria-pressed="false">재생</button>
    <button data-step="-1" aria-label="이전 프레임">◀</button>
    <button data-step="1" aria-label="다음 프레임">▶</button>
    <div class="speeds" role="group" aria-label="재생 속도">
      <button data-fps="24">24</button><button data-fps="12">12</button><button data-fps="6">6</button>
    </div>
    <input type="range" min="0" max="{max}" value="0" data-scrub aria-label="프레임">
  </div>
  {table}
  <script type="application/json" data-beats>{beats}</script>
  <script type="application/json" data-payload>{payload}</script>
</section>
""".format(name=html.escape(_clip["name"]), count=len(_uris), keyed=keyed,
           note=_rich(_clip["note"]), max=len(_uris) - 1,
           table=render_table(_clip, _frames), beats=beats_json, payload=payload)


_STYLE = """<style>
/* 팔레트는 쥐 자신의 색에서 뽑았다 — 청록 몸통, 산호 꼬리, 숯빛 무대.
   **모든 토큰은 맨 :root에 전부 정의한다.** 어두운 쪽에서는 같은 토큰만 다시 쓴다 —
   미디어 쿼리 안에서만 존재하는 색이 하나라도 있으면 시스템 기본값에서 그 색이 빈다. */
:root{
  --teal:#2F6E75; --teal-dim:#24565C; --coral:#C2564B;
  --ink:#12191B; --muted:#5C6B6E; --line:#D5DEDE; --paper:#F4F7F6; --card:#FFFFFF;
  --stage:#3A3A40; --onstage:#FFFFFF; --scrim:rgba(0,0,0,.55);
}
@media (prefers-color-scheme: dark){
  :root:not([data-theme="light"]){
    --teal:#5FB3BC; --teal-dim:#8FD2D9; --coral:#E88C82;
    --ink:#E8EFEF; --muted:#93A4A7; --line:#26363A; --paper:#0D1416; --card:#131F22;
    --stage:#22282B; --onstage:#FFFFFF; --scrim:rgba(0,0,0,.6);
  }
}
:root[data-theme="dark"]{
  --teal:#5FB3BC; --teal-dim:#8FD2D9; --coral:#E88C82;
  --ink:#E8EFEF; --muted:#93A4A7; --line:#26363A; --paper:#0D1416; --card:#131F22;
  --stage:#22282B; --onstage:#FFFFFF; --scrim:rgba(0,0,0,.6);
}
*{box-sizing:border-box}
html{color-scheme:light dark}
body{background:var(--paper);color:var(--ink);margin:0;
  font:400 16px/1.6 "Source Sans 3",system-ui,-apple-system,"Segoe UI",sans-serif;
  padding:40px 24px 72px}
.wrap{max-width:1180px;margin:0 auto;display:flex;flex-direction:column;gap:36px}
header{display:flex;flex-direction:column;gap:10px;border-bottom:2px solid var(--teal);padding-bottom:18px}
.titlerow{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;flex-wrap:wrap}
h1{font:700 34px/1.15 Archivo,system-ui,sans-serif;margin:0;letter-spacing:-.015em;text-wrap:balance}
.sub{color:var(--muted);max-width:64ch;margin:0}
.meta{display:flex;flex-wrap:wrap;gap:8px;margin-top:4px}
.chip{font:600 11.5px/1 "IBM Plex Mono",ui-monospace,monospace;letter-spacing:.06em;
  text-transform:uppercase;padding:6px 9px;border:1px solid var(--line);border-radius:3px;color:var(--muted)}
.chip b{color:var(--teal)}
.clips{display:flex;flex-direction:column;gap:28px}
.clip{background:var(--card);border:1px solid var(--line);border-radius:6px;overflow:hidden;
  display:flex;flex-direction:column}
.clip hgroup{padding:16px 18px 12px;border-bottom:1px solid var(--line);display:flex;flex-direction:column;gap:6px}
.clip h2{font:700 20px/1.2 Archivo,sans-serif;margin:0;display:flex;align-items:baseline;gap:10px;flex-wrap:wrap}
.clip h2 span{font:600 11px/1 "IBM Plex Mono",ui-monospace,monospace;color:var(--muted);letter-spacing:.06em}
.note{font-size:14px;color:var(--muted);margin:0}
.note b,.why b{color:var(--ink);font-weight:600}
.stage{background:var(--stage);position:relative;aspect-ratio:16/9;display:grid;place-items:center}
.stage img{width:auto;height:100%;max-width:100%;object-fit:contain;display:block}
.badge{position:absolute;left:12px;top:12px;font:600 12px/1 "IBM Plex Mono",ui-monospace,monospace;
  background:var(--scrim);color:var(--onstage);padding:6px 8px;border-radius:3px;letter-spacing:.04em}
.beat{position:absolute;left:12px;bottom:12px;right:12px;font:600 14px/1.35 "Source Sans 3",sans-serif;
  color:var(--onstage);text-shadow:0 1px 3px rgba(0,0,0,.85);text-align:center}
.transport{display:flex;align-items:center;gap:10px;padding:12px 16px;border-top:1px solid var(--line);flex-wrap:wrap}
button{font:600 13px/1 "Source Sans 3",sans-serif;color:var(--ink);background:transparent;
  border:1px solid var(--line);border-radius:4px;padding:8px 12px;cursor:pointer}
button:hover{border-color:var(--teal);color:var(--teal)}
button:focus-visible{outline:2px solid var(--teal);outline-offset:2px}
button.play{background:var(--teal);border-color:var(--teal);color:var(--onstage);min-width:74px}
button.play:hover{background:var(--teal-dim);border-color:var(--teal-dim);color:var(--onstage)}
.speeds{display:flex;border:1px solid var(--line);border-radius:4px;overflow:hidden}
.speeds button{border:0;border-radius:0;padding:8px 10px;font-family:"IBM Plex Mono",ui-monospace,monospace;font-size:12px}
.speeds button+button{border-left:1px solid var(--line)}
.speeds button[aria-pressed="true"]{background:var(--teal);color:var(--onstage)}
input[type=range]{flex:1;min-width:110px;accent-color:var(--teal)}
.tablewrap{overflow-x:auto;border-top:1px solid var(--line)}
table{width:100%;border-collapse:collapse;font:400 13px/1.4 "IBM Plex Mono",ui-monospace,monospace;
  font-variant-numeric:tabular-nums}
th,td{padding:7px 10px;text-align:right;white-space:nowrap;border-bottom:1px solid var(--line);vertical-align:top}
th:first-child,td:first-child{text-align:left}
thead th{font-weight:600;color:var(--muted);font-size:11.5px;letter-spacing:.05em;position:sticky;top:0;background:var(--card)}
tbody th{font-weight:400;min-width:260px;max-width:420px;white-space:normal}
tbody th .bone{color:var(--ink);font-weight:600}
.why{margin:4px 0 0;font:400 12px/1.45 "Source Sans 3",sans-serif;color:var(--muted);white-space:normal}
td.cur,th.cur{background:color-mix(in srgb,var(--teal) 15%,transparent)}
th.cur{color:var(--teal)}
td.cur{font-weight:600}
.neg{color:var(--teal)} .pos{color:var(--coral)} .zero{color:var(--muted)}
/* 키가 없는 칸 — 0과 다르게 보여야 한다. 옅은 사선 한 줄로 「여기엔 아무것도 없다」를 표시한다. */
.none{background:repeating-linear-gradient(135deg,transparent 0 5px,var(--line) 5px 6px)}
footer{color:var(--muted);font-size:14px;border-top:1px solid var(--line);padding-top:16px;
  display:flex;flex-direction:column;gap:8px}
footer p{margin:0;max-width:76ch}
code{font-family:"IBM Plex Mono",ui-monospace,monospace;font-size:13px;color:var(--ink)}
</style>"""

_SCRIPT = """<script>
// 테마 토글 — 로컬 파일에는 호스트가 없어서 data-theme을 아무도 안 찍는다.
// 세 상태(밝음·어두움·시스템)를 실제로 눈으로 확인하려면 이 버튼이 필요하다.
document.querySelector('[data-theme-toggle]').addEventListener('click', function(){
  var r = document.documentElement;
  var next = {'':'light', 'light':'dark', 'dark':''}[r.getAttribute('data-theme') || ''];
  if (next) { r.setAttribute('data-theme', next); } else { r.removeAttribute('data-theme'); }
  this.textContent = '테마: ' + (next || '시스템');
});

document.querySelectorAll('.clip').forEach(function(sec){
  var payload = JSON.parse(sec.querySelector('[data-payload]').textContent);
  var beats = JSON.parse(sec.querySelector('[data-beats]').textContent);
  var imgs = payload.src, bfs = payload.bf;
  var img = sec.querySelector('[data-img]');
  var badge = sec.querySelector('[data-badge]');
  var beatEl = sec.querySelector('[data-beat]');
  var scrub = sec.querySelector('[data-scrub]');
  var playBtn = sec.querySelector('[data-play]');

  // 전부 미리 디코드해 둔다 — 첫 바퀴에서 이미지가 늦게 뜨면 그게 「타이밍이 이상하다」로
  // 오독된다. data URI라 네트워크는 없지만 디코드 비용은 있다.
  imgs.forEach(function(src){ var i = new Image(); i.src = src; });

  var idx = 0, fps = 6, timer = null;

  function paint(){
    img.src = imgs[idx];
    var bf = bfs[idx];
    badge.textContent = 'bf' + String(bf).padStart(2, '0');
    beatEl.textContent = beats[String(bf)] || '';
    scrub.value = idx;
    sec.querySelectorAll('[data-col]').forEach(function(c){
      c.classList.toggle('cur', Number(c.dataset.col) === bf);
    });
  }
  function tick(){ idx = (idx + 1) % imgs.length; paint(); }
  function start(){
    if (timer) clearInterval(timer);
    timer = setInterval(tick, 1000 / fps);
    playBtn.textContent = '일시정지';
    playBtn.setAttribute('aria-pressed', 'true');
  }
  function stop(){
    if (timer) { clearInterval(timer); timer = null; }
    playBtn.textContent = '재생';
    playBtn.setAttribute('aria-pressed', 'false');
  }

  playBtn.addEventListener('click', function(){ timer ? stop() : start(); });
  sec.querySelectorAll('[data-step]').forEach(function(b){
    b.addEventListener('click', function(){
      stop();
      idx = (idx + Number(b.dataset.step) + imgs.length) % imgs.length;
      paint();
    });
  });
  sec.querySelectorAll('[data-fps]').forEach(function(b){
    b.addEventListener('click', function(){
      fps = Number(b.dataset.fps);
      sec.querySelectorAll('[data-fps]').forEach(function(o){
        o.setAttribute('aria-pressed', String(o === b));
      });
      if (timer) start();
    });
  });
  scrub.addEventListener('input', function(){ stop(); idx = Number(scrub.value); paint(); });

  paint();
  if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) start();
});
</script>"""


def build_page(_clips: list[dict], _frame_uris: list[list[str]], _fps: float,
               _title: str = "클립 검수") -> str:
    """클립 여러 개를 한 페이지에 세운다. Shoot과 ShootDraw처럼 **발사 포즈가 같아야 하는**
    클립을 나란히 놓고 비교하는 게 이 도구의 본래 쓸모다."""
    if len(_clips) != len(_frame_uris):
        raise ClipReviewError(
            f"클립 {len(_clips)}개에 프레임 묶음 {len(_frame_uris)}개 — 짝이 안 맞는다")

    total_frames = sum(len(u) for u in _frame_uris)
    bone_names = sorted({b for c in _clips for b in c["bones"]})
    seconds = max((c["frame_end"] - c["frame_start"]) for c in _clips) / _fps

    chips = "".join([
        '<span class="chip">클립 <b>%d</b></span>' % len(_clips),
        '<span class="chip">프레임 <b>%d</b></span>' % total_frames,
        '<span class="chip">움직이는 뼈 <b>%d</b></span>' % len(bone_names),
        '<span class="chip"><b>%g</b> fps</span>' % _fps,
        '<span class="chip">길이 <b>%.2f</b>s</span>' % seconds,
    ])

    sources = " · ".join("<code>%s</code>" % html.escape(c["source"]) for c in _clips)
    axis = column_frames(_clips)
    sections = "".join(render_clip_section(c, u, axis) for c, u in zip(_clips, _frame_uris))

    # **charset을 맨 앞에 박는다.** 이 페이지는 통째로 한글이고 혼자 돌아다니는 파일이라
    # (임시 폴더에 떨어져 브라우저로 열린다) 브라우저가 UTF-8을 알아서 맞혀 주길 기대할
    # 수 없다 — 실제로 없이 열었더니 제목부터 「伊� �ш꺽」로 깨졌다(2026-09-12 실측).
    return """<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{title}</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Archivo:wght@500;700&family=IBM+Plex+Mono:wght@400;600&family=Source+Sans+3:wght@400;600&display=swap">
{style}
<div class="wrap">
<header>
  <div class="titlerow">
    <h1>{title}</h1>
    <button data-theme-toggle>테마: 시스템</button>
  </div>
  <p class="sub">정지 화면으로는 박자를 못 본다 — 재생하고, 멈추고, 한 프레임씩 넘긴다.
  표의 열은 지금 보고 있는 프레임을 따라 같이 하이라이트된다. 각도는 뼈 로컬 X축이다.</p>
  <div class="meta">{chips}</div>
</header>
<div class="clips">
{sections}</div>
<footer>
  <p><b>음수 = 뒤로 젖힘</b>(일어섬), <b>양수 = 앞으로</b>. <code>root</code>와 뒷다리는 건드리지 않는다 — 움직이면 발이 미끄러져 보인다.</p>
  <p>사선으로 그은 칸은 <b>그 프레임에 키가 없다</b>는 뜻이다 — <code>0°</code>(0도로 찍었다)와 다른 사실이다.</p>
  <p>표와 주석은 클립 원본에서 그대로 읽었다: {sources}. 페이지를 손으로 고치지 말고 원본을 고친 뒤 다시 뽑는다.</p>
</footer>
</div>
{script}
""".format(title=html.escape(_title), style=_STYLE, chips=chips,
           sections=sections, sources=sources, script=_SCRIPT)


# ──────────────────────────────────────────────────────────────────────────────
# CLI
# ──────────────────────────────────────────────────────────────────────────────

def resolve_frame_patterns(_defs: list[str], _frames: list[str] | None,
                           _dir: str | None, _clips: list[dict]) -> list[str]:
    """클립 정의마다 프레임 글롭 하나를 짝지어 준다.

    --frames를 준 경우 **순서대로** 짝짓는다(개수가 다르면 에러 — 조용히 남는 쪽을
    버리면 엉뚱한 클립의 그림이 붙은 페이지가 나온다). 안 줬으면 --dir + 클립명으로 만든다.
    """
    if _frames:
        if len(_frames) != len(_defs):
            raise ClipReviewError(
                f"--clip-def {len(_defs)}개와 --frames {len(_frames)}개의 수가 다르다 — "
                "순서대로 짝지으므로 같은 수여야 한다")
        return list(_frames)
    if _dir:
        return [os.path.join(_dir, "*%s*.png" % c["name"]) for c in _clips]
    raise ClipReviewError("--frames(정의마다 하나), 또는 --dir 중 하나가 필요하다")


def parse_args(_argv: list[str] | None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--clip-def", dest="clip_defs", action="append", required=True,
                   help="클립 원본 .py 경로 (여러 번 주면 한 페이지에 나란히 올린다)")
    p.add_argument("--frames", action="append",
                   help="프레임 PNG 글롭 — --clip-def와 같은 순서로 하나씩")
    p.add_argument("--dir", help="프레임 디렉터리 — --frames 대신, 클립명으로 글롭을 만든다")
    p.add_argument("--out", required=True, help="출력 HTML 경로")
    p.add_argument("--fps", type=float, default=24.0, help="클립 프레임레이트 (기본 24)")
    p.add_argument("--size", type=int, default=512,
                   help="프레임 가로 픽셀로 축소 (기본 512, 0=원본 — 768 원본 두 클립이면 "
                        "512에서 페이지가 1.1MB 정도다)")
    p.add_argument("--title", default="클립 검수", help="페이지 제목")
    return p.parse_args(_argv)


def main(_argv: list[str] | None = None) -> int:
    args = parse_args(_argv)
    try:
        clips = [parse_clip(d) for d in args.clip_defs]
        patterns = resolve_frame_patterns(args.clip_defs, args.frames, args.dir, clips)
        frame_sets = [find_frames(p) for p in patterns]
        uris = [embed_frames(fs, args.size) for fs in frame_sets]
        page = build_page(clips, uris, args.fps, args.title)
    except ClipReviewError as e:
        print(f"[clip_review_page] 에러: {e}", file=sys.stderr)
        return 1

    out_dir = os.path.dirname(os.path.abspath(args.out))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir, exist_ok=True)
    with io.open(args.out, "w", encoding="utf-8") as f:
        f.write(page)

    summary = ", ".join("%s %d장" % (c["name"], len(u)) for c, u in zip(clips, uris))
    print("[clip_review_page] 완료: %s (%s, %.1fMB)"
          % (args.out, summary, os.path.getsize(args.out) / 1048576.0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
