# coding=utf-8
"""경량 HTML 리포트 — 스텝(pass/fail + 선택 스크린샷)을 한 파일로 묶는다."""
import html as _html
import time


class Report:
    def __init__(self, title):
        self.title = title
        self.steps = []

    def step(self, name, passed, note="", screenshot_b64=None):
        self.steps.append({
            "name": name, "passed": bool(passed),
            "note": note, "shot": screenshot_b64,
        })

    def write(self, path):
        rows = []
        for s in self.steps:
            badge = "PASS" if s["passed"] else "FAIL"
            color = "#1a7f37" if s["passed"] else "#cf222e"
            shot = ""
            if s["shot"]:
                shot = ('<br><img style="max-width:480px;border:1px solid #ddd" '
                        'src="data:image/jpeg;base64,{}">').format(s["shot"])
            rows.append(
                '<tr><td><b style="color:{}">{}</b></td><td>{}</td>'
                '<td>{}{}</td></tr>'.format(
                    color, badge, _html.escape(s["name"]),
                    _html.escape(s["note"]), shot))
        passed = sum(1 for s in self.steps if s["passed"])
        doc = (
            "<!doctype html><meta charset='utf-8'>"
            "<title>{title}</title>"
            "<body style='font-family:sans-serif;max-width:900px;margin:2em auto'>"
            "<h1>{title}</h1>"
            "<p>{p}/{n} PASS · {ts}</p>"
            "<table style='border-collapse:collapse' border='1' cellpadding='6'>"
            "<tr><th>결과</th><th>스텝</th><th>비고</th></tr>"
            "{rows}</table></body>"
        ).format(title=_html.escape(self.title), p=passed, n=len(self.steps),
                 ts=time.strftime("%Y-%m-%d %H:%M:%S"), rows="".join(rows))
        open(path, "w", encoding="utf-8").write(doc)
        return path
