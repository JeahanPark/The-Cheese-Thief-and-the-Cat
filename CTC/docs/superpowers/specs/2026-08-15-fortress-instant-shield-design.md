# 요새 즉발 방패화 · 미끼 삭제 — 설계 (2026-08-15)

## 왜

쥐의 설치물이 셋(미끼·요새·창고)이었다. 미끼(50, 가짜 발소리 30초)와 요새(220, 조준 설치·회복 벙커)가
"쫓길 때 쓰는 물건" 자리를 나눠 갖고 있었는데, 미끼는 싸서 도배되고 요새는 조준하느라 쫓기는 순간에
못 썼다. **하나로 합친다** — 미끼를 없애고, 요새를 미끼처럼 즉시 놓는 **3방짜리 방패**로 만든다.

## 규칙 (확정)

| | 지금 | 변경 |
|---|---|---|
| 미끼 | 50 · 발소리 가짜 쥐 30초 | **삭제** |
| 요새 가격 | 220 | **80** |
| 요새 HP | 300 (노업 5방) | **180** — 노업 덮치기 60 × 3, 3방째 부서진다 |
| 설치 | 고스트 조준(앞 1.5~4m) | **즉시, 내 위치 중심** · 입구 = 내가 보는 방향 |
| 안쪽 회복 | 초당 10 | **없음** — 순수 방패 |
| 겹침 | 플레이어 무시 | 풋프린트 안 **고양이 → 바깥으로 밀어냄** · 벽 두께 자리의 쥐 → 밀어냄 · 안쪽 쥐 → 같이 보호 |
| 창고 | 고스트 조준 | 그대로 (설치물 중 유일하게 조준 남음) |

## 밀어내기

이동은 소유자 권위(ClientNetworkTransform)라 서버가 남의 몸을 못 옮긴다.
서버가 스폰 직후 풋프린트(2.4×2.4 + CC 반경)에 겹친 몸을 찾아 소유자에게 `PushRpc(delta)` →
소유자가 `CC.Move(delta)`. 봇은 서버 소유라 같은 코드가 즉시 돈다.
고양이는 중심에서 가장 가까운 바깥 지점(벽 바깥 + CC 반경 + 여유 0.2)까지, 쥐는 벽 두께 자리에
있을 때만 안쪽/바깥쪽 가까운 쪽으로. 밀린 고양이 화면엔 FortressPlace 음(기존)이 난다.

## 봇

도주 트리 `PlaceDecoyForFlee` → `PlaceFortressForFlee`(보이는 고양이 근접 + 치즈 ≥ 80, 쿨 10s).
`ChooseRefuge`(기존 요새로 피난)는 유지 — 회복은 없어도 고양이가 못 들어오는 건 그대로다.
`NextPurchase`에서 요새 우선순위는 그대로(대시→창고→요새), 가격이 내려가 더 자주 산다.

## 데이터

- `ShopInfo.csv`: Decoy 행 삭제, Fortress 220→80
- `BalanceInfo.csv`: FortressHp 300→180, `FortressHealPerTick`·`FortressHealRadius`·`FortressHealTickSeconds`·
  `DecoyLifetimeSeconds` 컬럼 삭제 → `Tools/CTC/Convert CSV → Info`
- 요새 HP는 프리팹에 굽는다 → 씬 재생성 필수

## 코드 범위

- 삭제: `Economy/DecoyLayers.cs`·`DecoyLifetime.cs`·`FortressHealZone.cs`, 미끼 프리팹·레이어 세팅(SceneComposer·
  SetupNetworkingScene·PlayerPrefabFactory·StructurePrefabFactory의 Decoy 부분), `SfxId.DecoyPlace` 클립 배선,
  `ShopItemId.Decoy` (enum 값은 남겨도 되나 카탈로그·CanApply·Apply·IsPlaceable에서 제거)
- 변경: `ShopController.SpawnStructure` — 요새는 발밑(플레이어 위치) 스폰 + 밀어내기 · `ShopLogic.IsPlaceable`(요새 false) ·
  `ShopHotbar` 힌트/토스트 · `MouseBotLogic`/`BotBrainTrees`/`BotAgent`(미끼→요새) · `TestManager.Economy`(TestBuyDecoy 삭제)
- HUD: 쥐 핫바 6→5칸(HOTBAR_SLOTS 상수는 6 유지 — 카탈로그가 5면 5칸만 켜진다) · 요새 힌트 "3방 버티는 즉석 벙커" ·
  구매 토스트 "요새 설치 — 고양이는 못 들어온다 · 세 방이면 부서진다"

## 검증

- EditMode: `ShopLogicTests`(Decoy 제거·Fortress not placeable) · `FortressHealZone` 관련 테스트 삭제 · 밀어내기 순수 함수 테스트
  (`FortressPushLogic.PushDelta(center, yaw, bodyPos, bodyRadius, isCat)`)
- qa_v36(미끼 단언 제거) · qa_v44(미끼 발소리 단언 제거) · qa_v39(요새 사용처 확인)
- **밀어내기 실검**: 고양이를 쥐 옆 1m에 세우고 설치 → 고양이 밖·쥐 안 (신규 단언, qa_v46 또는 v36에 추가)
- 봇 3판 — 쥐 승률 33% 유지되는지(미끼 삭제로 도주 수단 하나 줄었지만 즉발 방패가 대체)

## 문서

규칙과-현황.html(상점·소리 표·미끼 행 삭제) · 코드 다이어그램(워크플로우 ② 미끼 노드 삭제·요새 설명) · CLAUDE.md 상점 언급
