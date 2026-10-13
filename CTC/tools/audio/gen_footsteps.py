"""고양이·쥐 발소리 1·2·3 세트를 만든다(ElevenLabs → 자르기 → 후처리 → 사운드_후보/ → apply).

사용:
  python gen_footsteps.py gen [--takes 2]        # ELEVENLABS_API_KEY(사용자 환경변수) → %TEMP%/ctc-cat-steps/take*.mp3 + 후보 ogg
  왜 한 번에 셋인가: 따로 뽑으면 재질·공간을 모델이 매번 다르게 채워 서로 다른 발로 들린다(2026-10-11 계측 1.3k/4.9k/2.6kHz).
  python gen_footsteps.py slice <in.mp3|wav> <tag>  # 이미 있는 파일을 자르기만
  python gen_footsteps.py apply <tag>              # 후보 <tag>_1..3.ogg 를 Assets/Audio/Movement/sfx_cat_step1..3.ogg 로 복사
  python gen_footsteps.py gen1 [--takes 3]       # 단발 원샷 N개 → 각각 피치 -3/0/+3%로 1·2·3 파생(울림 긴 소리용 — 아래 단발 경로 주석)
  python gen_footsteps.py set1 <in.mp3> <tag>      # 있는 단발 파일에서 1·2·3 파생만
"""
import os, sys, json, subprocess, shutil, math
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(os.environ.get("TEMP", HERE), "ctc-cat-steps")
OUT = os.path.join(HERE, "out")
PROJ = os.path.abspath(os.path.join(HERE, "..", ".."))   # tools/audio/ → CTC/
CAND = os.path.join(PROJ, "..", "사운드_후보")
TARGET = os.path.join(PROJ, "Assets", "Audio", "Movement")
SR = 44100
MAX_LEN = 0.40          # 기존 고양이 발소리 길이와 같게
MIN_GAP = 0.30          # 걸음 사이 최소 간격(초) — 이보다 가까운 피크는 같은 걸음(기존 step1은 0.18s 뒤 두 번째 둥이 있다)

PROMPT = ("Cartoon video game sound effect, cute comedic toon style, no music, no melody. "
          "Three slow heavy cat footsteps in a row on old wooden floorboards in a pantry: big soft paw pads landing with a dull round thud "
          "and a hollow plank knock, evenly spaced about half a second apart, exaggerated Tom-and-Jerry style, menacing but comedic, "
          "dry close-mic recording, no echo, no reverb tail, silence between steps.")   # API 한도 450자 — 넘으면 400 text_too_long
# 문장 구조는 take1·4를 낸 것(「in a row … evenly spaced about half a second apart」)을 지킨다 —
# 「Each step: … Step, pause, step」·「deep thud」로 바꾸자 세 테이크 다 **한 번 쿵**(take8~10)이 나왔다.
# 왜 널마루인가(2026-10-11): 고양이 발소리는 「접근 경고」(사운드 기획 A) — 20m 선형 감쇠를 버티고 쥐의 가벼운 탁과 한 귀에 갈라져야 한다.
# 저음만 있는 쿵(take1, ~270Hz)은 안 들렸고, 또렷한 발바닥 탭(take4, ~1.2kHz)은 쥐와 성격이 가까웠다. 나무 판 공명이 중음 몸통을 준다.
DURATION = 2.0   # 2.5로 늘려도 걸음이 늘지 않았다(take8~10) — 길이가 아니라 문장 구조가 걸음 수를 정한다
PROMPT_INFLUENCE = 0.75


def ffmpeg_decode(path):
    cmd = ["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"]
    data = subprocess.run(cmd, check=True, capture_output=True).stdout
    return np.frombuffer(data, dtype=np.float32).astype(np.float64)


