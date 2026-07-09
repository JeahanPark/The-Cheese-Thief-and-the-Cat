# coding=utf-8
"""심은 버그(seed) 카탈로그 — 실 게임 UI 트리(snapshot.json)를 변조하는 mutator들.
각 seed = (이름, 변조함수(tree)->tree, 설명). 변조는 deep copy 위에서 수행."""
import copy


def _find_remove(tree, name):
    """name 노드를 트리에서 제거(부모의 children에서 drop)."""
    def rec(node):
        kids = node.get("children") or []
        node["children"] = [k for k in kids if (k.get("payload") or {}).get("name") != name]
        for k in node["children"]:
            rec(k)
    rec(tree)
    return tree


def _find_rename(tree, name, new):
    def rec(node):
        p = node.get("payload") or {}
        if p.get("name") == name:
            p["name"] = new
            node["name"] = new
        for k in (node.get("children") or []):
            rec(k)
    rec(tree)
    return tree


def _retype(tree, name, new_type):
    def rec(node):
        p = node.get("payload") or {}
        if p.get("name") == name:
            p["type"] = new_type
        for k in (node.get("children") or []):
            rec(k)
    rec(tree)
    return tree


def seed_catalog():
    return [
        ("StartButton 삭제", lambda t: _find_remove(t, "StartButton"),
         "버튼 누락 회귀 — 존재 단언이 잡아야 함"),
        ("ExitButton 삭제", lambda t: _find_remove(t, "ExitButton"),
         "버튼 누락 회귀"),
        ("StartButton 이름변경", lambda t: _find_rename(t, "StartButton", "StartBtn"),
         "위계/네이밍 변경 회귀 — 셀렉터가 못 찾아야 함"),
        ("StartButton 타입손상", lambda t: _retype(t, "StartButton", "Image"),
         "클릭불가 회귀 — type==Button 단언이 잡아야 함"),
    ]


def mutate(tree, mutator):
    return mutator(copy.deepcopy(tree))
