# coding=utf-8
"""시안(목업) vs 빌드 결정론적 시각 비교 — OpenCV 다중 서브메트릭.

기존 structure_match(텍스트 행 카운트)가 스케일·텍스처·밀도 차이를 못 봐서 F5를
1.0(동일)로 오판한 문제를 보완. 같은 입력 → 항상 같은 점수(결정론적).

서브메트릭:
  §1 fill   — 콘텐츠 영역 채움비율(스케일/여백 차이). 렌더방식 무관해서 주축.
  §2 color  — HSV 색 히스토그램 상관(텍스처/톤 차이: 나무vs돌).
  §3 edge   — Canny 엣지의 coarse 그리드 분포 코사인(밀도/배치).
  §4 corr   — 그레이 정규화 후 전역 상관(matchTemplate TM_CCOEFF_NORMED).
"""
import os

import cv2
import numpy as np


def _content_fill(img):
    """콘텐츠(비배경) 바운딩박스의 화면 채움비율 + 종횡비. 배경=최빈 어두운 영역."""
    h, w = img.shape[:2]
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    # 배경(가장 흔한 어두운 값)보다 충분히 밝은 픽셀 = 콘텐츠
    bg = int(np.median(gray))
    mask = (gray > bg + 18).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE,
                            cv2.getStructuringElement(cv2.MORPH_RECT, (15, 15)))
    n, _, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    if n <= 1:
        return 0.0, 1.0
    # 배경(0) 제외 컴포넌트들의 union bbox
    xs = stats[1:, cv2.CC_STAT_LEFT]
    ys = stats[1:, cv2.CC_STAT_TOP]
    xe = xs + stats[1:, cv2.CC_STAT_WIDTH]
    ye = ys + stats[1:, cv2.CC_STAT_HEIGHT]
    # 면적 상위 영역만(노이즈 제외)
    areas = stats[1:, cv2.CC_STAT_AREA]
    keep = areas >= areas.max() * 0.02
    x0, y0, x1, y1 = xs[keep].min(), ys[keep].min(), xe[keep].max(), ye[keep].max()
    fill = ((x1 - x0) * (y1 - y0)) / float(w * h)
    aspect = (x1 - x0) / max(1.0, (y1 - y0))
    return float(fill), float(aspect)


def _color_sim(img_a, img_b):
    """HSV 색 히스토그램 상관 (0~1)."""
    def hist(img):
        hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
        h = cv2.calcHist([hsv], [0, 1], None, [50, 60], [0, 180, 0, 256])
        cv2.normalize(h, h, 0, 1, cv2.NORM_MINMAX)
        return h
    c = cv2.compareHist(hist(img_a), hist(img_b), cv2.HISTCMP_CORREL)
    return max(0.0, float(c))


def _edge_sim(img_a, img_b, W=48, H=27):
    """Canny 엣지의 coarse 분포 코사인 (0~1)."""
    def emap(img):
        g = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        e = cv2.Canny(g, 60, 160)
        small = cv2.resize(e, (W, H)).astype(np.float32).ravel()
        n = np.linalg.norm(small)
        return small / (n + 1e-6)
    va, vb = emap(img_a), emap(img_b)
    return max(0.0, float(va.dot(vb)))


def _global_corr(img_a, img_b, W=256, H=144):
    """크기 정규화 후 전역 정규화 상관 (matchTemplate TM_CCOEFF_NORMED, 0~1)."""
    ga = cv2.cvtColor(cv2.resize(img_a, (W, H)), cv2.COLOR_BGR2GRAY)
    gb = cv2.cvtColor(cv2.resize(img_b, (W, H)), cv2.COLOR_BGR2GRAY)
    r = cv2.matchTemplate(ga, gb, cv2.TM_CCOEFF_NORMED)
    return max(0.0, float(r[0, 0]))


def _geom_features(img):
    """렌더 무관 기하 특징: 채움·종횡비·중심·투영분포(콘텐츠 마스크 기반)."""
    h, w = img.shape[:2]
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    bg = int(np.median(gray))
    mask = (gray > bg + 18).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE,
                            cv2.getStructuringElement(cv2.MORPH_RECT, (15, 15)))
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return dict(fill=0.0, aspect=1.0, cx=0.5, cy=0.5,
                    xprof=np.zeros(64, np.float32), yprof=np.zeros(48, np.float32))
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    fill = ((x1 - x0) * (y1 - y0)) / float(w * h)
    aspect = (x1 - x0) / max(1.0, float(y1 - y0))
    cx, cy = xs.mean() / w, ys.mean() / h
    xprof = cv2.resize(mask.sum(0).astype(np.float32).reshape(1, -1), (64, 1)).ravel()
    yprof = cv2.resize(mask.sum(1).astype(np.float32).reshape(-1, 1), (1, 48)).ravel()
    xprof /= (xprof.max() + 1e-6)
    yprof /= (yprof.max() + 1e-6)
    return dict(fill=fill, aspect=aspect, cx=cx, cy=cy, xprof=xprof, yprof=yprof)


