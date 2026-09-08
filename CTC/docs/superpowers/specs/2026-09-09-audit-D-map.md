# 전체 검수 — 도메인 D: 맵·길찾기·봇 (2026-09-09)

> **한 줄 결론 — 굽는 코드와 구운 데이터는 한 톨도 안 어긋난다. 어긋나는 것은 「데이터가 서로 어디에 놓였나」이고, 그걸 보는 검사는 상점 하나뿐이라 정식 기본 맵의 쥐 스폰 하나가 궤짝 벽 속에 박혀 있다.**

- 범위: `Assets/Scripts/Map/`(32) · `Ai/`(19) · `Bots/`(17) · `Assets/Prefabs/Maps/Layout_*.asset`(4) · 맵 프리팹 6종.
- **정적 분석만이다.** 사용자 지시로 유니티 에디터·플레이모드를 안 썼다. 좌표 검산은 `.asset` YAML을
  직접 파싱해 `WarehouseLayoutBuilder`의 상수 배열로 **재계산**하고, `NavGridBaker`의 스탬프 규칙을
  같은 셀 크기(0.5 m)로 **다시 구현해** 돌린 결과다 — 실행 결과가 아니다.
- 봇 감사([2026-09-09-bot-audit.md](2026-09-09-bot-audit.md))가 이미 본 것은 안 되풀이한다. §6이 그 뒤 상태만 갱신한다.
- 아무것도 고치지 않았다.

---

## 1. 책임 지도

| 층 | 누가 | 무엇을 | 원본 |
|---|---|---|---|
| 맵 원본(손) | `Editor/WarehouseLayoutBuilder` | 도면 → `Layout_Warehouse01.asset` 굽기 | 상수 배열(파일 안) ← `docs/design-vault/맵 시안 v2 창고.html` |
| 맵 원본(절차) | `Map/MapGenerator` + `MapCandidatePool` | 시드 → 방·복도·치즈·요새 | `MapCandidatePool*.asset` |
| 생성 게이트 | `Map/MapValidator` | V3·V4·V6·V9·V11 | **절차 맵에만 돈다** |
| 데이터 그릇 | `Map/MapLayout` + `MapData` | Rooms·Corridors·Cheese·Fortresses·Alcoves·Boxes·Obstacles·Props·Lights·Shops | — |
| 씬 굽기 | `Map/MapBuilder` | 바닥 타일·경계벽·차단물 박스·치즈 지점 → `GeneratedMap` | `MapLayout` |
| 옷 | `Map/MapDresser` | 선반·배럴·빛웅덩이 비주얼 교체 | `MapLayout.Props`·`.Lights` |
| 맵 고르기 | `Map/MapDirector`(Worker) + `MapCatalogLogic` | 인덱스 복제 → `MapBuilder.Build` | `SetupNetworkingScene.MapLayoutPaths` |
| 스포너 — 쥐/고양이 | `Lobby/PlayerSpawner` | `Fortresses[n%N]` 입구 **앞 1.8 m** | `MapLayout.Fortresses` |
| 스포너 — 벽감 | `Map/AlcoveSpawner` | 좌표·yaw·페이로드 고정, **내용물만 셔플** | `MapLayout.Alcoves` |
| 스포너 — 상자 | `Map/LootBoxSpawner` | `MapLayout.Boxes` | **모든 레이아웃이 빈 목록**(§5 M8) |
| 스포너 — 상점 | `Map/ShopSpotSpawner` + `ShopSpotLogic` | 후보 8 → 시드로 4 | `MapLayout.Shops` · `BalanceInfo` |
| 스포너 — 요새/창고 | 없음(쥐가 산다) | `FortressPlaceCost 80` · `WarehousePlaceCost 100` | `BalanceInfo` |
| 스포너 — 직업권 | `Map/Alcove.SpawnBox` → `LootBox` | 벽 뒤 상자 안에만 | `AlcovePlacement.BoxContents` |
| 격자 A\* | `Ai/Grid/NavGridBaker`→`NavGrid`→`GridPathfinder`, `NavGridService` 전역 1벌 | 셀 0.5 m · 이름 접두로 지형 판정 | `GeneratedMap`의 BoxCollider |
| 격자 자국(판 중) | `Ai/Grid/NavGridBlocker` | 요새·창고·벽감·상자 · 요새 문 = 쥐 전용 | 프리팹 4종 |
| 봇 두뇌 | `Bots/BotAgent`·`BotBrainTrees`·`BotCommander`·`MouseBotLogic`·`CatBotLogic` | — | — |
| 봇 다리 | `Bots/BotNavigator` → `Ai/NavRoute` → `GridPathfinder` | — | — |
| 계측 | `Bots/TestBotManager` + `Testing/TestManager.*` | QA 훅 약 60개 | — |

**정식 맵은 `Layout_Warehouse01` 하나다.** Sketch(15)·Expanded(21)는 `HalfExtent < 60`이라 배포에서 안 뜨고,
Large01은 `DevOnly = 1`이다(`MapCatalogLogic.IsSelectable`). 아래 검산의 무게는 전부 창고 맵에 있다.

---

## 2. 레이아웃 데이터 정합성 ← 핵심

### 2-1. 상수 배열 ↔ 구운 `.asset` — **어긋남 0건**

`WarehouseLayoutBuilder`의 `SHELVES`·`CRATE_WALLS`·`BARRELS`·`BLOCKS`·`CHEESE`·`FORTS`·`SHOPS`·`ALCOVES`를
`WarehouseLogic.SplitByGaps`/`ShelfBox`와 함께 다시 계산해 YAML과 필드 단위로 비교했다.

| 목록 | 코드 | 에셋 | 차이 |
|---|:--:|:--:|:--:|
| Props (선반 조각 + 궤짝 벽 + 배럴 + 가구) | 77 | 77 | **0** (Name·Kind·Pos·Size·Height 전부 일치) |
| Cheese | 14 | 14 | 0 |
| Fortresses | 8 | 8 | 0 |
| Shops | 8 | 8 | 0 |
| Alcoves | 6 | 6 | 0 |
| Rooms · Corridors · Boxes · Obstacles | 0 | 0 | 0 (손 배치 갈림 정상) |

