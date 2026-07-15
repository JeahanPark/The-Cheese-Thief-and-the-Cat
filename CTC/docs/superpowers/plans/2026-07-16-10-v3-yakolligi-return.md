# Plan 10: v3 원작 복귀 (초기 벙커·유지비 삭제·터널 삭제·10분 안전 타이머) — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스펙 v3 반영 — 쥐 1마리당 **진입형 초기 요새**(원작 벙커), 치즈 유지비/굶주림 삭제, 쥐 터널 삭제(v1 가구 맵 복귀), 타이머 5분→10분 안전장치화.

**Architecture:** 요새는 새 프리팹 빌더(`CreateFortressPrefab`) — 외벽 4면 박스 + 쥐 전용 입구(1.0×0.75, v2 쥐구멍 물리 재활용) + 내부 회복존(`FortressHealZone` 복원, 반경 1.0). 초기 배치는 `LobbyManager`가 스폰 시 쥐마다 서버 스폰(동적 — 재시작 스윕이 자동 정리). 쥐 스폰 공식은 역할 인지형 `SpawnPosition(roles, index)`(순수, TDD)로 교체 — 쥐는 자기 요새 입구 앞. **`CatPounce`에 차폐(LoS) 레이캐스트 추가** — 없으면 벽 너머 타격으로 진입형 요새가 무력화된다. 유지비 계열(`SurvivalLogic`·`CheeseUpkeep`·`SpendUpTo`)은 전면 삭제, 매치 시간 상수는 `MatchFlowLogic`으로 이사(600s).

**Tech Stack:** 기존 NGO·씬 재생성 파이프라인·EditMode TDD. 신규 패키지 없음.

**스펙 근거:** [기획서 v3](../specs/2026-07-06-cheese-thief-and-cat-design.md) §4(타이머)·§6(거점)·§7(순수 화폐)·§8(맵)·§12-A(수치)

## 설계 결정 (v3 확정)

| 항목 | 값 |
|---|---|
| 매치 시간 | **600s** (안전장치 — 만료 시 생존 쥐 ≥1 → MouseWin). `MatchFlowLogic.MATCH_DURATION_SECONDS` |
| 유지비·굶주림·시작치즈 | **전부 삭제** — 치즈는 순수 화폐 |
| 초기 요새 | 쥐 1마리당 1개, `FORTRESS_SPOTS[쥐순번]`(외곽 6곳, 입구는 맵 중앙향), 서버가 게임 시작 시 동적 스폰 |
| 요새 구조 | 풋프린트 2.4×2.4·벽높이 1.2·두께 0.2, 입구 1.0(폭)×0.75(높이)+인방 — 쥐 CC(실효 0.60/0.63) 통과·고양이(0.86/1.03) 차단. 지붕 없음(고양이 점프 없음) |
| 요새 스탯 | HP 300(=v1)·내부 회복 +10/s(반경 1.0 — 입구 밖은 미회복)·파괴 시 소멸·보상 없음 |
| 파운스 차폐 | RaycastAll — 시전자/대상 자신의 콜라이더 제외, 그 외 히트 = 차단. **쥐가 요새 안에서 쏘는 건 허용**(원작 벙커 안 사격) — MousePopgun은 변경 없음 |
| 상점 요새 | 220 재등록 — 구매 시 전방 1.5m, 입구가 구매자 쪽을 향하게 스폰 |
| 맵 | v1 가구 그레이박스 복원. 터널 링·쥐구멍·인방 빌더 삭제 |
| 치즈 지점 | v1 복원 + **G1 (-9,0,-7) r2**(쥐0 스폰+넛지(2,3) 도달점) + **G2 (-13.5,0,-10)**(요새1과 겹침 회피) |
| 쥐 스폰 | 자기 요새 입구 앞 1.8m. 고양이 스폰 = k번째 고양이 (k*2, 0.1, 0) |

## 실행 환경 주의 (모든 태스크 공통)

- 컴파일: `u_editor_asset(action: refresh)` → `powershell -NoProfile -File tools/focus-unity.ps1` → ~12s → `u_console` CS 에러 0
- EditMode: `Tools/CTC/Run EditMode Tests` → `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CTC\TestResults.xml`. **현재 Passed 80.** 완료 시 **81** (Survival −2 · ShopFortress +1 · Spawn +2)
- QA 호스트 시작은 wesqa `TestStartHost` (직접 StartHost 금지)
- 신규 코드 `m_`/`_` 컨벤션 · **기존 파일은 그 파일의 스타일 유지**(LobbyManager는 무접두 `catPrefab` 스타일) · 커밋은 저장소 루트에서 `CTC/` 접두·.meta 포함·`Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`. `ProjectSettings/*`·`Assets/Settings/*`·`.claude/*` 커밋 금지
- 씬/프리팹 재생성 후 새 NetworkObject 프리팹은 `BakeGlobalObjectIdHash` 경로 + `NetworkObjectHashTests.ASSET_PATHS` 등록 필수

## 파일 구조

