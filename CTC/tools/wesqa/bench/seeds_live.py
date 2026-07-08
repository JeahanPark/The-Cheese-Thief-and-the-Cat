# coding=utf-8
"""실 게임 seed 주입(Invoke 브리지 사용) + 동작 검증 — 효과측정 자동주입 토대(M4).
M1 mutation(스냅샷 변조)과 달리 실제 게임 상태를 TestManager로 바꾼 뒤 wesqa로 단언한다.
주의: 대부분의 TestManager 시나리오는 인게임(InGameController) 상태 전제 —
이 모듈은 '인게임 진입 후' 실행을 가정한다. 현 단계는 브리지 왕복(호출 성공)을 검증한다.
"""


def live_seed_demo(game):
    """반환: (이름, 통과여부) 리스트. 인게임이 아니면 호출은 성공하나 게임효과는 없을 수 있음."""
    results = []
    try:
        game.invoke("SimulateAddItem", _itemId=1)
        results.append(("SimulateAddItem 호출", True))
    except Exception:
        results.append(("SimulateAddItem 호출", False))
    try:
        game.invoke("SimulateInventoryToggle")
        results.append(("SimulateInventoryToggle 호출", True))
    except Exception:
        results.append(("SimulateInventoryToggle 호출", False))
    return results
