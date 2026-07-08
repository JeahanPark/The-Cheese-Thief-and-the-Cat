# wesqa — WES QA 자동화 (Poco 최소 포크)

`poco/`는 AirtestProject/Poco (Apache-2.0)의 최소 포크다. 드라이버(android/ios/unity3d 등)와
airtest 의존을 제거하고, 게임 내 자작 C# 서버(`Assets/WesQA/`)에 직접 TCP로 붙는다.

## 사용
    from wesqa import WesPoco
    game = WesPoco(instance=0)          # localhost:5001
    game('btn_inventory').click()       # (M2)
    assert game('wood_count').get_text() == "3"