| 파일 | 작업 |
|---|---|
| `Assets/Scripts/Match/MatchFlowLogic.cs` | `MATCH_DURATION_SECONDS = 600f` 추가 (`EvaluateAtTimeUp`은 유지) |
| `Assets/Scripts/Match/MatchManager.cs` | 타이머 초기화 참조를 `MatchFlowLogic.MATCH_DURATION_SECONDS`로 |
| `Assets/Scripts/Economy/SurvivalLogic.cs` | **삭제** (+.meta) |
| `Assets/Scripts/Economy/CheeseUpkeep.cs` | **삭제** (+.meta) |
| `Assets/Scripts/Economy/CheeseWallet.cs` | `SpendUpTo` 삭제 (`using UnityEngine;` 도 미사용 시 제거) |
| `Assets/Scripts/Economy/ShopLogic.cs` | 카탈로그 Fortress 220 재등록·`FORTRESS_HEAL_RADIUS` 3→1.0 |
| `Assets/Scripts/Economy/ShopController.cs` | `m_FortressPrefab`·Fortress 케이스·입구향 스폰 |
| `Assets/Scripts/Economy/ShopHud.cs` | ITEM_LABELS에 "Fortress" 재삽입 |
| `Assets/Scripts/Economy/FortressHealZone.cs` | **복원**(신규 작성 — v1 코드 + 반경만 축소) |
| `Assets/Scripts/Combat/CatPounce.cs` | 차폐(LoS) 레이캐스트 추가 |
| `Assets/Scripts/Lobby/LobbyManager.cs` | `FORTRESS_SPOTS`·역할 인지 `SpawnPosition`·초기 요새 스폰·시작치즈 제거 |
| `Assets/Scripts/Player/PlayerMovement.cs` | 소유자 스폰 위치 계산을 새 공식으로 |
| `Assets/Scripts/Editor/SetupNetworkingScene.cs` | `CreateFortressPrefab`·v1 맵 복원·터널 삭제·치즈지점 v3·요새 배선·타이머 라벨 "10:00"·CheeseUpkeep 부착 제거 |
| `Assets/Scripts/Testing/TestManager.cs` | `TestBuyFortress` 복원 (`TestExpireTimer` 유지) |
| `Assets/Scripts/Network/MultiplayerQaProbe.cs` | `TestMpBuyFortress` 케이스 복원 |
| `Assets/Scripts/Tests/EditMode/SurvivalLogicTests.cs` | **삭제** (+.meta) |
| `Assets/Scripts/Tests/EditMode/ShopLogicTests.cs` | Fortress 테스트 복원 +1 |
| `Assets/Scripts/Tests/EditMode/LobbyRoleLogicTests.cs` | 스폰 공식 테스트 +2 |
| `Assets/Scripts/Tests/EditMode/NetworkObjectHashTests.cs` | `CheeseFortress.prefab` 경로 재등록 |
| `Assets/Prefabs/CheeseFortress.prefab` | 재생성(Setup 메뉴가 생성) |

---

### Task 1: 유지비 삭제 + 매치 시간 600s (TDD 아님 — 삭제·상수 이동)

**Files:**
- Delete: `Assets/Scripts/Economy/SurvivalLogic.cs`(+.meta), `Assets/Scripts/Economy/CheeseUpkeep.cs`(+.meta), `Assets/Scripts/Tests/EditMode/SurvivalLogicTests.cs`(+.meta)
- Modify: `Assets/Scripts/Match/MatchFlowLogic.cs`, `Assets/Scripts/Match/MatchManager.cs:41`, `Assets/Scripts/Economy/CheeseWallet.cs:32-37`, `Assets/Scripts/Lobby/LobbyManager.cs:141-144`, `Assets/Scripts/Editor/SetupNetworkingScene.cs:192`

- [ ] **Step 1: MatchFlowLogic에 상수 추가** — 클래스 첫 줄에:

```csharp
    public static class MatchFlowLogic
    {
        /// <summary>v3: 안전 타이머(원작 66:40 대응 축소) — 만료 시 생존 쥐가 있으면 쥐 승.</summary>
        public const float MATCH_DURATION_SECONDS = 600f;
```

클래스 주석의 `v2: 전멸 판정 + 타이머 만료 판정(생존 쥐 있으면 쥐 생존승).` 줄은
`v3: 전멸 판정 + 안전 타이머 만료 판정(생존 쥐 있으면 쥐 생존승).` 으로 교체.

- [ ] **Step 2: MatchManager 참조 교체** — 41행:

```csharp
                m_RemainingSeconds.Value = Economy.SurvivalLogic.MATCH_DURATION_SECONDS;
```
→
```csharp
                m_RemainingSeconds.Value = MatchFlowLogic.MATCH_DURATION_SECONDS;
```

- [ ] **Step 3: 파일 삭제** — `SurvivalLogic.cs`·`CheeseUpkeep.cs`·`SurvivalLogicTests.cs` + 각 `.meta`

- [ ] **Step 4: CheeseWallet에서 SpendUpTo 삭제** — 32~37행 메서드 전체 + 이제 미사용이 된 `using UnityEngine;`(1행) 제거

- [ ] **Step 5: LobbyManager 시작 치즈 제거** — `SpawnPlayers()`의:

```csharp
                if (p.Role == RoleId.Mouse)
                {
                    go.GetComponent<Economy.CheeseWallet>().AddCheese(Economy.SurvivalLogic.STARTING_CHEESE);
                }
```
블록 전체 삭제.

- [ ] **Step 6: SetupNetworkingScene 프리팹 부착 제거** — 192행 `root.AddComponent<CheeseUpkeep>(); // v2 유지비 — 쥐만` 삭제