def ffmpeg_encode_ogg(x, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    cmd = ["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
           "-c:a", "libvorbis", "-q:a", "5", path]
    subprocess.run(cmd, check=True, input=x.astype(np.float32).tobytes())


def biquad_hpf(x, fc, q=0.7071):
    w0 = 2 * math.pi * fc / SR
    alpha = math.sin(w0) / (2 * q)
    cosw = math.cos(w0)
    b0 = (1 + cosw) / 2; b1 = -(1 + cosw); b2 = (1 + cosw) / 2
    a0 = 1 + alpha; a1 = -2 * cosw; a2 = 1 - alpha
    b = np.array([b0, b1, b2]) / a0; a = np.array([a1, a2]) / a0
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = b[0] * v + b[1] * x1 + b[2] * x2 - a[0] * y1 - a[1] * y2
        x2, x1 = x1, v; y2, y1 = y1, o
        y[i] = o
    return y


def envelope(x, ms=5.0):
    n = max(1, int(SR * ms / 1000))
    k = np.ones(n) / n
    return np.convolve(np.abs(x), k, mode="same")


def find_onsets(x, count=3):
    env = envelope(x)
    peak = env.max()
    thr = peak * 10 ** (-20 / 20)        # 피크 -20dB 넘는 곳이 「걸음」
    above = env > thr
    onsets = []
    i = 0
    gap = int(MIN_GAP * SR)
    armed = True                          # 한 번 넘으면 -30dB 밑으로 내려올 때까지 다시 안 센다(히스테리시스)
    low = peak * 10 ** (-30 / 20)
    while i < len(x):
        if above[i] and armed:
            onsets.append(i); armed = False
            i += gap
        else:
            if env[i] < low: armed = True
            i += 1
    return onsets, env


def cut_step(x, onset, next_onset):
    seg_peak = np.abs(x[onset: min(len(x), onset + int(0.1 * SR))]).max()
    # 시작: 그 걸음 피크의 -40dB를 처음 넘는 곳 2ms 앞 (어택 보존)
    back = max(0, onset - int(0.05 * SR))
    thr = seg_peak * 10 ** (-40 / 20)
    idx = np.where(np.abs(x[back:onset + int(0.05 * SR)]) > thr)[0]
    start = back + (idx[0] if len(idx) else onset - back) - int(0.002 * SR)
    start = max(0, start)
    end_limit = min(len(x), start + int(MAX_LEN * SR), (next_onset - int(0.01 * SR)) if next_onset else len(x))
    # 끝: -50dB 꼬리까지
    thr_tail = seg_peak * 10 ** (-50 / 20)
    seg = x[start:end_limit]
    loud = np.where(np.abs(seg) > thr_tail)[0]
    end = start + (loud[-1] + 1 if len(loud) else len(seg))
    return x[start:end].copy()


def post(x):
    x = x - x.mean()                    # DC
    x = biquad_hpf(x, 45.0)             # 초저음
    n_in = int(0.003 * SR)              # 3ms 올림 코사인 페이드인
    if len(x) > n_in:
        x[:n_in] *= 0.5 - 0.5 * np.cos(np.linspace(0, math.pi, n_in))
    n_out = int(0.030 * SR)             # 30ms 페이드아웃
    if len(x) > n_out:
        x[-n_out:] *= np.linspace(1, 0, n_out)
    # 세트 안 세기를 맞춘다 — 피크 정규화만 하면 둔탁한 소리는 **들리지 않는다**(2026-10-11: take1 -21~-26 LUFS vs 기존 -17.3,
    # 사용자 「엄청 안 들린다」). 통합 라우드니스를 기존 발소리와 같은 TARGET_LUFS로 맞추고, 그래도 넘치는 피크만 -1dBFS로 깎는다.
    # 짧은 톡(쥐 발소리 등)은 피크 대비 세기가 작아 리미터를 지나면 라우드니스가 다시 떨어진다(2026-10-11: 목표 -17인데 -20~-25).
    # 리미터를 지난 뒤 다시 재서 모자란 만큼 올리기를 최대 4번 — 어택을 뭉개지 않게 총 리미팅은 MAX_LIMIT_DB까지만.
    cap = 10 ** (-1 / 20)
    pushed = 0.0
    for _ in range(4):
        cur = lufs(x)
        if cur is None: break
        gain_db = TARGET_LUFS - cur
        if abs(gain_db) < 0.5: break
        if gain_db > 0: gain_db = min(gain_db, max(0.0, MAX_LIMIT_DB - pushed))
        if gain_db == 0: break
        x = x * 10 ** (gain_db / 20)
        if np.abs(x).max() > cap:
            pushed += max(0.0, 20 * math.log10(np.abs(x).max() / cap))
            x = limit(x, cap)           # 피크만 -1dBFS로 눌러 세기는 지킨다
    if np.abs(x).max() > cap:
        x = x * cap / np.abs(x).max()
    return x


MAX_LIMIT_DB = 8.0                      # 리미터로 깎는 피크의 총량 상한 — 넘으면 톡이 「틱」으로 뭉개진다


def rms50(y):
    """가장 센 50ms 창의 RMS(dB). 0.1초 안팎의 톡은 LUFS(400ms 블록)로 재면 묽어져 세기를 못 맞춘다 — 짧은 소리는 이걸로 맞춘다."""
    w = int(0.05 * SR)
    if len(y) < w: y = np.concatenate([y, np.zeros(w - len(y))])
    c = np.convolve(y ** 2, np.ones(w) / w, mode="valid")
    return 10 * math.log10(c.max() + 1e-12)


def match_rms50(y, target_db, max_limit_db=MAX_LIMIT_DB):
    """세트 안 세기를 rms50으로 맞춘다(쥐 발소리: 세트 안 편차가 8dB였다). 피크는 리미터로 -1dBFS."""
    cap = 10 ** (-1 / 20); pushed = 0.0
    for _ in range(5):
        gain = target_db - rms50(y)
        if abs(gain) < 0.3: break
        if gain > 0: gain = min(gain, max(0.0, max_limit_db - pushed))
        if gain == 0: break
        y = y * 10 ** (gain / 20)
        if np.abs(y).max() > cap:
            pushed += 20 * math.log10(np.abs(y).max() / cap)
            y = limit(y, cap)
    return y


def limit(x, cap):
    """ffmpeg alimiter — 1ms 어택·20ms 릴리즈 피크 리미터. 발소리 같은 짧은 타격음의 꼭대기만 깎는다."""
    cmd = ["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
           "-af", f"alimiter=limit={cap:.3f}:attack=1:release=20:level=0", "-f", "f32le", "-"]
    r = subprocess.run(cmd, input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(r.stdout, dtype=np.float32).astype(np.float64)


TARGET_LUFS = -17.0                     # 09-27 고양이 step1(-17.3 LUFS)과 같은 세기


def lufs(x):
    """ffmpeg ebur128 통합 라우드니스(LUFS). 짧은 소리라 뒤에 1초 무음을 붙여 잰다(게이트가 무음은 뺀다)."""
    import re
    cmd = ["ffmpeg", "-v", "info", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
           "-af", "apad=pad_dur=1,ebur128", "-f", "null", "-"]
    r = subprocess.run(cmd, input=x.astype(np.float32).tobytes(), capture_output=True)
    m = re.findall(r"I:\s+(-?[\d.]+) LUFS", r.stderr.decode(errors="ignore"))
    return float(m[-1]) if m else None


def measure(x):
    rms = lufs(x) or 0.0  # 표기는 LUFS
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x))))
    freqs = np.fft.rfftfreq(len(x), 1 / SR)
    cen = float((spec * freqs).sum() / (spec.sum() + 1e-12))
    return len(x) / SR, rms, cen