부수 확인: 경계벽 겹침 0건(`InsideBounds`, inset 0.25) · 상점 후보 최소 간격 **28.0 m** ≥ 25 · 등급 다중집합 {1..7}×2 ·
요새–치즈 최소거리 12.04 m ≥ 3.7. 다른 세 레이아웃도 등급·이격 게이트를 통과한다(Sketch 3.81 · Expanded 5.26 · Large 14.96).

> 즉 **「굽는 코드가 데이터를 배신한다」는 없다.** 함정 2(씬 빌더가 손 배치를 덮음)도 막혀 있다 —
> `SetupNetworkingScene`이 `if (layout.HandPlaced) continue;`로 자동 계획을 건너뛴다.

### 2-2. 좌표가 서로 겹치거나 벽에 박힌 것 — **7건**

격자(0.5 m)를 `NavGridBaker`와 같은 규칙으로 다시 굽고 각 지점의 셀 상태·여유(clearance)를 쟀다.
격자 전체는 **한 덩어리**다(자유 셀 49,784 / 도달 100% / 섬 1개) — 끊긴 구역은 없다.

| # | 무엇 | 어긋남 | 근거 |
|:--:|---|---|---|
| **L1** | **쥐 스폰 #2** — 요새자리 `(54, 54)` yaw 225 → 스폰 `(52.73, 52.73)` | **궤짝 벽 `C-y7`(x 46~56 · z 46~54 · 높이 2.4) 속 1.27 m.** 격자 셀 `blocked=1`. 요새자리 중심 `(54,54)`도 같은 벽 위 | `Assets/Scripts/Editor/WarehouseLayoutBuilder.cs:47` (`("y7", Rect(46,46,10,8))`) · `:88` (`(54,54), 225f`) · `Assets/Scripts/Lobby/PlayerSpawner.cs:42-44` |
| **L2** | **쥐 스폰 #0** — `(-54, 54)` yaw 135 → 스폰 `(-52.73, 52.73)` | 선반 `S-p1-1`(x −52.5~−51.5) 면까지 **0.227 m**. 쥐 CC 반지름 **0.22** — 여유 **7 mm**. 격자 여유 0.25 m | `WarehouseLayoutBuilder.cs:29`·`:87` · `Assets/Prefabs/PlayerMouse.prefab:84` |
| **L3** | **치즈 #10 `(-30,-48)` · #12 `(-48,-30)`** | 배럴 면까지 0.8 m. 파밍 반경 2 m·원반(지름 3 m)이 배럴을 파고든다. 격자 여유 **0.25 m** — 고양이 몸(0.35)이 지점 중심에 못 선다 | `WarehouseLayoutBuilder.cs:55`·`:81-82` |
| **L4** | **치즈 #3 `(-20,-20)`** | 파밍 반경 2 m가 선반 `S-sW-1`(x −18.5~−17.5)을 1.5 m 지점부터 문다 | `WarehouseLayoutBuilder.cs:35`·`:80` |
| **L5** | **강아지 벽감 #2 `(58,36)` yaw 270** | 개가 나오는 자리 `(56.4, 36)`가 궤짝 벽 `C-y5`(x 48~56) 면에서 **0.4 m**. 개 CC 0.35 → 여유 5 cm. **그 셀은 격자에서 막힌 셀**이라 첫 경로가 스냅으로 시작한다 | `WarehouseLayoutBuilder.cs:46`·`:115` · `Assets/Scripts/Map/Alcove.cs:158,166` · `Assets/Prefabs/Dog.prefab:138` |
| **L6** | **상점 후보 #2 `(-40, 36)`** | 원반(r 3)은 깨끗하지만 **설치 사거리 링**(원반 3 + `PlaceGhostMaxMeters` 4 = 7 m)이 선반 셋을 문다. 폭 7 m 골목 한가운데다 | `WarehouseLayoutBuilder.cs:103` · `Assets/Resources/CSVInfo/BalanceInfo.csv` (`PlaceGhostMaxMeters=4`) |
| **L7** | 홀 네 모서리에서 선반 조각 **8쌍이 겹친다**(`S-hallN-0`↔`S-hallW-2` 등) | **의도로 보인다**(ㄱ자 이음). 다만 어디에도 「모서리는 겹쳐도 된다」가 적혀 있지 않아, 겹침 검사를 넣는 순간 오탐 8건이 된다 | 재계산 결과 |

**L1이 이 감사의 최대 건이다.** 쥐가 3명 이상이면 `Fortresses[2]`가 반드시 쓰인다(`nth % 8`).
그 쥐는 높이 2.4 m 궤짝 상자 **안**에서 판을 시작한다.

### 2-3. 스포너가 읽는 필드 ↔ 레이아웃이 채우는 필드

| 스포너 | 읽는 필드 | 손 배치(창고) | 절차(Large 등) | 갈림 처리 |
|---|---|:--:|:--:|---|
| `PlayerSpawner` | `Fortresses` | 8 | 8 | 갈림 없음 — 양쪽 다 있다 |
| `AlcoveSpawner` | `Alcoves` | 6 | 4~6 | 갈림 없음 |
| `ShopSpotSpawner` | `Shops` + `Rooms` | Shops 8 | Shops 0 → `Rooms` | **`ShopSpotLogic.Resolve`가 `HandPlaced`로 갈라 탄다 ✓** |
| `LootBoxSpawner` | `Boxes` | **0** | **0** | **양쪽 다 비어 있다 — 이 경로는 아무것도 안 낳는다**(M8) |
| `MapBuilder.BuildFromLayout` | `HandPlaced` | Props | Rooms/Corridors | ✓ |
| `MapBuilder.BuildObstacles` | `Obstacles` + `Props(Barrel)` | 배럴 21 | Obstacles | ✓ |
| `MapDresser` | `Props`·`Lights` | 77 / 7 | 0 / 0 | 절차 맵은 옷이 없다(설계상) |
| `MinimapPainter` | `Rooms` 없으면 `Props` | Props | Rooms | ✓ (테스트 있음) |