- [ ] **Step 7: 컴파일 → EditMode** — refresh + focus-unity 후 `u_console` CS 에러 0.
`Tools/CTC/Run EditMode Tests` → Expected: **Passed: 78** (80 − Survival 2)

- [ ] **Step 8: Commit**

```bash
git add -A CTC/Assets/Scripts
git commit -m "feat: remove cheese upkeep/starvation, extend safety timer to 10min (v3)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: 상점 카탈로그 Fortress 복원 (TDD)

**Files:**
- Modify: `Assets/Scripts/Economy/ShopLogic.cs`, `Assets/Scripts/Economy/ShopHud.cs:27-30`
- Test: `Assets/Scripts/Tests/EditMode/ShopLogicTests.cs`

- [ ] **Step 1: 실패 테스트 추가** — `ShopLogicTests.cs`에 (v1에 있던 테스트 복원):

```csharp
        [Test]
        public void Find_Fortress_MatchesOriginalBunker()
        {
            Assert.AreEqual(220, ShopLogic.Find(ShopItemId.Fortress).Cost);
            Assert.AreEqual(RoleId.Mouse, ShopLogic.Find(ShopItemId.Fortress).Role);
        }
```

- [ ] **Step 2: EditMode 실행 → 실패 확인** — Expected: Failed 1 (`Find`가 default 반환 → Cost 0 ≠ 220)

- [ ] **Step 3: ShopLogic 수정** —

enum 주석 정리 (12~13행):
```csharp
        Fortress = 5,
```
(`// Fortress = 5 → v2 삭제` 주석 줄 삭제)

CATALOG의 `Decoy` 줄 다음에 삽입:
```csharp
            new ShopItem(ShopItemId.Fortress, RoleId.Mouse, 220),
```

`FORTRESS_HEAL_RADIUS` 교체 (진입형 — 내부만 회복):
```csharp
        public const float FORTRESS_HEAL_RADIUS = 1.0f; // 요새 내부(입구 밖 미적용)
```

클래스 주석의 가격 설명은 그대로(`벙커220` 이미 언급).

- [ ] **Step 4: ShopHud 라벨 재삽입** — CATALOG 순서와 일치 (Decoy 다음, ATK Up 앞):

```csharp
        private static readonly string[] ITEM_LABELS =
        {
            "Slingshot", "Machine Gun", "Dash", "Decoy", "Fortress", "ATK Up", "SPD Up",
        };
```

- [ ] **Step 5: 컴파일 → EditMode** — Expected: **Passed: 79**
- [ ] **Step 6: Commit** — `feat: restore fortress to shop catalog (v3, 220 cheese)`

---

### Task 3: 역할 인지 스폰 공식 + 초기 요새 배치 좌표 (TDD)

**Files:**
- Modify: `Assets/Scripts/Lobby/LobbyManager.cs`, `Assets/Scripts/Player/PlayerMovement.cs:47-60`
- Test: `Assets/Scripts/Tests/EditMode/LobbyRoleLogicTests.cs`

- [ ] **Step 1: 실패 테스트 추가** — `LobbyRoleLogicTests.cs`에:

```csharp
        // Vector3 정확 비교(Assert.AreEqual)는 float 오차(-12f+1.8f ≠ -10.2f 리터럴,
        // yaw 회전 epsilon)로 깨진다 — 거리 허용오차 비교 사용.
        private static void AssertPos(Vector3 _expected, Vector3 _actual)
        {
            Assert.Less(Vector3.Distance(_expected, _actual), 1e-4f,
                $"expected {_expected} but was {_actual}");
        }

        [Test]
        public void SpawnPosition_Cat_CenterLine()
        {
            var roles = new List<RoleId> { RoleId.Cat, RoleId.Mouse, RoleId.Cat };
            // 0번째 고양이 (0,0.1,0), 1번째 고양이 (2,0.1,0)
            AssertPos(new Vector3(0f, 0.1f, 0f), LobbyManager.SpawnPosition(roles, 0));
            AssertPos(new Vector3(2f, 0.1f, 0f), LobbyManager.SpawnPosition(roles, 2));
        }

        [Test]
        public void SpawnPosition_Mouse_AtOwnFortressEntrance()
        {
            var roles = new List<RoleId> { RoleId.Cat, RoleId.Mouse, RoleId.Mouse };
            // 0번째 쥐 = FORTRESS_SPOTS[0] 입구 앞 1.8m — (-11,0,-12)+yaw0 전방(+Z)*1.8, y 0.1
            AssertPos(new Vector3(-11f, 0.1f, -10.2f), LobbyManager.SpawnPosition(roles, 1));
            // 1번째 쥐 = FORTRESS_SPOTS[1] — (9,0,-12.5)+yaw0 전방*1.8
            AssertPos(new Vector3(9f, 0.1f, -10.7f), LobbyManager.SpawnPosition(roles, 2));
        }
```

파일 상단 using에 `System.Collections.Generic`·`UnityEngine`이 없으면 추가 (`CTC.Runtime.Lobby`·`NUnit.Framework`는 이미 있음).

- [ ] **Step 2: EditMode 실행 → 컴파일 실패 확인** — Expected: `SpawnPosition(List<RoleId>, int)` 오버로드 없음 CS 에러

- [ ] **Step 3: LobbyManager 구현** — 기존 `SpawnPosition(int,int)`(83~86행)를 다음으로 **교체**:

```csharp
        // v3 초기 요새 자리(외곽 6곳) — pos + 입구가 맵 중앙을 향하는 yaw.
        // 가구(BuildGreyboxMap)·치즈 지점과 풋프린트(2.4×2.4)가 안 겹치게 선정.
        public static readonly (Vector3 pos, float yawDeg)[] FORTRESS_SPOTS =
        {
            (new Vector3(-11f, 0f, -12f), 0f),    // 남서 — 쥐0 (QA: 넛지(2,3)→G1)
            (new Vector3(9f, 0f, -12.5f), 0f),    // 남
            (new Vector3(-10f, 0f, 9f), 180f),    // 북서
            (new Vector3(10f, 0f, 9f), 180f),     // 북동
            (new Vector3(0f, 0f, -12.5f), 0f),    // 남 중앙
            (new Vector3(-12.5f, 0f, -3f), 90f),  // 서
        };

        public const float FORTRESS_ENTRANCE_OFFSET_METERS = 1.8f;

        /// <summary>
        /// 결정적 스폰 공식(서버 스폰·소유자 초기 텔레포트 공용) —
        /// k번째 고양이는 중앙 라인, m번째 쥐는 자기 요새 입구 앞.
        /// </summary>
        public static Vector3 SpawnPosition(IReadOnlyList<RoleId> roles, int index)
        {
            int nth = 0; // 같은 역할 내 순번
            for (int i = 0; i < index; i++)
            {
                if (roles[i] == roles[index]) nth++;
            }

            if (roles[index] == RoleId.Cat) return new Vector3(nth * 2f, 0.1f, 0f);

            (Vector3 pos, float yawDeg) = FORTRESS_SPOTS[nth % FORTRESS_SPOTS.Length];
            Vector3 entranceDir = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;
            Vector3 spawn = pos + entranceDir * FORTRESS_ENTRANCE_OFFSET_METERS;
            spawn.y = 0.1f;
            return spawn;
        }

        /// <summary>소유 클라이언트가 스폰 시 자기 자리를 계산할 때 사용(서버 공식과 동일).</summary>
        public Vector3 SpawnPositionOf(int index) => SpawnPosition(SnapshotRoles(), index);
```

`SpawnPlayers()`의 `Vector3 pos = SpawnPosition(i, _players.Count);` →

```csharp
                Vector3 pos = SpawnPosition(SnapshotRoles(), i);
```

(주의: `SnapshotRoles()`는 이미 존재. 루프 밖에서 1회 스냅샷해 변수로 쓰는 게 낫다:)

```csharp
        private void SpawnPlayers()
        {
            List<RoleId> roles = SnapshotRoles();
            for (int i = 0; i < _players.Count; i++)
            {
                LobbyPlayerState p = _players[i];
                GameObject prefab = p.Role == RoleId.Cat ? catPrefab : mousePrefab;
                Vector3 pos = SpawnPosition(roles, i);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId);
            }
        }
```

- [ ] **Step 4: PlayerMovement 소유자 텔레포트 교체** — 58행:

```csharp
            transform.position = Lobby.LobbyManager.SpawnPosition(idx, lobby.PlayerCount);
```
→
```csharp
            transform.position = lobby.SpawnPositionOf(idx);
```

- [ ] **Step 5: 컴파일 → EditMode** — Expected: **Passed: 81** (+2)
- [ ] **Step 6: Commit** — `feat: role-aware spawn at own fortress entrance (v3)`

---

### Task 4: 요새 런타임 — FortressHealZone 복원 + 초기 스폰 + 상점 적용 + 파운스 차폐

**Files:**
- Create: `Assets/Scripts/Economy/FortressHealZone.cs`
- Modify: `Assets/Scripts/Lobby/LobbyManager.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Combat/CatPounce.cs`, `Assets/Scripts/Testing/TestManager.cs`, `Assets/Scripts/Network/MultiplayerQaProbe.cs`

- [ ] **Step 1: FortressHealZone 작성** (v1 복원 — 반경 상수만 ShopLogic 1.0을 따름):

```csharp
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 쥐 요새(원작 벙커)의 회복 거점: 서버가 1초마다 반경(내부) 안의 살아있는
    /// 쥐 플레이어를 회복. 요새 자체는 Health+RoleTag(Mouse)+PlayerDeath —
    /// 고양이가 부수면 despawn. v3: 진입형 — 반경 1.0이라 입구 밖은 미적용.
    /// </summary>
    public class FortressHealZone : NetworkBehaviour
    {
        private const float TICK_SECONDS = 1f;

        private float m_Timer;

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            m_Timer += Time.deltaTime;
            if (m_Timer < TICK_SECONDS) return;
            m_Timer -= TICK_SECONDS;

            foreach (Health target in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (target.IsDead) continue;
                if (target.NetworkObject == null || !target.NetworkObject.IsPlayerObject) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > ShopLogic.FORTRESS_HEAL_RADIUS * ShopLogic.FORTRESS_HEAL_RADIUS) continue;

                target.Heal(ShopLogic.FORTRESS_HEAL_PER_TICK);
            }
        }
    }
}
```

- [ ] **Step 2: LobbyManager 초기 요새 스폰** — 필드에 (기존 `mousePrefab` 아래):

```csharp
        [SerializeField] private GameObject fortressPrefab;
```

`SpawnPlayers()` 루프 안, `SpawnAsPlayerObject` 다음 줄에:

```csharp
                if (p.Role == RoleId.Mouse)
                {
                    SpawnInitialFortress(MouseOrderOf(roles, i));
                }
```

클래스에 헬퍼 2개 추가 (`SpawnPlayers` 아래):

```csharp
        // index 앞까지의 쥐 수 = 이 쥐의 요새 순번 (SpawnPosition의 nth와 동일 공식)
        private static int MouseOrderOf(List<RoleId> roles, int index)
        {
            int nth = 0;
            for (int i = 0; i < index; i++)
            {
                if (roles[i] == RoleId.Mouse) nth++;
            }
            return nth;
        }

        // v3 초기 벙커: 쥐마다 자기 요새를 서버가 동적 스폰 — 재시작 스윕이 자동 정리.
        private void SpawnInitialFortress(int mouseOrder)
        {
            if (fortressPrefab == null) return;

            (Vector3 pos, float yawDeg) = FORTRESS_SPOTS[mouseOrder % FORTRESS_SPOTS.Length];
            GameObject go = Instantiate(fortressPrefab, pos, Quaternion.Euler(0f, yawDeg, 0f));
            go.GetComponent<NetworkObject>().Spawn(true);
        }
```

- [ ] **Step 3: ShopController 요새 구매 복원** — 필드에 (`m_DecoyPrefab` 아래):

```csharp
        [SerializeField] private GameObject m_FortressPrefab;
```

`CanApply` switch의 `Decoy` 케이스 다음에:

```csharp
                case ShopItemId.Fortress:
                    return m_FortressPrefab != null;
```

`Apply` switch의 `Decoy` 케이스 다음에:

```csharp
                case ShopItemId.Fortress:
                    // 입구(로컬 +Z)가 구매자를 향하게 — 쫓기며 지어도 바로 들어갈 수 있다
                    SpawnStructure(m_FortressPrefab, Quaternion.LookRotation(-FlatForward()));
                    break;
```

`SpawnStructure`를 회전 파라미터 버전으로 교체 + 헬퍼:

```csharp
        private void SpawnStructure(GameObject _prefab)
        {
            SpawnStructure(_prefab, Quaternion.identity);
        }

        private void SpawnStructure(GameObject _prefab, Quaternion _rotation)
        {
            Vector3 pos = transform.position + FlatForward() * PLACE_FORWARD_METERS;
            GameObject go = Instantiate(_prefab, pos, _rotation);
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        private Vector3 FlatForward()
        {
            return new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        }
```

(기존 `SpawnStructure` 본문의 forward 계산은 `FlatForward()`로 대체된다. `Decoy`의 `SpawnStructure(m_DecoyPrefab);` 호출은 그대로 컴파일된다.)

- [ ] **Step 4: CatPounce 차폐 추가** — `PounceRpc`의 `IsInPounceArc` 검사 다음 줄에:

```csharp
                // v3 진입형 요새: 벽 너머 타격 금지 — 요새 안 쥐는 요새(외벽)부터 부숴야 한다
                if (IsOccluded(target)) continue;
```

클래스에 메서드 추가 (`PounceRpc` 아래):

```csharp
        // 시전자→대상 가슴 높이 레이 — 둘 자신의 콜라이더는 차단으로 안 친다
        // (요새 자체를 때릴 때 그 요새의 외벽에 막히면 안 됨). 트리거 무시.
        private bool IsOccluded(Health _target)
        {
            Vector3 from = transform.position + Vector3.up * 0.45f;
            Vector3 to = _target.transform.position + Vector3.up * 0.4f;
            Vector3 dir = to - from;

            foreach (RaycastHit hit in Physics.RaycastAll(
                         from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform hitRoot = hit.transform.root;
                if (hitRoot == transform.root || hitRoot == _target.transform.root) continue;
                return true;
            }
            return false;
        }
```

- [ ] **Step 5: QA 훅 복원 + 요새 진입 훅** — `TestManager.cs`의 `TestBuyDecoy` 줄 다음에:

```csharp
        public void TestBuyFortress() => BuyLocal(ShopItemId.Fortress);
```

`TestManager.cs`의 `TestTeleportToOtherPlayer` 아래에 (호스트 로컬 플레이어 임의 배치 — 차폐 QA용):

```csharp
        /// <summary>로컬 플레이어를 지정 좌표로 순간이동(CC 토글) — 요새 차폐/진입 QA용.</summary>
        public void TestTeleportLocalPlayer(float _x, float _z)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) { GameDebug.LogWarning("[TestManager] 로컬 플레이어 없음"); return; }

            var cc = local.GetComponent<UnityEngine.CharacterController>();
            if (cc != null) cc.enabled = false;
            local.transform.position = new UnityEngine.Vector3(_x, 0.1f, _z);
            if (cc != null) cc.enabled = true;
            GameDebug.Log($"[TestManager] TeleportLocalPlayer → ({_x}, {_z})");
        }
```

`MultiplayerQaProbe.cs`의 `ExecuteCloneCommand` switch, `TestMpBuyDecoy` 케이스 다음에 (클론 쥐는 소유자 권위 — 호스트가 못 움직이므로 클론 자체 커맨드 필요):

```csharp
                case "TestMpBuyFortress":
                    BuyLocal(ShopItemId.Fortress);
                    break;
                case "TestMpEnterNearestFortress":
                    EnterNearestFortress();
                    break;
```

`MultiplayerQaProbe.cs`의 `BuyLocal` 아래에:

```csharp
        // 클론 쥐를 가장 가까운 요새 중심으로 순간이동(CC 토글) — 내부 회복/차폐 QA용.
        private void EnterNearestFortress()
        {
            NetworkObject local = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject()
                : null;
            if (local == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] EnterNearestFortress — 로컬 플레이어 없음");
                return;
            }

            FortressHealZone nearest = null;
            float best = float.MaxValue;
            foreach (var zone in FindObjectsByType<FortressHealZone>(FindObjectsSortMode.None))
            {
                float d = (zone.transform.position - local.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = zone; }
            }
            if (nearest == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] EnterNearestFortress — 요새 없음");
                return;
            }

            var cc = local.GetComponent<CharacterController>();
            Vector3 pos = nearest.transform.position + Vector3.up * 0.1f;
            if (cc != null) cc.enabled = false;
            local.transform.position = pos;
            if (cc != null) cc.enabled = true;
            Debug.Log($"[MultiplayerQaProbe] EnterNearestFortress → {pos}");
        }
```

- [ ] **Step 6: 컴파일 → EditMode** — Expected: **Passed: 81** (변동 없음 — 런타임 코드)
- [ ] **Step 7: Commit** — `feat: enterable fortress runtime — initial per-mouse bunker, shop placement, pounce occlusion (v3)`

---

### Task 5: SetupNetworkingScene — 요새 프리팹 빌더 + v1 맵 복원 + 배선

**Files:**
- Modify: `Assets/Scripts/Editor/SetupNetworkingScene.cs`, `Assets/Scripts/Tests/EditMode/NetworkObjectHashTests.cs:17-23`

- [ ] **Step 1: 경로 상수 + Run() 확장** — 상수에 (`DecoyPrefabPath` 아래):

```csharp
        private const string FortressPrefabPath = PrefabDir + "/CheeseFortress.prefab";
```

`Run()`의 decoyPrefab 생성 다음에:

```csharp
            GameObject fortressPrefab = CreateFortressPrefab();
```

`CreatePlayerPrefab` 두 호출에 `fortressPrefab: fortressPrefab,` 인자 추가(decoyPrefab 인자 옆), `CreateGameScene(catPrefab, mousePrefab, fortressPrefab, decoyPrefab);` 로 교체, 로그 문자열은 `"[SetupNetworkingScene] 완료 — 프리팹 4종 + Game.unity 재생성 (v3 초기 벙커 맵)"`.

- [ ] **Step 2: CreateFortressPrefab 작성** — `CreateStructurePrefab` 아래에:

```csharp
        // v3 진입형 요새(원작 벙커): 외벽 4면 + 쥐 전용 입구(1.0×0.75 — v2 쥐구멍 실효치수).
        // 쥐 CC(0.44Ø/0.5h, skinWidth 포함 실효 0.60/0.63)만 통과, 고양이(0.86/1.03)는 차단.
        // 지붕 없음 — 점프가 없어 벽 1.2로 충분. 내부 회복은 FortressHealZone(반경 1.0).
        private const float FORTRESS_WALL_HEIGHT = 1.2f;
        private const float FORTRESS_HALF = 1.2f;          // 풋프린트 2.4×2.4
        private const float FORTRESS_DOOR_WIDTH = 1.0f;
        private const float FORTRESS_DOOR_HEIGHT = 0.75f;
        private const float FORTRESS_WALL_THICKNESS = 0.2f;

        private static GameObject CreateFortressPrefab()
        {
            Material wallMat = GridboxMaterial("Brown");
            var root = new GameObject("CheeseFortress");
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", 300);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                root.AddComponent<PlayerDeath>();
                root.AddComponent<FortressHealZone>();

                float h = FORTRESS_WALL_HEIGHT;
                float half = FORTRESS_HALF;
                float t = FORTRESS_WALL_THICKNESS;

                // 뒷벽·좌우벽 (입구는 로컬 +Z면)
                AddBox(root, wallMat, "Wall_Back", new Vector3(0f, h * 0.5f, -half + t * 0.5f),
                    new Vector3(half * 2f, h, t));
                AddBox(root, wallMat, "Wall_Left", new Vector3(-half + t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));
                AddBox(root, wallMat, "Wall_Right", new Vector3(half - t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));

                // 앞벽: 입구(폭 1.0) 좌우 세그먼트 + 인방(입구 위 y 0.75~1.2)
                float sideWidth = half - FORTRESS_DOOR_WIDTH * 0.5f; // 1.2-0.5 = 0.7
                float sideCenterX = FORTRESS_DOOR_WIDTH * 0.5f + sideWidth * 0.5f; // 0.85
                float frontZ = half - t * 0.5f;
                AddBox(root, wallMat, "Wall_Front_L", new Vector3(-sideCenterX, h * 0.5f, frontZ),
                    new Vector3(sideWidth, h, t));
                AddBox(root, wallMat, "Wall_Front_R", new Vector3(sideCenterX, h * 0.5f, frontZ),
                    new Vector3(sideWidth, h, t));
                float lintelHeight = h - FORTRESS_DOOR_HEIGHT;
                AddBox(root, wallMat, "Wall_Lintel",
                    new Vector3(0f, FORTRESS_DOOR_HEIGHT + lintelHeight * 0.5f, frontZ),
                    new Vector3(FORTRESS_DOOR_WIDTH, lintelHeight, t));

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, FortressPrefabPath);
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
```

