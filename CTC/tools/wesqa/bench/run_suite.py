# coding=utf-8
"""스위트 1회 실행 → GREEN/RED·소요시간(JSON) 반환. seed 주입 사이에 호출된다.
사용: python -m bench.run_suite <label>"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))  # tools/wesqa
from wesqa import WesPoco
from bench.scenarios import login_checks


def run(label="run"):
    g = WesPoco(instance=0)
    t0 = time.time()
    results = []
    for name, fn in login_checks():
        try:
            ok = bool(fn(g))
        except Exception:
            ok = False
        results.append((name, ok))
    dt = time.time() - t0
    passed = sum(1 for _, ok in results if ok)
    red = [n for n, ok in results if not ok]
    return {
        "label": label,
        "passed": passed,
        "total": len(results),
        "red": red,
        "seconds": round(dt, 3),
    }


if __name__ == "__main__":
    out = run(sys.argv[1] if len(sys.argv) > 1 else "run")
    print(json.dumps(out, ensure_ascii=False))