def slice_file(path, tag, want=3):
    x = ffmpeg_decode(path)
    onsets, env = find_onsets(x)
    print(f"[{tag}] onsets(s): {[round(o / SR, 3) for o in onsets]}")
    if len(onsets) < want:
        print(f"[{tag}] 걸음이 {len(onsets)}개뿐 — 이 테이크는 버린다"); return False
    onsets = onsets[:want]
    pieces = []
    for i, o in enumerate(onsets):
        nxt = onsets[i + 1] if i + 1 < len(onsets) else None
        y = post(cut_step(x, o, nxt))
        pieces.append(y)
        d, rms, cen = measure(y)
        out = os.path.join(CAND, f"고양이발소리_{tag}_{i + 1}.ogg")
        ffmpeg_encode_ogg(y, out)
        print(f"  step{i + 1}: {d:.3f}s  {rms:6.1f} LUFS  peak {20*math.log10(np.abs(y).max()+1e-12):5.1f} dBFS  centroid {cen:6.0f} Hz  → {out}")
    # 들어 보기용: 세 걸음을 0.4초 간격으로 이어 붙인 미리듣기
    gap = np.zeros(int(0.4 * SR))
    preview = np.concatenate([np.concatenate([p, gap]) for p in pieces] * 2)
    ffmpeg_encode_ogg(preview, os.path.join(CAND, f"고양이발소리_{tag}_연속미리듣기.ogg"))
    return True


