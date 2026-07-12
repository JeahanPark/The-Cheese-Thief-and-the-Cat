# Plan 9: v2 생존 개정 (타이머·유지비·쥐 터널·요새 삭제) — 구현 계획

> **상태: ✅ 구현 완료** (2026-07-12, EditMode 80/80 · MPPM 검증: 타이머/유지비/굶주림/생존승/CatWin 회귀. 터널은 치수 보장(구멍 0.6 < 고양이 Ø0.7)+구멍 강제배치 밀림 확인 — 이동 실증은 플레이테스트 항목. 부수 수정: WesQABootstrap 서버 teardown 누락으로 인한 5001 좀비 리스너 해결)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스펙 v2 반영 — 5분 타이머(쥐 생존승), 치즈 유지비/굶주림, 쥐 전용 터널 맵, 요새 삭제.

**Architecture:** 타이머는 `MatchManager`에 NetworkVariable로 추가(만료 판정은 순수 `MatchFlowLogic.EvaluateAtTimeUp` TDD). 유지비는 새 `CheeseUpkeep`(쥐 프리팹, 서버 10초 틱: `TrySpend` 실패 시 `TakeDamage`) — 수치는 순수 `SurvivalLogic` 상수. 터널은 **콜라이더 치수만으로** 구현(구멍 0.6×0.6 + 인방 박스 — 쥐 CC 0.5/Ø0.44 통과, 고양이 0.9/Ø0.7 차단, 로직 없음). 요새는 카탈로그·프리팹·컴포넌트 전면 제거.

**Tech Stack:** 기존 NGO·씬 재생성 파이프라인·EditMode TDD. 신규 패키지 없음.

**스펙 근거:** [기획서 v2](../specs/2026-07-06-cheese-thief-and-cat-design.md) §4(승패)·§7(유지비)·§8(터널)·§12-A(수치)

## 설계 결정 (v2 확정)

| 항목 | 값 |
|---|---|
| 매치 시간 | 300s — 만료 시 생존 쥐 ≥1 → MouseWin, 0 → CatWin |
| 유지비 | 10초마다 치즈 −10, 부족 시 HP −10 (치즈는 있는 만큼만 차감) |
| 시작 치즈 | 30 (쥐만, 서버가 스폰 시 지급) |
| 회복 | 없음 (변경 아님 — 확인 사항) |
| 터널 | 외곽 링(내벽 ±13.2, 높이 1.2) + 쥐구멍 9개(0.6×0.6) + 인방(y 0.6~1.2) |
| 맵 사물 | 전부 순수 지형 박스 — 가구 조형 없음 (아트는 8-B) |
| 요새 | ShopItemId 유지하되 카탈로그·적용 로직·프리팹·FortressHealZone 삭제 |
| 타이머 HUD | 상단 중앙 "3:47" — 게임 중만 표시 |

---

## 실행 환경 주의 (모든 태스크 공통)

- 컴파일: `powershell -NoProfile -File tools/focus-unity.ps1` → `u_console` CS 에러 0
- EditMode: `Tools/CTC/Run EditMode Tests`. **현재 Passed 77.** 이 계획 완료 시 **80** (ShopLogic −1 · MatchFlow +2 · Survival +2)
- QA 호스트 시작은 wesqa `TestStartHost` (직접 StartHost 금지)
- 신규 코드 `m_`/`_` 컨벤션 · 기존 `_camelCase` 파일은 유지 · 커밋 규약은 기존 계획서와 동일

## 파일 구조