**갈림 처리는 전부 맞다.** 문제는 갈림이 아니라 §2-2의 좌표다.

---

## 3. 이미 검증되는 것

| 층 | 무엇 | 어디서 |
|---|---|---|
| 순수 규칙 — 맵 | `SplitByGaps`·`ShelfBox`·`InsideBounds` 6건 | `WarehouseLogicTests` |
| 순수 규칙 — 게이트 | V3·V4·V6·V9·V11 9건 | `MapValidatorTests` |
| 순수 규칙 — 생성 | 결정성·필수 방·G1 고정·등급 완비·**시드 100개 성공률** | `MapGeneratorTests` |
| 순수 규칙 — 상점 | 개수·간격·완화·시드 재현·손 배치 갈림 23건 | `ShopSpotLogicTests` |
| 순수 규칙 — 격자 | 셀 왕복·여유·봉인·몸 굵기·쥐 문·부수기·대각 코너컷·예약 **20건** | `NavGridTests` |
| 순수 규칙 — 방 모듈 | 문 방향 판정 | `RoomModuleLogicTests` |
| 순수 규칙 — 셔플 | 순열 결정성·항등 6건 | `TicketShuffleLogicTests` |
| 에셋 1건 | `Layout_Warehouse01`을 **실제로 로드해** 미니맵을 칠한다(방 0·복도 0·Props>0 확인) | `MinimapPainterTests` |
| 스폰 공식 | 고양이 중앙선 · 쥐 요새 앞 1.8 m · 웨이브 옆걸음 | `LobbyRoleLogicTests:93-114` (**좌표는 테스트가 만든 가짜 요새다**) |
| 봇 순수 규칙 | `MouseBotLogic` 29 · `CatBotLogic` 9 · `BotSenseLogic` · `BtNode` 11 · `MouseCombatLogic` 17 | EditMode |
| 플레이 | 봇 건강검사(끼임·중복목표·재계획) · `qa_v36/37/38/44/50` | `tools/wesqa/` |
| 격자 캐내기 | `TestGetNavGridInfo` · `TestTracePath` · `TestDumpGrid` · `TestGetNavStats` · `TestNavMeshIslands` | `Assets/Scripts/Testing/TestManager.NavGrid.cs` |

---

## 4. 검증 공백

| 심각도 | 무엇이 안 잡히나 | 왜 위험한가 | 근거 |
|:--:|---|---|---|
| **치명** | **구운 레이아웃의 지점이 차단물 안에 있는지 아무도 안 본다.** 굽기 검사는 ① 프롭 vs 경계벽 ② 상점 원반 vs 프롭 ③ 상점 간 간격 **셋뿐**이다 — 스폰·치즈·벽감은 무검사 | L1이 정확히 이 구멍으로 들어왔다. 상점만 검사가 있는 이유는 「상점으로 세 번 물려서」다. 같은 부류의 **네 번째가 이미 데이터에 있다** | `Assets/Scripts/Editor/WarehouseLayoutBuilder.cs:155-185` |
| **치명** | **`MapValidator`가 손 배치 맵에 안 돈다.** 게이트는 `MapGenerator.Validate`(절차 경로) 안에서만 호출된다 | 정식 기본 맵이 유일하게 게이트를 안 지나는 맵이다 | `Assets/Scripts/Map/MapGenerator.cs:205-211` — 호출자가 생성기뿐 |
| **높음** | **벽감 내용물 셔플의 보존이 검증되지 않는다.** `TicketShuffleLogicTests` 6건은 전부 `Permutation`만 부른다 — 「셔플 뒤에도 직업권 2장·치즈 총량이 그대로인가」가 0건 | M2가 그 틈으로 들어왔다(판마다 5/6 확률로 물건 하나가 사라진다) | `Assets/Scripts/Tests/EditMode/TicketShuffleLogicTests.cs` |
| **높음** | **격자와 실제 콜라이더가 같은 것을 보는지 검증이 없다.** `NavGridTests` 20건은 전부 손으로 만든 작은 격자다 — `NavGridBaker.Bake`를 **실제 씬 계층에 대고** 부르는 테스트가 0건 | M4(`Deco_*` 콜라이더 누락)가 이 틈이다. 물리는 막는데 A\*는 뚫린 줄 안다 | `Assets/Scripts/Tests/EditMode/NavGridTests.cs` — `Bake` 호출 0회 |
| **높음** | **스폰 자리 유효성**을 아무 층도 안 본다. `LobbyRoleLogicTests`는 가짜 요새 리스트를 넣어 **공식만** 검증한다 | 공식은 맞는데 그 공식이 뱉는 좌표가 벽 속이다(L1) | `Assets/Scripts/Tests/EditMode/LobbyRoleLogicTests.cs:103-114` |
| 보통 | **도달 가능성**을 판정하는 코드·테스트가 런타임에 없다. `MapValidator.AllConnected`는 방-복도 그래프라 손 배치 맵(방 0)에는 **정의상 무의미**하다 | 지금 창고는 한 덩어리지만(재계산으로 확인), 도면을 고치면 조용히 갈라진다 | `Assets/Scripts/Map/MapValidator.cs:28-44` |
| 보통 | **치즈 총량 불변식** 훅(`TestGetMapTotalCheese`·`TestGetMapLootCheese`)이 **레이아웃 값을 읽는다** — 스폰 뒤 실제 상자 내용물을 안 센다 | M2가 이 훅으로는 안 잡힌다 | `Assets/Scripts/Testing/TestManager.Economy.cs:311` |
| 보통 | 격자 스탬프의 **재발행 내성**이 없다. `NavGridBlocker.Stamp`는 `OnEnable` 1회 + `!NavGridService.Ready`면 조용히 넘긴다 | 맵을 다시 지으면(`Publish`) 살아 있는 구조물의 자국이 새 격자에 없다. 그 뒤 파괴되면 `Undo`가 **남의 셀 카운트**를 깎는다 | `Assets/Scripts/Ai/Grid/NavGridBlocker.cs:46,61` · `Assets/Scripts/Ai/Grid/NavGridService.cs:28-33` |
| 낮음 | 프롭끼리 겹침을 보는 검사가 없다(L7이 의도인지 사고인지 데이터만으로는 구분 불가) | 새 도면에서 진짜 겹침이 나도 안 걸린다 | 굽기 검사에 없음 |