def gen(takes):
    import requests
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        print("ELEVENLABS_API_KEY 환경변수가 없다"); sys.exit(2)
    os.makedirs(RAW, exist_ok=True)
    for t in range(1, takes + 1):
        r = requests.post("https://api.elevenlabs.io/v1/sound-generation",
                          params={"output_format": "mp3_44100_192"},
                          headers={"xi-api-key": key, "Content-Type": "application/json"},
                          json={"text": PROMPT, "duration_seconds": DURATION,
                                "prompt_influence": PROMPT_INFLUENCE, "model_id": "eleven_text_to_sound_v2"},
                          timeout=120)
        if r.status_code != 200:
            print(f"take{t}: HTTP {r.status_code} {r.text[:300]}"); sys.exit(1)
        raw = os.path.join(RAW, f"take{t}.mp3")
        open(raw, "wb").write(r.content)
        print(f"take{t}: {len(r.content)} bytes → {raw}")
        slice_file(raw, f"take{t}")


def apply(tag):
    for i in (1, 2, 3):
        src = os.path.join(CAND, f"고양이발소리_{tag}_{i}.ogg")
        dst = os.path.join(TARGET, f"sfx_cat_step{i}.ogg")
        shutil.copyfile(src, dst)
        print(f"{src} → {dst}")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "gen"
    if cmd == "gen":
        n = int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 2
        gen(n)
    elif cmd == "slice":
        slice_file(sys.argv[2], sys.argv[3])
    elif cmd == "apply":
        apply(sys.argv[2])
    elif cmd in ("gen1", "set1", "set3", "mouse", "apply_mouse"):
        pass   # 아래 단발 경로 블록이 처리한다
    else:
        print(__doc__)


# ---------------------------------------------------------------------------------------------
# 단발 경로(2026-10-11): 「널마루 쿵」처럼 울림이 긴 소리는 한 테이크에 세 걸음을 넣어도 모델이 **한 번 쿵**만 낸다
# (take5~13, 9개 중 3걸음 0개). 게임의 걸음 간격은 0.4초라 클립도 0.4초 안에서 끝나야 하니 긴 울림은 어차피 못 쓴다.
# → **한 걸음짜리 원샷**을 뽑고, 1·2·3은 그 하나에서 피치 −3% / 0 / +3%로 파생한다. 같은 발·같은 바닥이 보장되고
#   코드의 ±4% 지터와 합쳐 연타 느낌만 피한다(고양이 발소리는 「접근 경고」 신호라 매번 같은 소리로 들리는 쪽이 맞다).
# ---------------------------------------------------------------------------------------------
SINGLE_PROMPT = ("Foley sound effect, single one-shot, no music. One heavy step of a big fat cat on a wooden floor: "
                 "a weighty soft muffled thud like a padded leather glove pressed on the floor, slight fur brush. "
                 "Dry, close-mic, no echo, no knock, no hollow tone.")
