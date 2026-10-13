"""쥐 총 발사음 3종 — 2차(폴리) 프롬프트.

쥐 총 발사음 3종(딱총·새총·기관총 한 발)을 다시 뽑는다 — 2026-10-11 사용자 「이상하다」.

계측한 원인(옛 파일):
  딱총   에너지 53%가 150Hz 밑(「붕」) + 29%가 4kHz 위 쉿소리 — 코르크 「뽕」의 몸통(150Hz~4kHz)은 18%뿐
  새총   에너지 95%가 150Hz 밑 — 고무줄 「팅」이 아니라 둔한 쿵
  기관총 79%가 4kHz 위, 0~120ms 내내 평평한 소음 뒤 130ms에서 칼같이 끊김 — 총소리가 아니라 「치익」
설계: 무기 티어(딱총 0.8s · 새총 0.8s · 기관총 0.15s 연사), 전부 3D 20m 직선 감쇠, 고양이 봇도 듣는다.
      쥐가 든 **장난감** 총이라 만화 톤은 맞다(발소리와 다르다). 단 'tiny'·'mouse'는 쓰지 않는다(쨍한 쇳소리).

  python gen_guns.py gen [--takes 4] [--only 딱총,기관총]   → 사운드_후보/총_비교/<무기>_A·B.ogg(한 발) + <무기>_A·B_연사.ogg(게임 박자)
  python gen_guns.py repick              → 받아 둔 원본으로 다시 처리·고르기(크레딧 0)
  python gen_guns.py apply <무기> <A|B>  → Assets/Audio/Combat/sfx_<id>_fire.ogg (.meta 유지)
"""
import os, sys, math, re, shutil, subprocess
import numpy as np
import importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("fs", os.path.join(HERE, "gen_footsteps.py"))
fs = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(fs)
SR = fs.SR
OUT = os.path.join(fs.CAND, "총_비교")
RAW = os.path.join(os.environ.get("TEMP", HERE), "ctc-guns")
TARGET = os.path.join(fs.PROJ, "Assets", "Audio", "Combat")

# 1차(2026-10-11)는 'Cartoon … comedic toy style' + 'springy twang'이었다 → 사용자 「다 물총 같고 띠용띠용 거린다」.
# 발소리 때와 같은 병이다 — 만화·장난감 어휘가 「띠용」(음정이 휘는 울림)을 부른다. 실제 물건의 폴리로 쓰고 띠용을 명시적으로 막는다.
PREFIX = "Foley sound effect, single one-shot, no music. "
NO_BOING = " Realistic, punchy, close-mic, dry, no echo, no boing, no spring, no wobble, no water."
WEAPONS = {
    # 3차(2026-10-11): 사용자 「좀 더 살짝 리얼하게」 — 2차 코르크·에어소프트는 탈락, 새총 B(사실적 탁 + 바람)만 채택.
    "딱총": dict(id="popgun", hp=120, lim=8.0, gen=0.5, max_len=0.40, interval=0.8, mmax=-15.0, want=(150, 4000),
               prompt=PREFIX + "A single shot from a small air pistol: a sharp compact crack-pop with a quick puff of compressed air "
                               "and a faint mechanical click. Small and light but realistic." + NO_BOING),
    "새총": dict(id="slingshot", hp=200, lim=6.0, gen=0.6, max_len=0.45, interval=0.8, mmax=-16.0, want=(300, 6000),
               prompt=PREFIX + "A real slingshot firing: a sharp snap of a thick rubber band being released, "
                               "followed by a fast short whoosh of a stone." + NO_BOING + " No twang."),
    "기관총": dict(id="machinegun", hp=150, lim=10.0, gen=0.5, max_len=0.13, interval=0.15, mmax=-16.0, want=(300, 5000),
                prompt=PREFIX + "One single shot from a compact submachine gun, small and tight: a short sharp crack with a little "
                                "mechanical clack of the bolt, very short." + NO_BOING + " No burst, only one shot."),
}


def mmax(y):
    """순간 라우드니스 최대(400ms, LUFS) — 0.1~0.4초 효과음 세기 비교용."""
    r = subprocess.run(["ffmpeg", "-v", "info", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                        "-af", "apad=pad_dur=1,ebur128", "-f", "null", "-"],
                       input=y.astype(np.float32).tobytes(), capture_output=True)
    return max(float(m) for m in re.findall(r"M:\s*(-?[\d.]+)", r.stderr.decode(errors="ignore")))