| 파일 | 작업 |
|---|---|
| `Assets/Scripts/Match/MatchFlowLogic.cs` | `EvaluateAtTimeUp` 추가 |
| `Assets/Scripts/Match/MatchManager.cs` | 타이머 NetworkVariable·만료 판정·재시작 리셋·`DevSetRemaining` |
| `Assets/Scripts/Match/TimerHud.cs` (신규) | 상단 중앙 mm:ss |
| `Assets/Scripts/Economy/SurvivalLogic.cs` (신규) | 유지비 상수·굶주림 수학 (순수) |
| `Assets/Scripts/Economy/CheeseUpkeep.cs` (신규) | 서버 10초 틱: 차감/굶주림 |
| `Assets/Scripts/Economy/ShopLogic.cs` | 카탈로그에서 Fortress 제거 |
| `Assets/Scripts/Economy/ShopController.cs` | Fortress 케이스·m_FortressPrefab 제거 |
| `Assets/Scripts/Economy/ShopHud.cs` | ITEM_LABELS에서 "Fortress" 제거 |
| `Assets/Scripts/Economy/FortressHealZone.cs` | **삭제** (+.meta) |
| `Assets/Prefabs/CheeseFortress.prefab` | **삭제** (+.meta) — 씬 재생성이 등록도 해제 |
| `Assets/Scripts/Editor/SetupNetworkingScene.cs` | 요새 생성 제거·터널 링 빌더·맵 v2·TimerHud 배선·쥐 시작치즈는 코드(스폰) |
| `Assets/Scripts/Lobby/LobbyManager.cs` | 쥐 스폰 시 시작 치즈 지급 |
| `Assets/Scripts/Testing/TestManager.cs` | TestBuyFortress 제거·`TestExpireTimer` 추가 |
| `Assets/Scripts/Network/MultiplayerQaProbe.cs` | TestMpBuyFortress 제거·`matchRemaining` 노출 |
| `Assets/Scripts/Tests/EditMode/ShopLogicTests.cs` | Fortress 테스트 제거 |
| `Assets/Scripts/Tests/EditMode/MatchFlowLogicTests.cs` | 만료 판정 +2 |
| `Assets/Scripts/Tests/EditMode/SurvivalLogicTests.cs` (신규) | +2 |
| `Assets/Scripts/Tests/EditMode/NetworkObjectHashTests.cs` | CheeseFortress 경로 제거 |

---

### Task 1: MatchFlowLogic 만료 판정 + SurvivalLogic (TDD)

- [ ] **Step 1: 테스트 추가** — `MatchFlowLogicTests.cs`에:

```csharp
        [Test]
        public void EvaluateAtTimeUp_MiceAlive_MouseWin()
        {
            Assert.AreEqual(MatchResult.MouseWin, MatchFlowLogic.EvaluateAtTimeUp(_aliveMice: 1));
        }

        [Test]
        public void EvaluateAtTimeUp_NoMice_CatWin()
        {
            Assert.AreEqual(MatchResult.CatWin, MatchFlowLogic.EvaluateAtTimeUp(_aliveMice: 0));
        }
```

신규 `Assets/Scripts/Tests/EditMode/SurvivalLogicTests.cs`:

```csharp
using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    public class SurvivalLogicTests
    {
        [Test]
        public void UpkeepSpend_CappedAtBalance()
        {
            Assert.AreEqual(10, SurvivalLogic.UpkeepSpend(30));
            Assert.AreEqual(7, SurvivalLogic.UpkeepSpend(7));
            Assert.AreEqual(0, SurvivalLogic.UpkeepSpend(0));
        }

        [Test]
        public void StarvationDamage_OnlyWhenShort()
        {
            Assert.AreEqual(0, SurvivalLogic.StarvationDamage(10));
            Assert.AreEqual(10, SurvivalLogic.StarvationDamage(9));
            Assert.AreEqual(10, SurvivalLogic.StarvationDamage(0));
        }
    }
}
```

`ShopLogicTests.cs`에서 `Find_Fortress_MatchesOriginalBunker` 테스트 **삭제**.

- [ ] **Step 2: 구현** — `MatchFlowLogic.cs`에:

```csharp
        /// <summary>타이머 만료 시(v2): 생존 쥐가 있으면 쥐 생존승, 없으면 고양이 승.</summary>
        public static MatchResult EvaluateAtTimeUp(int _aliveMice)
        {
            return _aliveMice > 0 ? MatchResult.MouseWin : MatchResult.CatWin;
        }
```

신규 `Assets/Scripts/Economy/SurvivalLogic.cs`:

```csharp
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// v2 생존 수학(순수): 치즈 유지비·굶주림. 치즈 = 식량이자 화폐 —
    /// 유지비 틱마다 치즈를 소모하고, 부족하면 HP를 대신 소모한다.
    /// </summary>
    public static class SurvivalLogic
    {
        public const float MATCH_DURATION_SECONDS = 300f;
        public const float UPKEEP_INTERVAL_SECONDS = 10f;
        public const int UPKEEP_COST = 10;
        public const int STARVATION_DAMAGE = 10;
        public const int STARTING_CHEESE = 30;

        /// <summary>이번 틱에 실제로 차감할 치즈 — 잔액만큼만.</summary>
        public static int UpkeepSpend(int _cheese)
        {
            return Mathf.Min(_cheese, UPKEEP_COST);
        }

        /// <summary>잔액이 유지비에 못 미치면 HP로 대신 낸다.</summary>
        public static int StarvationDamage(int _cheese)
        {
            return _cheese >= UPKEEP_COST ? 0 : STARVATION_DAMAGE;
        }
    }
}
```