def _pcorr(a, b):
    a = a - a.mean(); b = b - b.mean()
    return max(0.0, float(a.dot(b) / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-6)))


def geom_compare(img_a, img_b, weights=(0.65, 0.15, 0.10, 0.10), threshold=0.50):
    """렌더 무관 기하 시각 비교 (결정론적). fill·profile·centroid·aspect 가중합.

    fill(콘텐츠 채움비율)을 주축으로 — AI그림 vs 실렌더에서도 안정적. 색/픽셀은
    렌더 차이로 무력해 제외. 캘리브레이션: L1~5(충실) 0.64~0.84 / F1~5(작은섬) 0.24~0.42
    → threshold 0.50으로 갈림. (스케일·배치 기준 결정론적 게이트; 미세 아트 일치는 못 봄.)
    """
    fa, fb = _geom_features(img_a), _geom_features(img_b)
    fill_sim = 1.0 - min(1.0, abs(fa["fill"] - fb["fill"]) / max(fa["fill"], fb["fill"], 1e-6))
    aspect_sim = 1.0 - min(1.0, abs(fa["aspect"] - fb["aspect"]) / max(fa["aspect"], fb["aspect"], 1e-6))
    centroid_sim = 1.0 - min(1.0, float(np.hypot(fa["cx"] - fb["cx"], fa["cy"] - fb["cy"])) / 0.5)
    profile_sim = 0.5 * (_pcorr(fa["xprof"], fb["xprof"]) + _pcorr(fa["yprof"], fb["yprof"]))
    w = weights
    score = w[0] * fill_sim + w[1] * profile_sim + w[2] * centroid_sim + w[3] * aspect_sim
    return {
        "score": round(score, 3),
        "passed": score >= threshold,
        "fill": round(fill_sim, 3),
        "profile": round(profile_sim, 3),
        "centroid": round(centroid_sim, 3),
        "aspect": round(aspect_sim, 3),
        "fill_raw": (round(fa["fill"], 3), round(fb["fill"], 3)),
    }


def _content_bbox(img, pad=0.02):
    """엣지밀도 기반 팝업 bbox — 배경 무관 crop.

    팝업(UI)은 엣지가 빽빽(나뭇결·글자·게이지), 배경(하늘·바닥·단색)은 매끈.
    Canny→dilate→최대 연결성분 bbox = 팝업. 시안(단색배경)·빌드(하늘+바닥) 양쪽 대응.
    """
    h, w = img.shape[:2]
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    e = cv2.Canny(gray, 60, 160)
    e = cv2.dilate(e, cv2.getStructuringElement(cv2.MORPH_RECT, (15, 15)))
    n, _, stats, _ = cv2.connectedComponentsWithStats(e, 8)
    if n <= 1:
        return 0, 0, w, h
    areas = stats[1:, cv2.CC_STAT_AREA]
    i = int(areas.argmax()) + 1  # 엣지 최대 덩어리 = 팝업
    x, y, ww, hh = (stats[i, cv2.CC_STAT_LEFT], stats[i, cv2.CC_STAT_TOP],
                    stats[i, cv2.CC_STAT_WIDTH], stats[i, cv2.CC_STAT_HEIGHT])
    px, py = int(ww * pad), int(hh * pad)
    x0, y0 = max(0, x - px), max(0, y - py)
    x1, y1 = min(w, x + ww + px), min(h, y + hh + py)
    return x0, y0, x1, y1


def _ssim(a, b):
    """표준 SSIM (gaussian window). a,b: 동일 크기 그레이 (0~255)."""
    a = a.astype(np.float64); b = b.astype(np.float64)
    C1, C2 = (0.01 * 255) ** 2, (0.03 * 255) ** 2
    k = (11, 11); s = 1.5
    mu_a = cv2.GaussianBlur(a, k, s); mu_b = cv2.GaussianBlur(b, k, s)
    mu_a2, mu_b2, mu_ab = mu_a * mu_a, mu_b * mu_b, mu_a * mu_b
    va = cv2.GaussianBlur(a * a, k, s) - mu_a2
    vb = cv2.GaussianBlur(b * b, k, s) - mu_b2
    vab = cv2.GaussianBlur(a * b, k, s) - mu_ab
    smap = ((2 * mu_ab + C1) * (2 * vab + C2)) / ((mu_a2 + mu_b2 + C1) * (va + vb + C2))
    return max(0.0, float(smap.mean()))