def share(y, lo, hi):
    n = 16384
    S = np.abs(np.fft.rfft(y * np.hanning(len(y)), n)) ** 2
    f = np.fft.rfftfreq(n, 1 / SR)
    return S[(f >= lo) & (f < hi)].sum() / (S[f > 20].sum() + 1e-12)


def match_mmax(y, target, max_limit_db=6.0):
    cap = 10 ** (-1 / 20); pushed = 0.0
    for _ in range(6):
        gain = target - mmax(y)
        if abs(gain) < 0.3: break
        if gain > 0: gain = min(gain, max(0.0, max_limit_db - pushed))
        if gain == 0: break
        y = y * 10 ** (gain / 20)
        if np.abs(y).max() > cap:
            pushed += 20 * math.log10(np.abs(y).max() / cap); y = fs.limit(y, cap)
    return y


def shape(path, w):
    """첫 어택부터 max_len까지 — 끝 15ms(기관총)·30ms 페이드로 칼 끊김을 없앤다. DC·45Hz·페이드인은 fs.post."""
    x = fs.ffmpeg_decode(path)
    on, _ = fs.find_onsets(x, count=1)
    old = fs.MAX_LEN; fs.MAX_LEN = w["max_len"]
    y = fs.cut_step(x, on[0], None)
    fs.MAX_LEN = old
    y = y[:int(w["max_len"] * SR)].copy()
    fade = int((0.015 if w["max_len"] < 0.2 else 0.03) * SR)
    if len(y) > fade: y[-fade:] *= np.linspace(1, 0, fade) ** 2
    # 폴리 프롬프트는 'punchy'에 저음 쿵을 얹는다(2차: 새총 45~87%가 150Hz 밑). 쥐가 든 작은 총에 큰 저음은 없다 —
    # 무기별 하이패스로 「붕」을 걷는다(45Hz 공통 HPF로는 모자랐다).
    y = fs.biquad_hpf(fs.biquad_hpf(y, w["hp"]), w["hp"])
    y = fs.post(y)
    return match_mmax(y, w["mmax"], w["lim"])   # 기관총 0.13s는 피크 대비 몸통이 작아 리미팅을 더 허용


def flatness(y):
    """어택 뒤(20~150ms)의 스펙트럼 평탄도. 낮으면 음정이 있는 울림 = 「띠용」. 총·딱 소리는 잡음성(높음)이어야 한다."""
    seg = y[int(0.02 * SR):int(0.15 * SR)]
    if len(seg) < 512: seg = y
    S = np.abs(np.fft.rfft(seg * np.hanning(len(seg)), 8192)); f = np.fft.rfftfreq(8192, 1 / SR)
    S = S[(f > 200) & (f < 8000)] + 1e-9
    return float(np.exp(np.mean(np.log(S))) / np.mean(S))


def score(y, w):
    """원하는 대역 몫이 크고, 150Hz 밑(붕)·6kHz 위(쉿)가 작고, 울림이 음정 없이 잡음성일수록 좋다."""
    s = share(y, *w["want"]) - 0.7 * share(y, 20, 150) - 0.5 * share(y, 6000, 22050) + 1.5 * flatness(y)
    e = np.abs(y); attack = np.argmax(e > e.max() * 0.5) / SR
    if w["id"] == "machinegun": s -= attack * 10
    s -= 0.4 * max(0, attacks(y) - 1)          # 한 발에 어택이 여럿 = 여러 발로 들린다
    return s


def attacks(y):
    e = fs.envelope(y, 3.0); db = 20 * np.log10(e / (e.max() + 1e-12) + 1e-9)
    lag = int(0.02 * SR); rise = db[lag:] - db[:-lag]; n = 0; i = 0
    while i < len(rise):
        if rise[i] > 10 and db[i + lag] > -20: n += 1; i += int(0.08 * SR)
        else: i += 1
    return max(1, n)