# 2026-10-11 사용자 채택(「C큰발2」). 만화 접두('cute comedic toon')·'hollow plank knock'은 「통통」 북소리를 냈다 — 발소리엔 쓰지 않는다.
SINGLE_DURATION = 0.6
VARIANT_PITCH = (-0.03, 0.0, 0.03)


def pitch_shift(x, ratio):
    """asetrate로 재생 속도를 바꿔 피치·길이를 함께 ±(리샘플) — 짧은 타격음엔 이게 가장 자연스럽다."""
    if abs(ratio) < 1e-6: return x.copy()
    rate = int(SR * (1 + ratio))
    cmd = ["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
           "-af", f"asetrate={rate},aresample={SR}", "-f", "f32le", "-"]
    r = subprocess.run(cmd, input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(r.stdout, dtype=np.float32).astype(np.float64)


def single_to_set(path, tag):
    x = ffmpeg_decode(path)
    onsets, _ = find_onsets(x, count=1)
    if not onsets:
        print(f"[{tag}] 걸음을 못 찾았다"); return False
    step = cut_step(x, onsets[0], None)
    pieces = []
    for i, p in enumerate(VARIANT_PITCH):
        y = post(pitch_shift(step, p))
        y = y[:int(MAX_LEN * SR)]
        pieces.append(y)
        d, lu, cen = measure(y)
        out = os.path.join(CAND, f"고양이발소리_{tag}_{i + 1}.ogg")
        ffmpeg_encode_ogg(y, out)
        print(f"  step{i + 1} (pitch {p:+.0%}): {d:.3f}s  {lu:6.1f} LUFS  peak {20*math.log10(np.abs(y).max()+1e-12):5.1f} dBFS  centroid {cen:6.0f} Hz")
    gap = np.zeros(int(0.4 * SR) - min(len(p) for p in pieces)) if min(len(p) for p in pieces) < int(0.4 * SR) else np.zeros(1)
    preview = np.concatenate([np.concatenate([p, np.zeros(max(1, int(0.4 * SR) - len(p)))]) for p in pieces] * 3)
    ffmpeg_encode_ogg(preview, os.path.join(CAND, f"고양이발소리_{tag}_연속미리듣기.ogg"))
    return True


def gen_single(takes, start=1):
    import requests
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        print("ELEVENLABS_API_KEY 환경변수가 없다"); sys.exit(2)
    os.makedirs(RAW, exist_ok=True)
    for t in range(start, start + takes):
        r = requests.post("https://api.elevenlabs.io/v1/sound-generation",
                          params={"output_format": "mp3_44100_192"},
                          headers={"xi-api-key": key, "Content-Type": "application/json"},
                          json={"text": SINGLE_PROMPT, "duration_seconds": SINGLE_DURATION,
                                "prompt_influence": PROMPT_INFLUENCE, "model_id": "eleven_text_to_sound_v2"}, timeout=120)
        if r.status_code != 200:
            print(f"single{t}: HTTP {r.status_code} {r.text[:300]}"); sys.exit(1)
        raw = os.path.join(RAW, f"single{t}.mp3")
        open(raw, "wb").write(r.content)
        print(f"single{t}: {len(r.content)} bytes")
        single_to_set(raw, f"단발{t}")


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] in ("gen1", "set1"):
    if sys.argv[1] == "gen1":
        n = int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 3
        st = int(sys.argv[sys.argv.index("--start") + 1]) if "--start" in sys.argv else 1
        gen_single(n, st)
    else:
        single_to_set(sys.argv[2], sys.argv[3])