- [ ] **Step 3: CheeseWallet에 부분 차감용 서버 메서드 추가** (`TrySpend` 아래):

```csharp
        /// <summary>서버 전용. 잔액 한도 내에서 있는 만큼 차감(유지비 — 부족해도 남은 걸 다 먹음).</summary>
        public void SpendUpTo(int _amount)
        {
            if (!IsServer || _amount <= 0) return;
            m_Cheese.Value -= Mathf.Min(m_Cheese.Value, _amount);
        }
```

(`using UnityEngine;` 필요 시 추가)

- [ ] **Step 4: 컴파일 → EditMode** — Expected: `Passed: 80, Failed: 0`
- [ ] **Step 5: Commit** — `feat: add time-up verdict and survival upkeep math (TDD)`

---

### Task 2: MatchManager 타이머 + TimerHud

- [ ] **Step 1: MatchManager 수정**

필드 추가:

```csharp
        // 남은 시간(초). 서버 기록, 전원 읽기(TimerHud). 시작 전/재시작 후엔 음수(-1)로 비활성.
        private readonly NetworkVariable<float> m_RemainingSeconds = new NetworkVariable<float>(-1f);

        private bool m_WasStarted;

        public float RemainingSeconds => m_RemainingSeconds.Value;
```

`Update()`를 다음으로 교체:

```csharp
        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            bool started = m_Lobby != null && m_Lobby.GameStarted;
            if (started && !m_WasStarted)
            {
                m_RemainingSeconds.Value = Economy.SurvivalLogic.MATCH_DURATION_SECONDS;
            }
            m_WasStarted = started;

            if (!started || Result != MatchResult.None) return;

            // 타이머
            m_RemainingSeconds.Value = Mathf.Max(0f, m_RemainingSeconds.Value - Time.deltaTime);

            m_Timer += Time.deltaTime;
            if (m_Timer < POLL_INTERVAL_SECONDS && m_RemainingSeconds.Value > 0f) return;
            m_Timer = 0f;

            CountAlive(out int cats, out int mice);
            if (MatchFlowLogic.ShouldArm(started, m_Armed, cats, mice)) m_Armed = true;

            MatchResult result = m_RemainingSeconds.Value <= 0f && m_Armed
                ? MatchFlowLogic.EvaluateAtTimeUp(mice)
                : MatchFlowLogic.Evaluate(m_Armed, cats, mice);
            if (result == MatchResult.None) return;

            m_Result.Value = (int)result;
            Debug.Log($"[MatchManager] 매치 종료: {result} (cats={cats}, mice={mice}, 남은시간={m_RemainingSeconds.Value:F0}s)");
        }
```

`RequestRestart()`의 리셋부에 추가:

```csharp
            m_RemainingSeconds.Value = -1f;
```

QA용 메서드 추가 (`RequestRestart` 아래):

```csharp
        /// <summary>QA: 타이머 강제 설정(서버 전용) — 만료 판정 검증용.</summary>
        public void DevSetRemaining(float _seconds)
        {
            if (IsServer) m_RemainingSeconds.Value = _seconds;
        }
```

- [ ] **Step 2: TimerHud 작성** — `Assets/Scripts/Match/TimerHud.cs`:

```csharp
using CTC.Runtime.Lobby;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>상단 중앙 매치 타이머(mm:ss). 게임 중에만 표시.</summary>
    public class TimerHud : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private MatchManager m_Match;
        [SerializeField] private TMP_Text m_Text;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted
                        && m_Match != null && m_Match.RemainingSeconds >= 0f;
            if (m_Text.gameObject.activeSelf != show) m_Text.gameObject.SetActive(show);
            if (!show) return;

            int total = Mathf.CeilToInt(m_Match.RemainingSeconds);
            m_Text.text = $"{total / 60}:{total % 60:00}";
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인 → Commit** — `feat: add 5-minute match timer with survival verdict`

---

### Task 3: CheeseUpkeep + 시작 치즈

- [ ] **Step 1: CheeseUpkeep 작성** — `Assets/Scripts/Economy/CheeseUpkeep.cs`:

```csharp
using CTC.Runtime.Combat;
using CTC.Runtime.Match;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// v2 유지비(쥐 전용): 서버가 10초마다 치즈를 소모시키고, 부족하면 HP를 대신 깎는다.
    /// 매치 진행 중(결과 없음)에만 동작 — 결과 화면에서 굶어 죽는 것 방지.
    /// </summary>
    [RequireComponent(typeof(CheeseWallet), typeof(Health))]
    public class CheeseUpkeep : NetworkBehaviour
    {
        private CheeseWallet m_Wallet;
        private Health m_Health;
        private MatchManager m_Match;
        private float m_Timer;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            m_Wallet = GetComponent<CheeseWallet>();
            m_Health = GetComponent<Health>();
            m_Match = FindFirstObjectByType<MatchManager>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || m_Health.IsDead) return;
            if (m_Match != null && m_Match.Result != MatchResult.None) return;

            m_Timer += Time.deltaTime;
            if (m_Timer < SurvivalLogic.UPKEEP_INTERVAL_SECONDS) return;
            m_Timer -= SurvivalLogic.UPKEEP_INTERVAL_SECONDS;

            int hunger = SurvivalLogic.StarvationDamage(m_Wallet.Cheese);
            m_Wallet.SpendUpTo(SurvivalLogic.UPKEEP_COST);
            if (hunger > 0) m_Health.TakeDamage(hunger);
        }
    }
}
```

- [ ] **Step 2: 시작 치즈** — `LobbyManager.SpawnPlayers()`의 스폰 직후(`SpawnAsPlayerObject` 다음 줄)에:

```csharp
                if (p.Role == RoleId.Mouse)
                {
                    go.GetComponent<Economy.CheeseWallet>().AddCheese(Economy.SurvivalLogic.STARTING_CHEESE);
                }
```

- [ ] **Step 3: 프리팹 부착** — SetupNetworkingScene `CreatePlayerPrefab`의 쥐 분기(`MousePopgun`/`MouseDash` 추가부)에 `root.AddComponent<CheeseUpkeep>();` 추가

- [ ] **Step 4: 컴파일 확인 → Commit** — `feat: add cheese upkeep with starvation (v2 survival economy)`

---

### Task 4: 요새 제거

- [ ] **Step 1:** `ShopLogic.CATALOG`에서 Fortress 줄 삭제 (enum 값은 유지 — 주석 `// Fortress = 5 → v2 삭제`)
- [ ] **Step 2:** `ShopController` — `m_FortressPrefab` 필드·CanApply Fortress 케이스·Apply Fortress 케이스 삭제
- [ ] **Step 3:** `ShopHud.ITEM_LABELS`에서 `"Fortress"` 삭제 (CATALOG 순서와 일치 유지: Slingshot, Machine Gun, Dash, Decoy, ATK Up, SPD Up)
- [ ] **Step 4:** `FortressHealZone.cs`(+.meta)·`CheeseFortress.prefab`(+.meta) 삭제, `NetworkObjectHashTests.ASSET_PATHS`에서 경로 제거
- [ ] **Step 5:** `TestManager.TestBuyFortress`·프로브 `TestMpBuyFortress` 케이스 삭제
- [ ] **Step 6:** SetupNetworkingScene — `CreateStructurePrefab("CheeseFortress"...)` 호출·`FortressPrefabPath`·`fortressPrefab` 파라미터 흐름 제거 (미끼는 유지 — decoyPrefab만 전달), `RegisterNetworkPrefabs`·`VerifyUniqueHashes`에서 fortress 제거. **주의:** `DefaultNetworkPrefabs.asset`에 남은 CheeseFortress 항목은 씬 재생성 후 에셋에서 수동 확인(리스트에 null 잔존 시 정리 코드가 필요할 수 있음 — RegisterNetworkPrefabs에 null/missing 정리 루프 추가 권장)
- [ ] **Step 7:** 컴파일 → Commit — `feat: remove fortress (v2 — safety moves to terrain tunnels)`

---

### Task 5: 맵 v2 — 쥐 터널 링 (SetupNetworkingScene)

- [ ] **Step 1: BuildGreyboxMap 교체** — 가구 조형 전부 제거, 순수 지형만:

```csharp
        // v2 그레이박스: 외벽 + 쥐 터널 링(내벽·쥐구멍·인방) + 중앙 차단 블록.
        // 사물 조형 없음 — 아트 패스(8-B)에서 교체. 구멍 치수는 SPEC §12-A (0.6×0.6).
        private const float TUNNEL_WALL_POS = 13.2f;   // 내벽 중심 좌표
        private const float HOLE_WIDTH = 0.6f;
        private const float HOLE_HEIGHT = 0.6f;
        private const float TUNNEL_WALL_HEIGHT = 1.2f;

        // 변마다 쥐구멍 위치(해당 축 좌표) — 9개
        private static readonly float[] HOLES_NORTH = { -7f, 0f, 7f };
        private static readonly float[] HOLES_SOUTH = { -5f, 7f };
        private static readonly float[] HOLES_WEST = { -6f, 6f };
        private static readonly float[] HOLES_EAST = { -6f, 6f };

        private static void BuildGreyboxMap()
        {
            Material wall = GridboxMaterial("Grey2");
            Material tunnelWall = GridboxMaterial("Brown");
            var root = new GameObject("GreyboxMap");

            AddBox(root, wall, "Wall_N", new Vector3(0f, 1.5f, 15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_S", new Vector3(0f, 1.5f, -15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_E", new Vector3(15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));
            AddBox(root, wall, "Wall_W", new Vector3(-15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));

            // 쥐 터널 내벽 4변 (구멍 뚫린 세그먼트)
            BuildTunnelWall(root, tunnelWall, "N", true, TUNNEL_WALL_POS, HOLES_NORTH);
            BuildTunnelWall(root, tunnelWall, "S", true, -TUNNEL_WALL_POS, HOLES_SOUTH);
            BuildTunnelWall(root, tunnelWall, "E", false, TUNNEL_WALL_POS, HOLES_EAST);
            BuildTunnelWall(root, tunnelWall, "W", false, -TUNNEL_WALL_POS, HOLES_WEST);

            // 중앙 차단 블록 (시야 차단·술래잡기 기둥 — 순수 지형)
            Material block = GridboxMaterial("Grey4");
            AddBox(root, block, "Block_Center", new Vector3(0f, 0.9f, 3f), new Vector3(6f, 1.8f, 3f));
            AddBox(root, block, "Block_SE", new Vector3(8f, 0.9f, -6f), new Vector3(3f, 1.8f, 3f));
            AddBox(root, block, "Block_SW", new Vector3(-8f, 0.9f, -5f), new Vector3(3f, 1.8f, 3f));
        }

        // 내벽 한 변: 구멍(폭 0.6)마다 벽을 끊고 인방(y 0.6~1.2)을 얹는다.
        // 쥐 CC(높이 0.5·Ø0.44)만 구멍 통과 — 고양이(0.9·Ø0.7)는 위·옆 다 걸림.
        private static void BuildTunnelWall(
            GameObject parent, Material material, string side, bool horizontal, float fixedCoord, float[] holes)
        {
            const float EXTENT = 13.2f; // 변 길이 절반 (모서리에서 만나 링이 닫힘)
            System.Array.Sort(holes);

            float cursor = -EXTENT;
            for (int i = 0; i <= holes.Length; i++)
            {
                float segEnd = i < holes.Length ? holes[i] - HOLE_WIDTH * 0.5f : EXTENT;
                float length = segEnd - cursor;
                if (length > 0.01f)
                {
                    float center = (cursor + segEnd) * 0.5f;
                    AddBox(parent, material, $"Tunnel_{side}_Seg{i}",
                        horizontal ? new Vector3(center, TUNNEL_WALL_HEIGHT * 0.5f, fixedCoord)
                                   : new Vector3(fixedCoord, TUNNEL_WALL_HEIGHT * 0.5f, center),
                        horizontal ? new Vector3(length, TUNNEL_WALL_HEIGHT, 0.4f)
                                   : new Vector3(0.4f, TUNNEL_WALL_HEIGHT, length));
                }

                if (i < holes.Length)
                {
                    // 인방 — 구멍 위(y 0.6~1.2)를 막아 고양이 통과 차단
                    float lintelHeight = TUNNEL_WALL_HEIGHT - HOLE_HEIGHT;
                    AddBox(parent, material, $"Tunnel_{side}_Lintel{i}",
                        horizontal ? new Vector3(holes[i], HOLE_HEIGHT + lintelHeight * 0.5f, fixedCoord)
                                   : new Vector3(fixedCoord, HOLE_HEIGHT + lintelHeight * 0.5f, holes[i]),
                        horizontal ? new Vector3(HOLE_WIDTH, lintelHeight, 0.4f)
                                   : new Vector3(0.4f, lintelHeight, HOLE_WIDTH));
                    cursor = holes[i] + HOLE_WIDTH * 0.5f;
                }
            }
        }
```

- [ ] **Step 2: 치즈 지점 재배치** — `CHEESE_SPOT_DEFS` 교체 (등급 = 쥐구멍까지 거리, G1 QA 불변):