def gen(takes=4, only=None):
    import requests
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key: print("ELEVENLABS_API_KEY 환경변수가 없다"); sys.exit(2)
    os.makedirs(RAW, exist_ok=True); os.makedirs(OUT, exist_ok=True)
    for name, w in WEAPONS.items():
        if only and name not in only: continue
        assert len(w["prompt"]) <= 450, (name, len(w["prompt"]))
        # 지난 차수 원본은 old/로 — repick이 탈락한 테이크를 다시 고르지 않게
        old = os.path.join(RAW, "old"); os.makedirs(old, exist_ok=True)
        for f in os.listdir(RAW):
            if f.startswith(w["id"]) and f.endswith(".mp3"): shutil.move(os.path.join(RAW, f), os.path.join(old, f"{len(os.listdir(old))}_{f}"))
        got = []
        for t in range(1, takes + 1):
            r = requests.post("https://api.elevenlabs.io/v1/sound-generation", params={"output_format": "mp3_44100_192"},
                              headers={"xi-api-key": key, "Content-Type": "application/json"},
                              json={"text": w["prompt"], "duration_seconds": w["gen"], "prompt_influence": 0.7,
                                    "model_id": "eleven_text_to_sound_v2"}, timeout=120)
            if r.status_code != 200: print(name, t, r.status_code, r.text[:200]); continue
            p = os.path.join(RAW, f"{w['id']}{t}.mp3"); open(p, "wb").write(r.content)
            try:
                y = shape(p, w); got.append((score(y, w), t, y))
            except Exception as ex:
                print(name, t, "자르기 실패", ex)
        _emit(name, w, sorted(got, key=lambda g: -g[0]))


def repick():
    """RAW에 있는 테이크로 다시 처리·채점만(크레딧 0)."""
    for name, w in WEAPONS.items():
        got = []
        for t in range(1, 9):
            p = os.path.join(RAW, f"{w['id']}{t}.mp3")
            if not os.path.exists(p): continue
            y = shape(p, w); got.append((score(y, w), t, y))
        _emit(name, w, sorted(got, key=lambda g: -g[0]))


def _emit(name, w, got):
    for label, (sc, t, y) in zip("AB", got[:2]):
        fs.ffmpeg_encode_ogg(y, os.path.join(OUT, f"{name}_{label}.ogg"))
        n = 8 if w["id"] == "machinegun" else 3
        burst = np.zeros(int((w["interval"] * n + 0.5) * SR))
        for k in range(n):
            s0 = int(k * w["interval"] * SR); burst[s0:s0 + len(y)] += y
        fs.ffmpeg_encode_ogg(burst * min(1.0, 0.89 / (np.abs(burst).max() + 1e-9)), os.path.join(OUT, f"{name}_{label}_연사.ogg"))
        print(f"[{name}_{label}] take{t}  {len(y)/SR:.2f}s  순간최대 {mmax(y):.1f} LUFS  어택 {attacks(y)}  "
              f"저(<150) {share(y,20,150):.0%} · 몸통 {share(y,*w['want']):.0%} · 고(>6k) {share(y,6000,22050):.0%}  평탄도 {flatness(y):.2f}")


def apply(name, label):
    w = WEAPONS[name]
    src = os.path.join(OUT, f"{name}_{label}.ogg"); dst = os.path.join(TARGET, f"sfx_{w['id']}_fire.ogg")
    shutil.copyfile(src, dst); print(src, "→", dst)


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "apply": apply(sys.argv[2], sys.argv[3])
    elif len(sys.argv) > 1 and sys.argv[1] == "repick": repick()
    elif len(sys.argv) > 1 and sys.argv[1] in ("simple", "simple_apply", "popgun_tries"): pass   # 아래 초기화 경로
    else:
        only = sys.argv[sys.argv.index("--only") + 1].split(",") if "--only" in sys.argv else None
        gen(int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 4, only)


# ---------------------------------------------------------------------------------------------
# 초기화 경로(2026-10-11): 사용자 「점점 이상해진다 — 프롬프트에 쓸모없는 게 많아서인가? 제약 걸린 프롬프트 없애고 새로」.
# 2~3차는 접두·금지어 줄줄이·저역 컷·리미팅 8~10dB·자동 채점이 겹쳐 있었다. 여기선 **짧은 프롬프트 한 줄 · prompt_influence 기본값**,
# 후처리는 앞 무음 자르기 · 길이 컷 + 끝 페이드 · DC 제거 · 피크 -1dBFS만(하이패스·리미터·채점 없음). 테이크를 전부 늘어놓고 사람이 고른다.
#   python gen_guns.py simple [--takes 4]  → 사운드_후보/총_비교/<무기>_1~4.ogg (+ 기관총은 _연사)
# ---------------------------------------------------------------------------------------------
SIMPLE = {
    "딱총": dict(id="popgun", prompt="popgun shot", gen=0.5, max_len=0.40, interval=0.8),
    "기관총": dict(id="machinegun", prompt="machine gun single shot", gen=0.5, max_len=0.13, interval=0.15),
}