---

## 5. 발견한 결함

| # | 심각도 | 무엇 | 근거 | 상태 |
|:--:|:--:|---|---|:--:|
| **M1** | **치명** | **쥐 스폰 #2가 궤짝 벽 속이다.** `Fortresses[2] = (54,54) yaw 225` → 스폰 `(52.73,52.73)`, 궤짝 벽 `C-y7`(x 46~56 · z 46~54) 내부 1.27 m. 쥐 3명 이상이면 반드시 쓰인다 | `WarehouseLayoutBuilder.cs:47,88` · `PlayerSpawner.cs:42-44` · 격자 재계산 `blocked=1` | **확인**(물리 반응은 미확인) |
| **M2** | **높음** | **벽감 내용물 셔플이 물건을 없앤다.** `AlcoveSpawner`가 6개 자리의 `BoxContents`를 순열로 교환하는데 **#2는 강아지 벽감(내용물 빈 목록)**이다. `perm[2] ≠ 2`(확률 5/6)이면 ① 어떤 상자 벽감이 **빈 목록**을 받아 아무것도 안 떨어뜨리고 ② 사라진 물건은 강아지 벽감으로 가 **영영 안 나온다**. 잃는 것은 치즈 250(3/6) 또는 **직업권 1장**(2/6). `MapBuilder`는 그 250을 지점 매장량에서 이미 뗀 뒤다 | `Assets/Scripts/Map/AlcoveSpawner.cs:36-58` · `Assets/Scripts/Map/Alcove.cs:134-146` · `Assets/Scripts/Map/AlcoveLogic.cs:29-40` · `Assets/Scripts/Map/MapBuilder.cs:547-549` | **확인** |
| **M3** | **높음** | **셔플이 「직업권 둘을 가장 먼 두 벽에」를 깬다.** `AlcoveFactory.FillBoxContents`가 최원거리 쌍을 골라 넣는데(주석: "나란히 두면 한 사람이 둘 다 먹는다"), 스폰 셔플이 그 배치를 무작위로 흩는다 | `Assets/Scripts/Editor/Setup/AlcoveFactory.cs:217-234` vs `Assets/Scripts/Map/AlcoveSpawner.cs:36-40` | **확인**(설계 의도 충돌) |
| **M4** | **높음** | **`Deco_*` 콜라이더가 길찾기 양쪽에서 빠진다.** `Module_Big_Pantry01/Deco_Pumpkin_0~2`, `Module_Small_Cellar01/Deco_Walnut_0~1`·`Deco_Almond_0`는 BoxCollider를 갖는데 이름 접두가 `Wall/Boundary/Obstacle/Corridor`가 아니다 → 격자에 안 찍히고, **`CollectSources`에서는 `area = 0`(걸을 수 있음)** 으로 들어가 폴리곤에 **올라갈 수 없는 걷기 면**을 만든다 | `Assets/Scripts/Ai/Grid/NavGridBaker.cs:22-24,47` · `Assets/Scripts/Map/MapBuilder.cs:170-172` · 프리팹 스캔 | **확인**(Large01 전용 = 개발 맵) |
| **M5** | 보통 | **쥐 스폰 #0의 여유가 7 mm다.** `(-52.727, 52.727)` vs 선반 면 `-52.5`, 쥐 CC 0.22 | 위 · `Assets/Prefabs/PlayerMouse.prefab:84` | **확인**(끼임 여부는 의심) |
| **M6** | 보통 | **치즈 지점 둘의 중심에 고양이가 못 선다.** #10·#12의 격자 여유 0.25 m < 0.35. 파밍 원반이 배럴을 문다 | 격자 재계산 · `WarehouseLayoutBuilder.cs:55` | **확인** |
| **M7** | 보통 | **강아지가 0.4 m 틈으로 나온다.** 벽감 #2 앞 `(56.4,36)`은 격자에서 **막힌 셀** → 첫 경로가 `TryFindNearestOpen` 스냅으로 시작한다 | `Assets/Scripts/Map/Alcove.cs:158` · `Assets/Scripts/Ai/Grid/GridPathfinder.cs:111-113` | 확인(격자) / **의심**(행동) |
| **M8** | 보통 | **상자 경로가 죽어 있다.** `BOXES_PER_MAP = 0`이고 손 배치 맵은 `SetHandPlaced`가 `m_Boxes`를 비운다 → 네 레이아웃 전부 `Boxes = []`. `LootBoxSpawner`·`LootBoxLogic.TotalCheese(Boxes)`·`BoxPlacement`·`ObstacleFactory`의 상자 회피가 전부 빈 입력으로 돈다 | `Assets/Scripts/Editor/Setup/AlcoveFactory.cs:62` · `Assets/Scripts/Map/MapLayout.cs` (`SetHandPlaced`) · 4개 에셋 모두 `m_Boxes: []` | **확인**(설계 의도로 보이나 죽은 경로가 남았다) |
| **M9** | 보통 | **격자 재발행에 스탬프가 못 따라간다.** `NavGridBlocker`는 `OnEnable` 1회 + `!Ready`면 무시. `NavGridService.Publish`가 격자를 갈아도 재스탬프 훅이 없고, 이후 `Undo()`는 **새 격자**에서 카운트를 깎는다 | `NavGridBlocker.cs:38,46,61,100` · `NavGridService.cs:28-33` · `Assets/Scripts/Ai/Grid/NavGrid.cs:256-269` | **의심**(로비 재빌드 시점엔 구조물이 없어 현재는 안 터질 수 있다) |
| **M10** | 낮음 | 상점 후보 #2가 폭 7 m 골목 한가운데다 — 원반은 들어가지만 설치 링(7 m)의 절반이 선반이다 | 격자 재계산 · `BalanceInfo.PlaceGhostMaxMeters=4` | **확인**(설계 판단 필요) |
| **M11** | 낮음 | `Module_Big_Pantry01`의 기둥이 `Pillar_NE/SE/SW` 셋뿐이다(NW 없음) | 프리팹 스캔 | 확인(외관) |

