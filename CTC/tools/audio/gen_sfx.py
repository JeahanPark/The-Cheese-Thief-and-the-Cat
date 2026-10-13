"""효과음을 **한 줄 프롬프트**로 다시 뽑는다(2026-10-11 총소리 5차에서 얻은 규칙 — 사운드 프롬프트.md 끝 절).

  python gen_sfx.py gen <이름,...> [--takes 4]   → 사운드_후보/효과음_비교/<이름>_1~N.ogg
  python gen_sfx.py natural <이름,...>           → 길이 지정·자르기 없이(2차, 권장)
  python gen_sfx.py apply <이름> <번호>           → 옛 파일과 같은 세기(순간최대 LUFS)로 맞춰 덮어쓴다(.meta 유지)

후처리는 최소: 앞 무음 자르기 · 길이 컷 + 끝 페이드 · DC · 피크 -1dBFS. 세기는 **고른 뒤에만** 옛 파일에 맞춘다.
뜻(게임 기획서 §11-1): 벽감 파괴음 = 「달려와」(45m), 상자 파괴음 = 「조용히 먹어」(12m) — 둘이 겹치면 안 된다.
"""
import os, sys, shutil
import numpy as np
import importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("gg", os.path.join(HERE, "gen_guns.py"))
gg = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(gg)
fs = gg.fs; SR = gg.SR
OUT = os.path.join(fs.CAND, "효과음_비교")
RAW = os.path.join(os.environ.get("TEMP", HERE), "ctc-sfx")
AUDIO = os.path.join(fs.PROJ, "Assets", "Audio")

SOUNDS = {
    # 이름: (한 줄 프롬프트, 생성 길이, 최대 길이, 대상 파일)
    "벽감맞음":   ("hitting a brick wall", 0.5, 0.35, "Economy/sfx_alcove_hit.ogg"),
    "벽감부서짐": ("brick wall crumbling", 1.2, 1.2, "Economy/sfx_alcove_break.ogg"),
    "상자맞음":   ("hitting a wooden crate", 0.5, 0.30, "Economy/sfx_box_hit.ogg"),
    "상자부서짐": ("wooden crate breaking", 0.8, 0.8, "Economy/sfx_box_break.ogg"),   # 여는 게 아니라 HP 40을 때려 깬다(LootBox) — 'breaking open'은 길고 「열림」 쪽이었다
    "창고설치":   ("wooden box placed down", 0.6, 0.6, "Economy/sfx_decoy_place.ogg"),   # WarehousePlace가 이 파일을 쓴다
}


def natural_shape(path):
    """2차(2026-10-13): 길이를 강제하지 않는다 — 앞 무음과 -50dB 밑 꼬리만 걷고 DC·피크 -1dBFS. 자르기·페이드로 소리를 바꾸지 않는다."""
    x = fs.ffmpeg_decode(path); x = x - x.mean(); a = np.abs(x); pk = a.max()
    start = max(0, int(np.argmax(a > pk * 0.05)) - int(0.002 * SR))
    loud = np.where(a > pk * 10 ** (-50 / 20))[0]
    end = int(loud[-1]) + 1 if len(loud) else len(x)
    y = x[start:end].copy()
    fin = int(0.002 * SR); y[:fin] *= np.linspace(0, 1, fin)
    fout = min(int(0.01 * SR), len(y) // 4); y[-fout:] *= np.linspace(1, 0, fout)
    return y * (10 ** (-1 / 20) / (np.abs(y).max() + 1e-12))


def gen_natural(names, takes=4):
    """프롬프트 한 줄만 보낸다 — duration_seconds·prompt_influence 없이(모델 기본값). 1차는 길이를 강제하고 잘랐다."""
    import requests
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key: print("ELEVENLABS_API_KEY 환경변수가 없다"); sys.exit(2)
    os.makedirs(RAW, exist_ok=True); os.makedirs(OUT, exist_ok=True)
    for name in names:
        prompt, _, _, target = SOUNDS[name]
        for t in range(1, takes + 1):
            r = requests.post("https://api.elevenlabs.io/v1/sound-generation", params={"output_format": "mp3_44100_192"},
                              headers={"xi-api-key": key, "Content-Type": "application/json"},
                              json={"text": prompt, "model_id": "eleven_text_to_sound_v2"}, timeout=180)
            if r.status_code != 200: print(name, t, r.status_code, r.text[:200]); continue
            p = os.path.join(RAW, f"{name}_n{t}.mp3"); open(p, "wb").write(r.content)
            y = natural_shape(p)
            fs.ffmpeg_encode_ogg(y, os.path.join(OUT, f"{name}_{t}.ogg"))
            print(f"{name}_{t} ({prompt}): {len(y)/SR:.2f}s")


def gen(names, takes=4):
    import requests
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key: print("ELEVENLABS_API_KEY 환경변수가 없다"); sys.exit(2)
    os.makedirs(RAW, exist_ok=True); os.makedirs(OUT, exist_ok=True)
    for name in names:
        prompt, dur, max_len, target = SOUNDS[name]
        old = fs.ffmpeg_decode(os.path.join(AUDIO, target))
        shutil.copyfile(os.path.join(AUDIO, target), os.path.join(OUT, f"{name}_기존.ogg"))
        for t in range(1, takes + 1):
            r = requests.post("https://api.elevenlabs.io/v1/sound-generation", params={"output_format": "mp3_44100_192"},
                              headers={"xi-api-key": key, "Content-Type": "application/json"},
                              json={"text": prompt, "duration_seconds": dur, "model_id": "eleven_text_to_sound_v2"}, timeout=120)
            if r.status_code != 200: print(name, t, r.status_code, r.text[:200]); continue
            p = os.path.join(RAW, f"{name}{t}.mp3"); open(p, "wb").write(r.content)
            y = gg.simple_shape(p, dict(max_len=max_len))
            fs.ffmpeg_encode_ogg(y, os.path.join(OUT, f"{name}_{t}.ogg"))
        print(f"{name} ({prompt}): {takes}개 · 옛 파일 {len(old)/SR:.2f}s {gg.mmax(old):.1f} LUFS")


def apply(name, t):
    prompt, dur, max_len, target = SOUNDS[name]
    dst = os.path.join(AUDIO, target)
    ref = gg.mmax(fs.ffmpeg_decode(dst))
    y = gg.match_mmax(fs.ffmpeg_decode(os.path.join(OUT, f"{name}_{t}.ogg")), ref, max_limit_db=3.0)
    fs.ffmpeg_encode_ogg(y, dst)
    print(f"{name}_{t} → {target}  세기 {gg.mmax(y):.1f} LUFS(옛 {ref:.1f})")


if __name__ == "__main__":
    if sys.argv[1] == "gen":
        gen(sys.argv[2].split(","), int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 4)
    elif sys.argv[1] == "natural":
        gen_natural(sys.argv[2].split(","), int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 4)
    elif sys.argv[1] == "apply":
        apply(sys.argv[2], sys.argv[3])