def simple_shape(path, w):
    x = fs.ffmpeg_decode(path)
    pk = np.abs(x).max()
    start = max(0, int(np.argmax(np.abs(x) > pk * 0.05)) - int(0.002 * SR))
    y = x[start:start + int(w["max_len"] * SR)].copy()
    y -= y.mean()
    fin = int(0.002 * SR); y[:fin] *= np.linspace(0, 1, fin)
    fade = int((0.015 if w["max_len"] < 0.2 else 0.04) * SR)
    if len(y) > fade: y[-fade:] *= np.linspace(1, 0, fade)
    return y * (10 ** (-1 / 20) / (np.abs(y).max() + 1e-12))


def simple(takes=4):
    import requests
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key: print("ELEVENLABS_API_KEY 환경변수가 없다"); sys.exit(2)
    os.makedirs(RAW, exist_ok=True); os.makedirs(OUT, exist_ok=True)
    for name, w in SIMPLE.items():
        for t in range(1, takes + 1):
            r = requests.post("https://api.elevenlabs.io/v1/sound-generation", params={"output_format": "mp3_44100_192"},
                              headers={"xi-api-key": key, "Content-Type": "application/json"},
                              json={"text": w["prompt"], "duration_seconds": w["gen"], "model_id": "eleven_text_to_sound_v2"},
                              timeout=120)
            if r.status_code != 200: print(name, t, r.status_code, r.text[:200]); continue
            p = os.path.join(RAW, f"simple_{w['id']}{t}.mp3"); open(p, "wb").write(r.content)
            y = simple_shape(p, w)
            fs.ffmpeg_encode_ogg(y, os.path.join(OUT, f"{name}_{t}.ogg"))
            if w["id"] == "machinegun":
                burst = np.zeros(int((w["interval"] * 8 + 0.3) * SR))
                for k in range(8):
                    s0 = int(k * w["interval"] * SR); burst[s0:s0 + len(y)] += y
                fs.ffmpeg_encode_ogg(burst * min(1.0, 0.89 / (np.abs(burst).max() + 1e-9)), os.path.join(OUT, f"{name}_{t}_연사.ogg"))
            print(f"{name}_{t}: {len(y)/SR:.2f}s  순간최대 {mmax(y):.1f} LUFS")


def simple_apply(name, t):
    w = SIMPLE[name]
    shutil.copyfile(os.path.join(OUT, f"{name}_{t}.ogg"), os.path.join(TARGET, f"sfx_{w['id']}_fire.ogg"))


# 딱총 5차(2026-10-11): 'popgun shot' 4개 탈락, 기관총 'machine gun single shot' 1번 채택 · 새총 2차 B 채택.
# 한 줄 프롬프트를 여러 개 놓고 각 2테이크 — 무엇이 「딱총」으로 들리는지는 사람이 고른다.
POPGUN_TRIES = {"장난감총": "toy gun shot", "공기총": "air gun shot", "작은권총": "small pistol shot", "코르크": "cork gun pop"}


def popgun_tries(takes=2):
    import requests
    key = os.environ["ELEVENLABS_API_KEY"]; w = SIMPLE["딱총"]
    for tag, prompt in POPGUN_TRIES.items():
        for t in range(1, takes + 1):
            r = requests.post("https://api.elevenlabs.io/v1/sound-generation", params={"output_format": "mp3_44100_192"},
                              headers={"xi-api-key": key, "Content-Type": "application/json"},
                              json={"text": prompt, "duration_seconds": w["gen"], "model_id": "eleven_text_to_sound_v2"}, timeout=120)
            if r.status_code != 200: print(tag, t, r.status_code, r.text[:200]); continue
            p = os.path.join(RAW, f"popgun_{tag}{t}.mp3"); open(p, "wb").write(r.content)
            y = simple_shape(p, w); fs.ffmpeg_encode_ogg(y, os.path.join(OUT, f"딱총_{tag}_{t}.ogg"))
            print(f"딱총_{tag}_{t} ({prompt}): 순간최대 {mmax(y):.1f} LUFS")


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "popgun_tries":
    popgun_tries()


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] in ("simple", "simple_apply"):
    if sys.argv[1] == "simple": simple(int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 4)
    else: simple_apply(sys.argv[2], sys.argv[3])