### 함정 재점검 (CLAUDE.md「조용히 깨지는 함정」)

| 함정 | 이 도메인의 현재 상태 |
|---|---|
| **2** 씬 빌더가 손 배치를 덮음 | **막혀 있다** — `SetupNetworkingScene`가 `if (layout.HandPlaced) continue;` |
| **5** 한 머티리얼을 UV 규약이 다른 메시가 나눠 씀 | **해소** — 맵 지오메트리는 트리플래너(`_TileMeters`)라 UV를 안 쓴다. 프롭은 `SdfMesher.PlanarUvs`(1 = 1 m) 단일 규약, `PropTextureBinder`가 문서화 |
| **6** `new Material(null)` 마젠타 | **양쪽 다 방어됨** — `Editor/Setup/EditorAssetUtil.cs:68-95` · `Editor/MapMaterialBaker.cs:29-33,95-97`이 셰이더 널 검사 + 구운 뒤 재확인 |
| **7** 같은 프레임에 껐다 켬 | 이 도메인엔 해당 자리 없음 |

---

## 6. 봇 감사 미해결 항목의 현재 상태

| # | 봇 감사가 말한 것 | 지금 | 근거 |
|:--:|---|:--:|---|
| A1 | 도망 중 자가 잠행 | **고쳐짐** — 기준이 「서로 시선이 통하는가」로 바뀜 | `c10d350` |
| A2 | 쥐가 반격 안 함 | **고쳐짐**(조건부 반격 + `TestSetBotCounterAttack`) | `c10d350` · `MouseCombatLogic` |
| A3 | 쥐 봇이 Lv1 5장에서 정지 | 봇 감사 범위 — 이번에 재확인 안 함 | — |
| A4 | 죽은 봇 길찾기 통계 미이월 | **고쳐짐** | `Assets/Scripts/Bots/TestBotManager.cs:373-384` |
| A5 | 시야 통계에 쥐 표본 섞임 | **고쳐짐** — `if (_agent.Role == RoleId.Cat)` | `TestBotManager.cs:362-369` |
| A6 | 사인 라벨 순환논법 | **고쳐짐** — 라벨 둘 기록 | `TestBotManager.cs:397-400` |
| **B1** | 고양이 배회 가지 도달 불가(지휘관이 명령을 안 지운다) | **그대로** — `BotOrders.Clear()`는 `SetCats`·`Stop`에서만 | `Assets/Scripts/Bots/BotCommander.cs:69,108` · `Assets/Scripts/Bots/BotBrainTrees.cs:103,121` |
| **B2** | 청각 비대칭 — `CatSkill`·`CatAdvance`가 쥐 가청 목록에서 빠짐 | **그대로** — `IsCatMade`는 여전히 5종(`PounceSwing`·`CatHurt`·`CatDeath`·`FortressHit`·`FortressBreak`) | `Assets/Scripts/Bots/BotSenseLogic.cs:92-100` |
| B3 | 쥐 봇이 고개를 못 돌린다 | 그대로(구조) | `Assets/Scripts/Player/PlayerMovement.cs:175-180` |
| B4 | 요새를 전역으로 안다 | 그대로 | `Assets/Scripts/Bots/BotAgent.cs` (`FindObjectsByType<FortressShield>`) |
| C1 | 도주 꺾기에 커밋이 없다 | 미확인 — A1 수정으로 조건이 바뀌어 재측정이 필요하다 | — |
| **C2** | 요새 경주 상수 1.3(실제 1.12) | **그대로** — 상수도 주석도 안 바뀜 | `Assets/Scripts/Bots/MouseBotLogic.cs:54-59,91` |
| **D1** | `NavGridBlocker`가 격자 없을 때 조용히 안 찍음 | **그대로** + 이번에 재발행 경로까지 확인(M9) | `NavGridBlocker.cs:46` |
| D2 | Box 콜라이더만 훑는다 | 그대로 | `NavGridBaker.cs:42` |
| **D3** | `Deco_*`가 격자에 안 찍힌다 | **그대로 + 확대** — NavMesh에서는 `area=0`(걷는 면)으로 들어간다(M4) | `MapBuilder.cs:170-172` |
| D4 | 봇 속도가 CSV, 몸은 프리팹 | 그대로 | `BotAgent.cs:195-202` |
| D5·D6 | 죽은 코드 · 도주가 예약 안 함 | 그대로 | — |
| E | 트리·배선 EditMode 0건 | **그대로** | 테스트 목록 |

---

## 7. 테스트 케이스 대장 (도메인 D)

**이 절은 일회성 체크리스트가 아니라 계속 쓰는 대장이다.** 규칙:

| 규칙 | 내용 |
|---|---|
| ID | `D-NN`. **번호를 재사용하지도, 밀지도 않는다.** 케이스가 없어지면 행을 남기고 「폐기(사유)」로 표시하고 번호는 비운다 |
| 본문 | **동작(behaviour)으로 쓴다.** 줄 번호·private 여부 같은 구조 사실은 「근거」·「필요한 훅」 칸에만 둔다 — 리팩터링으로 케이스가 썩지 않게 |
| 층 | **자동이 기본**이다. `EditMode`(에셋·순수 규칙) · `wesqa`(플레이) · `수동`(사람 눈이 실제로 필요한 것만) |
| 회귀 | 각 케이스는 **무엇이 깨지면 빨개지는가**를 한 줄로 갖는다. 이게 없는 케이스는 나중에 지워진다 |
| 중복 | 이미 있는 테스트와 겹치면 **겹친다고 적는다.** 새 케이스인 척하지 않는다 |
| 「지금 결과」 | 이 감사 시점(2026-09-09)에 그 케이스를 돌리면 어떻게 되는지. 새로 쓴 케이스가 처음부터 빨간지 아닌지를 남긴다 |

새 파일 `Assets/Scripts/Tests/EditMode/MapLayoutIntegrityTests.cs` 하나가 D-01~D-08을 담는다
(에셋을 실제로 로드하는 선례는 `MinimapPainterTests`).

---

### 7-1. 자동 — EditMode (구운 레이아웃의 불변식)

| ID | 검증할 동작 | 이 케이스가 잡는 회귀 | 필요한 훅 | 기존과 겹침 | 지금 결과 | 우선 | 근거 |
|:--:|---|---|---|---|:--:|:--:|---|
| **D-01** | 정식 맵의 **모든 쥐 스폰점**(요새자리 입구 앞 1.8 m)이 어떤 차단물 박스 안에도 없다 | 도면에 요새자리·선반·궤짝 벽을 추가·이동하다 스폰을 물건 속에 넣는다 | 없음 (`MapLayout` 로드 + `PlayerSpawner.SpawnPosition`은 이미 public static) | 없음 (`LobbyRoleLogicTests`는 **가짜 요새 리스트**로 공식만 본다) | **FAIL — 스폰 #2** | **1** | `Editor/WarehouseLayoutBuilder.cs:47,88` · `Lobby/PlayerSpawner.cs:42-44` |
| **D-02** | 모든 쥐 스폰점이 가장 가까운 차단물 면에서 **쥐 몸 반지름 + 0.3 m** 이상 떨어져 있다 | 스폰이 「기술적으로 밖」이지만 몸이 벽에 닿아 못 움직이는 자리로 밀린다 | 없음 (쥐 반지름은 `PlayerInfo.csv`에서 읽어 하드코딩을 피한다) | 없음 | **FAIL — 스폰 #0 (여유 0.227 m)** | **1** | 같은 곳 · `PlayerMouse.prefab:84` |
| **D-03** | **벽감 내용물 셔플이 총량을 보존한다** — 어떤 순열을 써도 ① 직업권 두 장이 여전히 **꺼낼 수 있는 벽**에 있고 ② 상자 벽감 중 빈 손이 없고 ③ 치즈 합이 셔플 전과 같다 | 강아지 벽감처럼 **내용물을 안 꺼내는 자리**가 섞임 대상에 남아 물건을 삼킨다 | `AlcoveSpawner`의 순열 적용을 **순수 함수로 노출**해야 한다(현재 스포너 안 private) | 부분 — `TicketShuffleLogicTests`는 순열 자체만 본다(보존은 0건) | **FAIL — 720 순열 중 600(=5/6)** | **1** | `Map/AlcoveSpawner.cs:36-58` · `Map/Alcove.cs:134-146` |
| **D-04** | 굽기 메뉴가 **지금 뱉는 값**이 디스크의 레이아웃 에셋과 같다 | 도면 상수만 고치고 굽기 메뉴를 안 돌린 채 커밋한다 | 없음 (Editor asmdef 안에 둔다) | 없음 | PASS (이 감사에서 수동으로 확인 — 77/14/8/8/6 전 필드 일치) | 2 | `Editor/WarehouseLayoutBuilder.cs` |
| **D-05** | **모든 치즈 지점**의 파밍 반경 안에 차단물이 없다 | 치즈를 옮기다 원반이 선반·배럴을 문다 → 고양이 몸이 지점 중심에 못 서고 파밍 대치가 깨진다 | 없음 | 없음 | **FAIL — 치즈 #3·#10·#12** | 2 | `Editor/WarehouseLayoutBuilder.cs:55,80-82` |
| **D-06** | **모든 벽감**의 정면 강아지 스폰 거리에 차단물이 없다 | 벽감을 벽 쪽으로 붙이다 개가 나올 자리를 막는다 | 없음 (거리 상수는 `Alcove`에서 읽는다 — 현재 private const라 **노출 필요**) | 없음 | **FAIL — 벽감 #2(강아지 벽)** | 2 | `Map/Alcove.cs`(`DOG_SPAWN_OFFSET`) · `WarehouseLayoutBuilder.cs:46,115` |
| **D-07** | **상점 후보**가 원반뿐 아니라 **설치 사거리 링까지** 차단물에 안 물린다 | 「상점엔 들어가지는데 그 안에서 설치할 자리가 벽」이 재발한다(이 부류로 이미 세 번 물렸다) | 없음 (`ShopSpotRadius`·`PlaceGhostMaxMeters`를 CSV에서 읽는다) | 겹침 — 굽기 검사가 **원반만** 본다. 이 케이스는 그 검사를 링까지 넓힌 판이다 | **미정 — 현행 데이터로 #2·#3·#5가 걸린다. 통과 기준(링 전체 vs 링의 절반)을 기획이 먼저 정해야 한다** | 3 | `Editor/WarehouseLayoutBuilder.cs:165-185` · `BalanceInfo.csv` |
| **D-08** | 손 배치 맵에도 **맵 종류와 무관한 게이트**가 걸린다 — 요새–치즈 최소 이격 · 치즈 등급 다중집합 완비 | 손 배치 맵만 게이트를 안 지나는 지금 구조가 그대로 굳는다 | 없음 (`MapValidator`가 이미 public static) | 겹침 — `MapValidatorTests`는 **가짜 데이터**로 함수만 본다. 이 케이스는 **실제 에셋**에 건다 | PASS (이격 12.04 ≥ 3.7 · 등급 {1..7}×2) — 회귀 잠금용 | 3 | `Map/MapValidator.cs` |
| **D-09** | **격자 굽기가 실제 계층을 훑을 때** 「막는 콜라이더인데 이름 규약을 안 따르는 오브젝트」를 놓치지 않는다 | 새 장식 프리팹에 콜라이더를 달고 `Deco_`처럼 규약 밖 이름을 쓰면 물리는 막는데 A\*는 뚫린 줄 안다 | 없음 (`NavGridBaker.Bake`는 public) | 없음 — `NavGridTests` 20건은 전부 **손으로 만든 격자**다. `Bake` 호출 0회 | **FAIL(현행 규칙 기준) — `Deco_Pumpkin`·`Deco_Walnut`·`Deco_Almond`** | 2 | `Ai/Grid/NavGridBaker.cs:22-24,47` |
| **D-10** | 폴리곤 굽기가 **막는 물건을 걷는 면으로 넣지 않는다** — 규약 밖 이름의 콜라이더가 `walkable`로 들어가면 아무도 못 올라가는 섬이 생긴다 | D-09와 같은 원인의 반대쪽 증상. 봇이 그 섬을 향해 걷는다 | `MapBuilder.CollectSources`가 private — **테스트용 노출 필요** | 없음 | **FAIL(현행 규칙 기준) — 같은 `Deco_*`** | 3 | `Map/MapBuilder.cs:170-172` |
| **D-11** | 봇 요새 진입 경주의 상수가 **CSV의 실제 고양이/쥐 이속 비 이상**이다 | CSV 이속을 조정하고 봇 상수를 안 따라 고치면, 봇이 이길 수 있는 요새를 계속 거른다 | 없음 (`InfoManager`) | 없음 | **FAIL — 상수 1.3 vs 실제 1.12** (봇 감사 C2) | 2 | `Bots/MouseBotLogic.cs:54-59,91` |
| **D-12** | 손 배치 맵의 **걸을 수 있는 면이 한 덩어리**다(격자를 굽고 임의의 자유 셀에서 채워 전부 닿는다) | 도면에 선반 열·궤짝 벽을 더하다 한 존을 통째로 봉인한다 | 없음 | 없음 — `MapValidator.AllConnected`는 방-복도 그래프라 **방이 0개인 손 배치 맵에는 무의미**하다 | PASS (자유 셀 49,784 / 도달 100% / 섬 1개) — 회귀 잠금용 | 2 | `Map/MapValidator.cs:28-44` |

