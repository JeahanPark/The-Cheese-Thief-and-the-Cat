# 요새 즉발 방패화 · 미끼 삭제 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 미끼를 지우고 요새를 "내 위치 중심 즉시 설치 · HP 180 · 회복 없음 · 80치즈 · 고양이 밀어내기" 방패로 바꾼다.

**Architecture:** 요새는 기존 `SpawnStructure`(발밑 즉시 스폰) 경로로 옮기고, 새 `FortressShield`(요새 프리팹) 컴포넌트가
`OnNetworkSpawn`에서 겹친 몸을 찾아 새 `BodyPush`(플레이어 프리팹)의 소유자 RPC로 밀어낸다(이동은 소유자 권위).
밀어내기 계산은 순수 함수 `FortressPushLogic`(EditMode). 미끼는 코드·프리팹·레이어·봇·QA에서 전부 걷어낸다.

**Tech Stack:** Unity 6 · NGO 2.12 · CSV(ShopInfo/BalanceInfo) · wesqa

---

### Task 1: 데이터 (CSV)
- Modify `Assets/Resources/CSVInfo/ShopInfo.csv` — Decoy 행 삭제, Fortress 220→80
- Modify `Assets/Resources/CSVInfo/BalanceInfo.csv` — FortressHp 300→180, `FortressHealPerTick`·`FortressHealRadius`·`FortressHealTickSeconds`·`DecoyLifetimeSeconds` 컬럼 삭제
- [ ] 편집 → 유니티 `Tools/CTC/Convert CSV → Info` (Task 8에서 일괄)

### Task 2: 순수 로직 + 테스트 (TDD)
- Create `Assets/Scripts/Economy/FortressPushLogic.cs` — `static bool PushDelta(Vector3 _center, float _yawDeg, Vector3 _body, float _bodyRadius, bool _isCat, out Vector3 _delta)`
  - 로컬 좌표(yaw 역회전). HALF 1.2 · WALL 0.2 · DOOR_HALF 0.5 · MARGIN 0.2
  - 고양이: |x|,|z| < HALF+r 이면 가장 가까운 변 바깥(HALF+r+MARGIN)으로
  - 쥐: 벽 띠(HALF-WALL-r < |axis| < HALF+r, 다른 축 |·| < HALF+r)에 있을 때만 — 단 z벽 띠이고 |x| < DOOR_HALF면 문이라 안 밈. 안/밖 가까운 쪽으로
- Create `Assets/Scripts/Tests/EditMode/FortressPushLogicTests.cs` — 고양이 중심→밖 · 고양이 밖→안 밈 · 쥐 중심→안 밈 · 쥐 벽 띠→밀림 · 쥐 문 자리→안 밈 · yaw 90 회전 반영
- Modify `ShopLogicTests`(Fortress 80 · 쥐 카탈로그 5 · IsPlaceable Fortress false · Decoy 제거) · `FunLogicTests`(FORTRESS_HP 쿼터 = 45) · `NetworkObjectHashTests`(MouseDecoy 제거)

### Task 3: 런타임 — 밀어내기 컴포넌트
- Create `Assets/Scripts/Player/BodyPush.cs` (NetworkBehaviour, RequireComponent CC) — `ServerPush(Vector3 _delta)` → `[Rpc(SendTo.Owner)] PushRpc` → `CC.Move`
- Create `Assets/Scripts/Economy/FortressShield.cs` (NetworkBehaviour, 요새 프리팹) — `OnNetworkSpawn` 서버: `FindObjectsByType<PlayerMovement>` 중 반경 3m 안 몸에 `FortressPushLogic.PushDelta` → `BodyPush.ServerPush`. 고양이가 밀리면 `SfxPlayer.PlayAt(FortressPlace)`. 봇·QA의 "요새 마커"도 이것.
- Delete `Economy/FortressHealZone.cs`·`DecoyLifetime.cs`·`DecoyLayers.cs`

### Task 4: 상점·HUD
- `ShopLogic` — `FORTRESS_HEAL_*`·`DECOY_LIFETIME_SECONDS` 삭제, `IsPlaceable` = Warehouse만
- `ShopController` — `m_DecoyPrefab` 삭제, `CanApply`/`Apply`/`PrefabOf` Decoy 제거, `SpawnStructure`: 요새는 `transform.position`(PlacementFits 무시) + `LookRotation(FlatForward())`(입구 = 보는 방향), 창고는 기존 앞 1.5m
- `ShopHotbar` — ITEM_LABELS/ITEM_HINTS에서 미끼 제거, 요새 힌트 "3방 버티는 즉석 벙커", `UsageOf` Fortress "요새 설치 — 고양이는 못 들어온다 · 세 방이면 부서진다", Decoy 케이스 삭제
- `MousePopgun` — DecoyLifetime 통과 규칙 삭제
- `SfxPlayer` — `DecoyPlace` 삭제 · `LobbyCanvasBuilder` 클립 배선 한 줄 삭제 · `BotSenseLogic` case 삭제

### Task 5: 씬 생성기
- `SetupNetworkingScene.Run` — decoyLayer·BakeDecoyMatrix·미끼 프리팹 생성 삭제, `_decoyPrefab` 인자 삭제, 쥐 레이어는 유지(`MOUSE_LAYER` 상수 인라인)
- `EditorAssetUtil` — `BakeDecoyMatrix` 삭제, `EnsureLayer` 주석 정리
- `StructurePrefabFactory` — `CreateStructurePrefab` 기본 sfx FortressPlace, 요새 프리팹에 `FortressHealZone`→`FortressShield`
- `PlayerPrefabFactory` — `_decoyPrefab` 인자·SetRef 삭제, `BodyPush` 추가
- `SceneComposer` — `_decoyPrefab` 인자·등록·해시 검사 삭제
- Delete `Assets/Prefabs/MouseDecoy.prefab(.meta)`

### Task 6: 봇
- `BotShopper.BuyDecoy` → `BuyFortressShield()` (Buy Fortress) · `BotAgent.PlaceDecoyForFlee` → `PlaceFortressForFlee` (쿨 10s) · `TryGetRefugeFortress` → `FortressShield` · `BotBrainTrees` 호출·주석 · `MouseBotLogic` 주석

### Task 7: QA 훅·스위트
- `TestManager` — `NearestDecoy`→`NearestFortress(FortressShield)`, `TestGetDecoyPos/Distance`→`TestGetFortressPos/Distance`, `TestBuyDecoy`·`TestPlaceDecoyAt`·`TestPlaceFortressAt` 삭제 · `MultiplayerQaProbe` TestMpBuyDecoy 삭제, FortressHealZone→FortressShield
- `tools/wesqa/tests/qa_v36.py` §3·4 → 요새: 고양이를 m2 옆 1.0m에 텔레포트 → `TestBuyFortress` → 요새 pos ≈ m2 pos(<0.5) · 고양이 거리 > 1.4 · m2 거리 < 0.6 · 고양이 0.35s 밀기 → 이동 < 1.2(못 들어감)

### Task 8: 유니티 검증
- [ ] refresh → Convert CSV → 컴파일 0 → Setup Networking Scene → EditMode 전부 → setup_match → qa_v36 → 봇 3판(`run_matches.py --matches 3`)

### Task 9: 문서·커밋
- 규칙과-현황.html(상점 표·소리 표 미끼 행·요새 문구) 재발행 · 코드 다이어그램(워크플로우 ② · 클래스 ② FortressShield/BodyPush) · handoff · CLAUDE.md