**주의:** `AddBox`는 `parent.transform`에 로컬 배치하므로 프리팹 루트 회전 시 벽이 따라 돈다 — 입구 방향 = 루트 +Z ✓.

- [ ] **Step 3: CreatePlayerPrefab 시그니처 확장** — 파라미터 `GameObject decoyPrefab` 옆에 `GameObject fortressPrefab` 추가, `SetRef(shop, "m_DecoyPrefab", decoyPrefab);` 다음에:

```csharp
                SetRef(shop, "m_FortressPrefab", fortressPrefab);
```

(고양이 프리팹에도 참조는 들어가지만 카탈로그 Role 검증이 구매를 막는다 — v1과 동일.)

- [ ] **Step 4: CreateGameScene 확장** — 시그니처를 `(GameObject catPrefab, GameObject mousePrefab, GameObject fortressPrefab, GameObject decoyPrefab)`로, 본문에서:
  - `RegisterNetworkPrefabs(nm, catPrefab, mousePrefab, fortressPrefab, decoyPrefab);`
  - `SetRef(lobby, "mousePrefab", mousePrefab);` 다음에 `SetRef(lobby, "fortressPrefab", fortressPrefab);`
  - `VerifyUniqueHashes(...)` 체인에 `.Append(fortressPrefab.GetComponent<NetworkObject>())` 추가

- [ ] **Step 5: v1 가구 맵 복원 + 터널 삭제** — `BuildGreyboxMap`·`BuildTunnelWall`·터널 상수(`TUNNEL_WALL_POS`~`HOLES_EAST`) 전체를 다음으로 교체:

```csharp
        // v3 그레이박스(v1 부엌 복원): 외벽 + ㄱ자 카운터 + 아일랜드 + 가전/가구 블록.
        // 쥐 전용 지형 없음 — 안전은 요새(초기 배치 + 상점)가 담당. 아트는 8-B에서 교체.
        private static void BuildGreyboxMap()
        {
            Material wall = GridboxMaterial("Grey2");
            Material counter = GridboxMaterial("Grey4");
            var root = new GameObject("GreyboxMap");

            AddBox(root, wall, "Wall_N", new Vector3(0f, 1.5f, 15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_S", new Vector3(0f, 1.5f, -15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_E", new Vector3(15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));
            AddBox(root, wall, "Wall_W", new Vector3(-15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));

            // 부엌 블록아웃: ㄱ자 카운터 + 중앙 아일랜드 + 가전/가구 블록
            AddBox(root, counter, "Counter_N", new Vector3(-5f, 0.6f, 13.5f), new Vector3(14f, 1.2f, 2f));
            AddBox(root, counter, "Counter_W", new Vector3(-13.5f, 0.6f, 6f), new Vector3(2f, 1.2f, 13f));
            AddBox(root, counter, "Island", new Vector3(0f, 0.6f, 3f), new Vector3(6f, 1.2f, 3f));
            AddBox(root, GridboxMaterial("White"), "Fridge", new Vector3(13f, 1.25f, 12f), new Vector3(2f, 2.5f, 2f));
            AddBox(root, GridboxMaterial("Brown"), "Table", new Vector3(8f, 0.5f, -6f), new Vector3(4f, 1f, 4f));
            Material crate = GridboxMaterial("Orange");
            AddBox(root, crate, "Crate_A", new Vector3(-8f, 0.5f, -9f), new Vector3(2f, 1f, 2f));
            AddBox(root, crate, "Crate_B", new Vector3(-5.5f, 0.4f, -10.5f), new Vector3(1.2f, 0.8f, 1.2f));

            // 밸런스 패스(Plan 8): 조밀화 — 동쪽 골목·시야 차단물·구석 엄폐
            AddBox(root, counter, "Counter_E", new Vector3(13.5f, 0.6f, -1f), new Vector3(2f, 1.2f, 12f));
            Material wood = GridboxMaterial("Brown");
            AddBox(root, wood, "Table_B", new Vector3(-9f, 0.5f, -4f), new Vector3(4f, 1f, 4f));
            AddBox(root, wood, "Chair_A", new Vector3(5.5f, 0.5f, -6f), new Vector3(1f, 1f, 1f));
            AddBox(root, wood, "Chair_B", new Vector3(8f, 0.5f, -3f), new Vector3(1f, 1f, 1f));
            AddBox(root, crate, "Crate_C", new Vector3(12f, 0.5f, -8f), new Vector3(1.5f, 1f, 1.5f));
            AddBox(root, GridboxMaterial("White"), "Pantry", new Vector3(-13.5f, 1.25f, -13.5f), new Vector3(2f, 2.5f, 2f));
        }
```

- [ ] **Step 6: 치즈 지점 v3** — `CHEESE_SPOT_DEFS`와 주석 교체:

```csharp
        // 치즈 지점(원작 돈줄) — 구석=저등급(요새 인접), 중앙 개활지=고등급(고양이 순찰 한복판).
        // G1 (-9,-7) r2는 QA 고정: 쥐0 스폰(-11,-10.2)에서 클론 넛지 (2,0,3) 도달점.
        // G2는 v1 (-11.5,-11.5)에서 이동 — 요새0(-11,-12) 풋프린트와 겹침 회피.
        private static readonly (Vector3 pos, int grade)[] CHEESE_SPOT_DEFS =
        {
            (new Vector3(-9f, 0f, -7f), 1),        // QA — 쥐0 요새 앞마당
            (new Vector3(-13.5f, 0f, -10f), 2),    // 서남 구석(찬장 옆)
            (new Vector3(12f, 0f, -13f), 3),       // 동남 구석
            (new Vector3(-11f, 0f, 11f), 4),       // 북서 카운터 코너 안쪽
            (new Vector3(12f, 0f, 9f), 5),         // 냉장고 남쪽
            (new Vector3(-4f, 0f, -2f), 6),        // 중앙 서쪽 개활지
            (new Vector3(0f, 0f, -3f), 7),         // 아일랜드 남쪽 정중앙 — 최고 위험
        };
```

`BuildCheeseSpots`의 반경 조건 교체:

```csharp
                SetFloat(spot, "m_Radius", grade == 1 ? 2f : 1.5f); // QA 지점(G1)만 넉넉히
```

- [ ] **Step 7: 타이머 라벨** — `BuildLobbyCanvas`의 `timerText.text = "5:00";` → `timerText.text = "10:00";`, 주석 `// 매치 타이머 (상단 중앙, v2)` → `// 매치 타이머 (상단 중앙 — v3 안전 타이머)`

- [ ] **Step 8: 해시 테스트 등록** — `NetworkObjectHashTests.ASSET_PATHS`에:

```csharp
            "Assets/Prefabs/CheeseFortress.prefab",
```

(`MouseDecoy.prefab` 줄 다음)

- [ ] **Step 9: 컴파일 → 씬 재생성 → EditMode** —
  1. refresh + focus-unity → `u_console` CS 에러 0
  2. `u_editor_menu`: `Tools/CTC/Setup Networking Scene` → 콘솔에 "완료 — 프리팹 4종" 로그·해시 에러 0
  3. EditMode → Expected: **Passed: 81** (해시 테스트가 CheeseFortress 포함 검증)

- [ ] **Step 10: Commit** — 씬·프리팹 포함:

```bash
git add -A CTC/Assets/Scripts CTC/Assets/Prefabs CTC/Assets/Scenes CTC/Assets/DefaultNetworkPrefabs.asset CTC/Assets/Materials
git commit -m "feat: rebuild scene — v1 kitchen map, enterable fortress prefab, v3 cheese spots

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: MPPM 2인 검증 (메인 세션 직접 — 서브에이전트 금지)

사전: MPPM Player 2 활성(꺼져 있으면 사용자에게 토글 요청). 표준 절차는 핸드오프 "검증 방법" 참조 — 호스트=고양이, 클론=쥐.

| # | 시나리오 | 방법 | 기대 |
|---|---|---|---|
| 1 | 접속·시작·타이머 | 표준 절차 → `mppm_collect` | matchRemaining ≈ 600에서 감소, TimerHud "9:5x" 렌더(스크린샷) |
| 2 | 초기 요새·스폰 | `mppm_collect` objects | CheeseFortress 1개(위치 ≈ -11,-12), 클론 쥐 pos ≈ (-11, -10.2), 호스트 고양이 (0,0) |
| 3 | 시작 치즈 0 | `mppm_collect` | 쥐·고양이 cheese 0 (유지비 없음 — 30s 대기 후에도 hp 100 유지) |
| 4 | G1 수입 | 클론 `TestMpNudgeLocalPlayer` → 10s | 클론 쥐 cheese 증가(+1/s) |
| 5 | 내부 회복 | 호스트 고양이 `TestTeleportToOtherPlayer`+`TestPounce`로 쥐 hp 40 → 클론 `TestMpEnterNearestFortress` → 10s | 쥐 hp +10/s 회복 (100캡) |
| 6 | 파운스 차폐 | 쥐가 요새 안(시나리오 5 상태)일 때 호스트 `TestTeleportLocalPlayer(-11, -9.5)`(입구 정면 밖) → `TestPounce` | 쥐 hp 불변 — 요새 Health만 감소 |
| 7 | 요새 파괴 | `TestPounce` 반복(요새 HP 300 / 60dmg = 5방, 쿨 2.5s) | CheeseFortress despawn, 보상 없음(고양이 cheese 불변) |
| 8 | 상점 요새 | `TestGrantCheeseAll` → 클론 `TestMpBuyFortress` | 새 CheeseFortress 스폰(쥐 전방 1.5m), cheese −220. 상점 UI(B키) 버튼 7개 렌더 |
| 9 | 생존승 | wesqa `TestExpireTimer` → 3s | matchResult=1 (MouseWin), "MICE WIN!" |
| 10 | 회귀 | 재시작 → 파운스 2방 쥐 처치 → CatWin. EditMode 81·재시작 후 요새 despawn·포트 잔존 0 | 전부 통과 |

**판정:** 기술 문제(에러·배선 누락·수치 이상)는 자동 수정 후 재시도. 기획적 진행 불가만 보고 중단.

완료 시: 핸드오프 문서 갱신 없이 결과만 보고 (핸드오프는 인수인계 시점에만).

## 검증 요약 (완료 기준)

EditMode **81/81** · 초기 요새 쥐당 1개 스폰·입구 앞 스폰 · 내부 회복/차폐/파괴 정상 · 유지비 부재(치즈 0 시작·hp 유지) · 타이머 600s 생존승 · 상점 7항목(요새 220) · 기존 전투/재시작 회귀 무결