### 7-2. 자동 — wesqa (실제 판에서만 드러나는 것)

| ID | 검증할 동작 | 이 케이스가 잡는 회귀 | 필요한 훅 | 기존과 겹침 | 지금 결과 | 우선 | 근거 |
|:--:|---|---|---|---|:--:|:--:|---|
| **D-13** | 쥐를 스폰 자리 수만큼 세워 판을 열면, **각자 제 스폰 좌표에 선다** — 물리 밀어냄으로 튀는 쥐가 없다 | 스폰이 지오메트리 안이면 CharacterController가 밀어내거나 낀다. D-01이 데이터로 잡는 것을 **실제 몸으로** 재확인한다 | 개별 봇/플레이어의 **월드 좌표를 주는 훅이 필요**하다(현재 `TestGetDistanceTo`·`TestGetSpawnDistanceFromOrigin`은 거리뿐) | 부분 — 봇 건강검사가 「끼임」을 보지만 **스폰 순간**은 안 본다 | 미측정(플레이모드 금지) — D-01이 FAIL이므로 여기도 빨갈 가능성이 높다 | **1** | `Lobby/PlayerSpawner.cs` |
| **D-14** | 스폰 직후 **각 쥐 자리에서 맵 중앙까지 쥐 경로가 나온다** | 스폰이 갇힌 주머니에 놓이면 그 쥐는 판 내내 못 움직인다 | `TestTracePath` **있음** (`agent="Mouse"`) | 없음 | 미측정 | **1** | `Testing/TestManager.NavGrid.cs` |
| **D-15** | 벽감을 전부 부수면 **직업권 두 장과 설계된 치즈 총량이 실제로 떨어진다** | 내용물 셔플·페이로드 배선이 물건을 삼킨다(M2) | `TestGetJobTicketPositions`·`TestBreakDogAlcove` 있음 · **「임의 벽감 부수기」 훅이 없다**(강아지 벽만 있다) | 없음 — 총량 훅은 **레이아웃 값**을 읽어 셔플 뒤를 못 본다 | 미측정 — 정적으로는 5/6 확률로 실패 | **1** | `Map/AlcoveSpawner.cs` · `Testing/TestManager.Economy.cs:311` |
| **D-16** | 서로 다른 매치 시드에서 **직업권 두 장이 서로 붙어 서지 않는다** | 셔플이 「가장 먼 두 벽에 나눠 둔다」를 깬다(M3) — 한 사람이 둘 다 먹는다 | D-15와 같은 훅 | 없음 | 미측정 · **판정선(최소 거리)을 기획이 먼저 정해야 한다** | 3 | `Editor/Setup/AlcoveFactory.cs:217-234` |
| **D-17** | 판 중에 생긴 구조물이 **격자에 자국을 남기고, 부서지면 그 자국이 정확히 걷힌다**(구조물 셀 수가 원래대로 돌아온다) | 격자를 다시 발행하거나 스탬프 시점이 어긋나면 요새·창고가 길찾기에 안 보이거나 유령 벽으로 남는다(M9·D1) | `TestGetNavGridInfo` **있음**(`struct=` 칸) | 부분 — `NavGridTests`가 **참조 카운트 되감기**를 순수 격자에서 본다. 이건 실제 스폰·파괴 경로다 | 미측정 | 2 | `Ai/Grid/NavGridBlocker.cs` · `Ai/Grid/NavGridService.cs` |
| **D-18** | 고양이 봇이 **배회 상태에 들어가는 틱이 0이 아니다** | 지휘관이 명령을 절대 놓지 않아 배회 가지가 구조적으로 죽는다(봇 감사 B1) | `TestGetBotDiagnostics`가 `Activity`를 준다 — **있음** | 없음 | 미측정 — 정적으로는 도달 불가 | 2 | `Bots/BotCommander.cs:69,108` · `Bots/BotBrainTrees.cs:103,121` |
| **D-19** | 고양이 스킬음이 울린 뒤 **쥐 봇이 위협으로 반응한다** | 큰 경고음을 쥐 봇의 가청 목록에 안 넣으면 「소리로 대응한다」는 설계가 봇 계측에서 통째로 0이 된다(봇 감사 B2) | `TestGetBotMouseReport` 있음 · **스킬 강제 발동 훅이 없다** | 없음 | 미측정 — 정적으로는 반응 불가(`IsCatMade`에 없다) | 3 | `Bots/BotSenseLogic.cs:92-100` |