```csharp
        private static readonly (Vector3 pos, int grade)[] CHEESE_SPOT_DEFS =
        {
            (new Vector3(3f, 0f, 1f), 1),          // QA — 중앙이지만 r2 (기존 절차 유지)
            (new Vector3(-5f, 0f, -12f), 2),       // 남쪽 구멍(-5) 코앞
            (new Vector3(7f, 0f, -12f), 3),        // 남쪽 구멍(7) 코앞
            (new Vector3(-12f, 0f, 6f), 4),        // 서쪽 구멍(6) 옆
            (new Vector3(12f, 0f, -6f), 5),        // 동쪽 구멍(-6) 옆
            (new Vector3(-5f, 0f, 5f), 6),         // 중앙 서쪽 — 구멍까지 ~8m
            (new Vector3(0f, 0f, -3f), 7),         // 정중앙 — 최고 위험
        };
```

(터널 내부(±13.2~15)에는 지점 두지 않음 — 터널 안 수입 0 원칙)

- [ ] **Step 3: TimerHud 배선** — `BuildLobbyCanvas`(ResultPanel 배선 뒤)에 상단 중앙 텍스트 + 컴포넌트:

```csharp
            // 매치 타이머 (상단 중앙)
            var timerTextGo = new GameObject("TimerText");
            timerTextGo.transform.SetParent(canvasGo.transform, false);
            var timerText = timerTextGo.AddComponent<TextMeshProUGUI>();
            timerText.text = "5:00";
            timerText.fontSize = 40f;
            timerText.alignment = TextAlignmentOptions.Top;
            var timerRect = timerTextGo.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0f, -16f);
            timerRect.sizeDelta = new Vector2(200f, 52f);
            timerTextGo.SetActive(false);

            var timerHud = canvasGo.AddComponent<TimerHud>();
            SetRef(timerHud, "m_Lobby", lobby);
            SetRef(timerHud, "m_Match", match);
            SetRef(timerHud, "m_Text", timerText);
```

- [ ] **Step 4: QA 훅** — TestManager에 (`TestRestartMatch` 옆):

```csharp
        /// <summary>서버 전용: 타이머를 3초로 — 생존승 판정 QA.</summary>
        public void TestExpireTimer()
        {
            var match = FindFirstObjectByType<MatchManager>();
            if (match == null) { GameDebug.LogWarning("[TestManager] MatchManager 없음"); return; }
            match.DevSetRemaining(3f);
        }
```

프로브 `ProbeSnapshot`에 `public int matchRemaining;` 추가, `BuildSnapshot()`의 matchResult 옆에:

```csharp
            s.matchRemaining = match != null && match.IsSpawned ? Mathf.CeilToInt(match.RemainingSeconds) : -1;
```

- [ ] **Step 5: 컴파일 → 씬 재생성 → EditMode 80 → Commit** — `feat: rebuild map with mouse tunnel ring and match timer HUD (v2)`

---

### Task 6: MPPM 2인 검증 (메인 세션 직접)

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 접속·시작 | matchRemaining ≈ 300에서 감소, TimerHud "4:5x" 렌더(스크린샷) |
| 2 | 시작 치즈 | 쥐 cheese 30 (고양이 0) |
| 3 | 유지비 | 10초 후 쥐 cheese 30→20 (지점 밖 대기 시) |
| 4 | 굶주림 | 치즈 소진 후 틱마다 hp −10 |
| 5 | 터널 물리 | u_set_transform으로 쥐를 구멍 앞 배치 → 클론 넛지로 통과(터널 안 좌표). 고양이는 같은 구멍에서 막힘(위치 불변) |
| 6 | 생존승 | wesqa `TestExpireTimer` → 3초 후 matchResult=1(MouseWin), "MICE WIN!" |
| 7 | 시간 내 전멸 | 재시작 → 파운스로 쥐 처치 → matchResult=2 (기존 CatWin 회귀) |
| 8 | 요새 부재 | 상점에 Fortress 버튼 없음(6개), TestMpBuyFortress 거부 로그 |
| 9 | 회귀 | EditMode 80·해시 테스트(프리팹 3종)·포트 0·재시작 후 타이머 리셋 |

## 검증 요약 (완료 기준)

EditMode **80/80** · 타이머 감소/만료 생존승 · 유지비/굶주림 수치 정확 · 터널 쥐 통과/고양이 차단 · 요새 완전 제거 · 기존 상점/전투/재시작 회귀 무결