def fine_compare(mockup, build, threshold=0.62, size=(420, 520), weights=(0.6, 0.4)):
    """동일 도메인(시안·빌드 같은 스프라이트) 정밀 아트충실도 비교.

    geom_compare(실루엣만)로 못 잡는 미세 불일치를 잡기 위한 2차 게이트.
    ① 각 이미지를 팝업 bbox로 crop(배경·에디터크롬 제거) → 공통 크기로 resize(정렬)
    ② SSIM(구조 유사) + edge 상관(Canny 코사인 — 게이지 각짐/모양·아티팩트 반영)
    반환: score(0~1) 가중합, passed, 서브점수.

    한계: 폰트 AA·정확 색은 렌더러 차이로 완벽 일치 불가 → threshold는 관대(0.62).
    코너 라운딩 수 px 같은 초미세 차이는 edge가 부분적으로만 반영. 최종은 사람.
    """
    W, H = size
    def prep(img):
        x0, y0, x1, y1 = _content_bbox(img)
        crop = img[y0:y1, x0:x1]
        g = cv2.cvtColor(cv2.resize(crop, (W, H)), cv2.COLOR_BGR2GRAY)
        return g
    ga, gb = prep(mockup), prep(build)
    ssim = _ssim(ga, gb)
    ea = cv2.Canny(ga, 60, 160).astype(np.float32).ravel()
    eb = cv2.Canny(gb, 60, 160).astype(np.float32).ravel()
    edge = max(0.0, float(ea.dot(eb) / (np.linalg.norm(ea) * np.linalg.norm(eb) + 1e-6)))
    w = weights
    score = w[0] * ssim + w[1] * edge
    return {
        "score": round(score, 3),
        "passed": score >= threshold,
        "ssim": round(ssim, 3),
        "edge": round(edge, 3),
        "note": "fine(구조+엣지) — 팝업 crop·정렬 후. 아트 미세일치는 여전히 사람 최종.",
    }


def visual_gate(build_img, ref_path, threshold=0.50, weights=(0.65, 0.15, 0.10, 0.10)):
    """골든 기준 자동 시각 게이트 (러너가 매 실행 호출하는 진입점).

    - ref_path 없음   → build_img를 골든으로 저장하고 PASS(첫 baseline 수립).
    - ref_path 있음   → geom_compare로 채점, score>=threshold면 PASS.
    반환 dict는 Report.step에 그대로 넣을 수 있도록 passed/note 포함.
    """
    if not os.path.exists(ref_path):
        d = os.path.dirname(ref_path)
        if d:
            os.makedirs(d, exist_ok=True)
        cv2.imwrite(ref_path, build_img)
        return {"passed": True, "score": None, "baseline": True,
                "note": "baseline 생성: %s" % os.path.basename(ref_path)}
    ref = cv2.imread(ref_path, cv2.IMREAD_COLOR)
    if ref is None:
        return {"passed": False, "score": None,
                "note": "골든 로드 실패: %s" % ref_path}
    r = geom_compare(build_img, ref, weights=weights, threshold=threshold)
    r["baseline"] = False
    r["note"] = "geom score=%.3f (thr %.2f) fill=%.3f" % (r["score"], threshold, r["fill"])
    return r


def visual_compare(img_a, img_b, weights=(0.40, 0.25, 0.20, 0.15)):
    """시안 vs 빌드 결정론적 시각 유사도. 반환: 서브점수 + 가중합 score(0~1)."""
    fa, aspa = _content_fill(img_a)
    fb, aspb = _content_fill(img_b)
    fill_sim = 1.0 - min(1.0, abs(fa - fb) / max(fa, fb, 1e-6))
    aspect_sim = 1.0 - min(1.0, abs(aspa - aspb) / max(aspa, aspb, 1e-6))
    s1 = 0.7 * fill_sim + 0.3 * aspect_sim
    s2 = _color_sim(img_a, img_b)
    s3 = _edge_sim(img_a, img_b)
    s4 = _global_corr(img_a, img_b)
    w = weights
    score = w[0] * s1 + w[1] * s2 + w[2] * s3 + w[3] * s4
    return {
        "score": round(score, 3),
        "fill": round(s1, 3),
        "color": round(s2, 3),
        "edge": round(s3, 3),
        "corr": round(s4, 3),
        "fill_ratio": (round(fa, 3), round(fb, 3)),
    }