### 7-3. 수동 — 사람 눈이 실제로 필요한 것

자동으로는 「무엇이 몇 개 있다」까지만 알 수 있고 **보이는가**는 못 재는 것들만 남긴다.

| ID | 절차 | 기대 결과 | 이 케이스가 잡는 회귀 | 주기 | 근거 |
|:--:|---|---|---|---|---|
| **D-20** | ① 로비에서 창고 맵 선택 → 판 시작 ② 자유 카메라(봇 실험실)로 맵을 위에서 훑는다 ③ 네 존(창고·하역장·식료품고·마당)과 중앙 홀을 각 1장씩 캡처 | 마젠타 면이 **한 장에도 없다**. 선반은 판벽 무늬, 궤짝 벽은 궤짝 무늬, 배럴은 배럴로 보인다 | 셰이더 로드가 널이라 조용히 마젠타가 구워진다(함정 6) · `MapDresser`의 프리팹 배선이 끊긴다 | 맵 에셋·머티리얼·`MapDresser`를 고칠 때마다 | `Editor/Setup/EditorAssetUtil.cs:68-95` · `Editor/MapMaterialBaker.cs:29-33` |
| **D-21** | ① 창고 맵으로 판 시작 ② 빛웅덩이 일곱 자리(중앙·마당·식료품고·하역장·계단실 등)를 차례로 방문 | **일곱 곳 모두** 바닥이 밝다. 어느 존도 통째로 캄캄하지 않다 | 바닥 타일 분할이 깨지면 URP의 오브젝트당 추가 광원 4개 한도에 걸려 **나머지 존이 캄캄해진다**(실측 이력 있음) | 바닥 굽기·타일 크기·빛 자리를 고칠 때 | `Map/MapBuilder.cs`(`FLOOR_TILE_METERS`) · `Map/MapDresser.cs` |
| **D-22** | ① 창고 맵으로 판 시작 ② 미니맵을 열고 실제 맵과 대조 | 선반 열·궤짝 벽의 배치가 **실제 지형과 같은 모양**으로 읽힌다. 존 구분이 눈에 잡힌다 | 손 배치 맵은 방·복도가 없어 미니맵이 **Props 경로**로만 그려진다 — 그 갈림이 끊기면 테두리만 남는다 | 미니맵·레이아웃 구조를 고칠 때 | `UI/MinimapPainter.cs:62` (`MinimapPainterTests`가 픽셀 수만 본다) |
| **D-23** | ① 쥐 8·고양이 2로 판을 연다 ② 스폰 직후 **8개 스폰 자리를 차례로 본다**(관전 또는 봇 실험실 카메라) | 여덟 쥐가 **전부 바닥 위, 물건 밖**에 서 있다. 상자 안에 낀 쥐·공중에 뜬 쥐가 없다 | D-01·D-13이 수치로 잡는 것의 **눈 확인**. 자동 케이스가 아직 없을 때의 임시 대체이기도 하다 | 도면(요새자리·차단물)을 고칠 때 | §2-2 L1·L2 |

### 7-4. 폐기 대장

| ID | 상태 | 사유 |
|:--:|---|---|
| — | — | (아직 폐기된 케이스 없음) |

### 7-5. 최소 세트

지금 이 감사가 찾은 **치명·높음 넷**(M1 스폰 매몰 · M2 물건 증발 · M3 직업권 뭉침 · M5 여유 7 mm)을 전부 붙잡는 최소 조합:

**D-01 · D-02 · D-03 · D-13 · D-14 · D-15** — EditMode 셋 + wesqa 셋. 앞의 셋은 훅 하나(D-03)만 추가하면 오늘 바로 돌아간다.

---

## 8. 한계

- **플레이모드를 안 썼다.** M1의 「쥐가 실제로 어떻게 되는가」(끼는가 · 밀려나는가 · 떨어지는가)는 안 봤다.
  격자 셀이 `blocked`이고 좌표가 상자 내부라는 것까지만 확인했다.
- 격자 판정은 `NavGridBaker.StampBox`의 규칙(셀 중심 ±0.25 m OBB 겹침)을 **파이썬으로 다시 구현한** 결과다.
  `MapBuilder`가 실제로 세우는 콜라이더의 스케일·회전을 그대로 재현했다고 보지만, 실행 대조는 못 했다.
- 여유(clearance) 값은 `NavGrid.BuildClearance`의 8방향 체임퍼를 같은 순서로 재현했다 — 오차 ±1 셀(0.5 m)을 감안해 읽어야 한다.
- 봇 트리·배선 층은 봇 감사의 결론을 인용했고, 미해결 6건만 코드에서 재확인했다.