# ---------------------------------------------------------------------------------------------
# 세트 경로(2026-10-11 최종): 피치 ±3% 파생은 사용자 귀에 「셋이 다른 게 없다」였다.
# → **같은 프롬프트로 여러 번** 뽑고, 기준 테이크와의 1/3옥타브 스펙트럼 거리가 1.5~6dB인 것 중 가까운 둘을 고른다
#   (같은 발·같은 바닥인데 녹음마다 다른 정도 — 실제 폴리 녹음의 「같은 발 여러 테이크」). 6dB 넘으면 다른 발로 들린다.
#   python gen_footsteps.py set3 <기준.mp3> <후보1.mp3> <후보2.mp3> ...  → 사운드_후보/고양이발소리_C세트_1~3.ogg
# ---------------------------------------------------------------------------------------------
def _step_of(path):
    x = ffmpeg_decode(path)
    on, _ = find_onsets(x, count=1)
    return post(cut_step(x, on[0], None))[:int(MAX_LEN * SR)]


def _bands(y):
    n = 8192
    Y = np.abs(np.fft.rfft(y, n)) ** 2
    f = np.fft.rfftfreq(n, 1 / SR)
    edges = [50 * 2 ** (k / 3) for k in range(0, 23)]          # 50Hz~8kHz 1/3옥타브
    b = np.array([Y[(f >= lo) & (f < hi)].sum() + 1e-12 for lo, hi in zip(edges[:-1], edges[1:])])
    b = 10 * np.log10(b)
    return b - b.mean()


def pick_set(ref_path, cand_paths, tag="C세트", lo=1.5, hi=6.0):
    ref = _step_of(ref_path)
    rb = _bands(ref)
    scored = []
    for p in cand_paths:
        y = _step_of(p)
        d = float(np.sqrt(((_bands(y) - rb) ** 2).mean()))
        scored.append((d, p, y))
        print(f"  {os.path.basename(p)}: 거리 {d:.1f} dB")
    scored.sort(key=lambda s: s[0])
    pick = [s for s in scored if lo <= s[0] <= hi][:2] or scored[:2]
    pieces = [ref] + [s[2] for s in pick]
    for i, y in enumerate(pieces):
        ffmpeg_encode_ogg(y, os.path.join(CAND, f"고양이발소리_{tag}_{i + 1}.ogg"))
    print("고름:", [os.path.basename(s[1]) for s in pick])


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "set3":
    pick_set(sys.argv[2], sys.argv[3:])


# ---------------------------------------------------------------------------------------------
# 쥐 발소리(2026-10-11 사용자 채택 「F손끝」) — 고양이와 다른 함정 셋:
#  ① 프롬프트에 'mouse'·'tiny'가 있으면 모델이 4kHz 위 쨍한 소리(원본 에너지 70~97%)를 낸다 = 「동전 쇳소리」.
#     'no metallic, no tink'를 붙여도 안 사라진다 → **쥐라는 말 없이 물리 동작만** 적는다(손끝으로 나무 탁자 톡: 5~7%).
#  ② 한 클립에 톡이 2~4번(종종걸음) 들어 있다 — 걸음은 0.29초마다라 겹쳐 뭉갠다 → 첫 톡만 남긴다(first_hit_only).
#  ③ 0.1초 안팎의 톡은 LUFS(400ms 블록)로 재면 묽어진다 → rms50(-12dB, 고양이 세트 -5dB보다 7dB 작게)로 맞추고
#     리미팅은 6dB까지만(12dB는 쨍함을 키웠다). 6kHz 위는 저역통과로 자른다.
#   python gen_footsteps.py mouse [--takes 4]  → 사운드_후보/쥐발소리_쥐세트_1~3.ogg  ·  apply_mouse <tag>
# ---------------------------------------------------------------------------------------------
MOUSE_PROMPT = ("Foley sound effect, single one-shot, no music. One small soft footstep of a mouse: a gentle muffled pat "
                "like a fingertip lightly tapping a wooden table, faint fur rustle. Very short, dry, close-mic. "
                "No metallic sound, no click, no tink, no ringing, no jingle, no high pitch.")
