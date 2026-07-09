# coding=utf-8
import os
from wesqa.report import Report


def test_report_writes_html(tmp_path):
    r = Report("스모크")
    r.step("로그인 화면", True)
    r.step("나무 3개", False, note="기대 3, 실제 2")
    out = os.path.join(str(tmp_path), "report.html")
    r.write(out)
    html = open(out, encoding="utf-8").read()
    assert "스모크" in html
    assert "로그인 화면" in html
    assert "PASS" in html and "FAIL" in html
    assert "기대 3, 실제 2" in html