MOUSE_MAX_LEN = 0.28
MOUSE_RMS50 = -12.0
MOUSE_LIMIT_DB = 6.0
MOUSE_LOWPASS = 6000


def lowpass(y, fc):
    r = subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                        "-af", f"lowpass=f={fc}:p=2,lowpass=f={fc}:p=2", "-f", "f32le", "-"],
                       input=y.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(r.stdout, dtype=np.float32).astype(np.float64)


def first_hit_only(x):
    """클립 안의 두 번째 톡 직전에서 자른다(첫 톡 = 가장 큰 톡이 아니라 -6dB를 처음 넘는 톡)."""
    e = envelope(x, 3.0); db = 20 * np.log10(e / e.max() + 1e-9)
    j = int(np.argmax(db > -6))
    while j < len(x) - 1 and db[j + 1] >= db[j]: j += 1
    dipped = False; lowest = 0.0
    for k in range(j, len(x)):
        if db[k] < -15: dipped = True
        lowest = min(lowest, db[k])
        if dipped and db[k] > -12 and db[k] - lowest > 10:
            return x[:max(int(0.03 * SR), k - int(0.005 * SR))]
    return x


def mouse_step_of(path):
    x = ffmpeg_decode(path); on, _ = find_onsets(x, count=1)
    y = first_hit_only(cut_step(x, on[0], None)[:int(MOUSE_MAX_LEN * SR)])
    return match_rms50(post(lowpass(y, MOUSE_LOWPASS)), MOUSE_RMS50, max_limit_db=MOUSE_LIMIT_DB)


def gen_mouse(takes=4, tag="쥐세트"):
    import requests, itertools
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key: print("ELEVENLABS_API_KEY 환경변수가 없다"); sys.exit(2)
    os.makedirs(RAW, exist_ok=True); got = []
    for t in range(1, takes + 1):
        r = requests.post("https://api.elevenlabs.io/v1/sound-generation", params={"output_format": "mp3_44100_192"},
                          headers={"xi-api-key": key, "Content-Type": "application/json"},
                          json={"text": MOUSE_PROMPT, "duration_seconds": 0.5, "prompt_influence": 0.7,
                                "model_id": "eleven_text_to_sound_v2"}, timeout=120)
        if r.status_code != 200: print(t, r.status_code, r.text[:200]); continue
        p = os.path.join(RAW, f"mouse_{tag}{t}.mp3"); open(p, "wb").write(r.content)
        got.append((t, mouse_step_of(p)))
    bands = {t: _bands(y) for t, y in got}; best = None
    for trio in itertools.combinations([t for t, _ in got], 3):
        ds = [float(np.sqrt(((bands[a] - bands[b]) ** 2).mean())) for a, b in itertools.combinations(trio, 2)]
        if min(ds) < 1.5: continue
        if best is None or max(ds) < best[0]: best = (max(ds), trio)
    for i, t in enumerate(best[1]):
        ffmpeg_encode_ogg(dict(got)[t], os.path.join(CAND, f"쥐발소리_{tag}_{i + 1}.ogg"))
    print("세트:", best[1])


def apply_mouse(tag):
    for i in (1, 2, 3):
        shutil.copyfile(os.path.join(CAND, f"쥐발소리_{tag}_{i}.ogg"), os.path.join(TARGET, f"sfx_mouse_step{i}.ogg"))


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] in ("mouse", "apply_mouse"):
    if sys.argv[1] == "mouse":
        gen_mouse(int(sys.argv[sys.argv.index("--takes") + 1]) if "--takes" in sys.argv else 4)
    else:
        apply_mouse(sys.argv[2])
