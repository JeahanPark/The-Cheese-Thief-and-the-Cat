# Plan 4: 치즈 경제 (등급제 점유 수입) — 구현 계획

> **상태: ✅ 구현 완료** (2026-07-12, MPPM 검증 통과 — [로드맵](2026-07-06-mvp-roadmap.md))

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 원작 약올리기 돈줄 구조 — 등급별 치즈 지점에 **머무는 동안 초당 수입**(지점당 1마리), 개인 지갑 네트워크 동기화, 잔액 HUD.

**Architecture:** 수입 규칙(등급→초당 수입, 선점자 선정, 소수 누적)은 순수 `CheeseSpotLogic`으로 분리해 EditMode TDD. `CheeseSpot`(씬 오브젝트)이 서버에서만 반경 검사→선점자에게 지급, 동기화는 플레이어의 `CheeseWallet`(NetworkVariable) 하나로 끝냄 — 지점 자체는 네트워크 오브젝트가 아님. HUD는 기존 LobbyCanvas에 TMP 텍스트 추가.

**Tech Stack:** Unity 6000.5, NGO 2.12(NetworkVariable, 서버 권위), TextMeshPro, 기존 SetupNetworkingScene 재생성 파이프라인.

**스펙:** [../specs/2026-07-06-cheese-thief-and-cat-design.md](../specs/2026-07-06-cheese-thief-and-cat-design.md) §5·§7 · **로드맵 Plan 4**

---

## 실행 환경 주의 (모든 태스크 공통)

- 컴파일 확인: CTC에서 `powershell -NoProfile -File tools/focus-unity.ps1` → MCP `u_console` CS 에러 0. `[McpBridge] Pipe is broken`은 노이즈.
- EditMode 테스트: `u_editor_menu`로 `Tools/CTC/Run EditMode Tests` → `C:/Users/user/AppData/LocalLow/DefaultCompany/CTC/TestResults.xml` 확인. **현재 Passed 32 / Failed 0.**
- Unity는 컴파일 에러 시 테스트 실행 불가 → TDD 태스크는 "테스트+잘못된 값 반환 스텁"을 같이 작성해 RED 확인.
- **신규 코드 컨벤션**: `m_` 멤버, `_` 매개변수, public 필드 금지, 상수 UPPER_SNAKE_CASE (CLAUDE.md — Plan 3 파일들과 다름, 신규부터 적용).
- 레거시 `Input.*` 금지 · CC 오브젝트 transform 위치 직접 세팅 금지.
- 커밋: 저장소 루트(`c:\GitFork\The-Cheese-Thief-and-the-Cat`)에서 `CTC/` 접두, .meta 포함, 끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`. `ProjectSettings/*`·`Assets/Settings/*`·`.claude/*`·`screenshot.png` 커밋 금지.
- MPPM 클론이 켜져 있으면 코드 작업 중엔 무시(플레이 안 하면 무해). 패키지 변경 없음.

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Economy/CheeseSpotLogic.cs` (신규) | 순수 규칙: 등급→수입, 선점자 선정, 소수 누적 |
| `Assets/Scripts/Economy/CheeseWallet.cs` (신규) | 개인 지갑 NetworkVariable (서버 기록·전원 읽기) |
| `Assets/Scripts/Economy/CheeseSpot.cs` (신규) | 씬 지점: 서버 틱, 반경 내 선점자에게 지급 |
| `Assets/Scripts/Economy/WalletHud.cs` (신규) | 자기 잔액 HUD (게임 시작 후 표시) |
| `Assets/Scripts/Tests/EditMode/CheeseSpotLogicTests.cs` (신규) | TDD |
| `Assets/Scripts/Network/MultiplayerQaProbe.cs` (수정) | 스냅샷에 지갑 잔액 노출 (MPPM 검증용) |
| `Assets/Scripts/Editor/SetupNetworkingScene.cs` (수정) | 프리팹에 지갑 추가, 지점 6개 배치, HUD 배선 |

---

### Task 1: CheeseSpotLogic (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/CheeseSpotLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/CheeseSpotLogicTests.cs`

- [ ] **Step 1: 테스트 + 스텁 작성**

`Assets/Scripts/Tests/EditMode/CheeseSpotLogicTests.cs`:

```csharp
using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CheeseSpotLogicTests
    {
        [Test]
        public void IncomePerSecond_EqualsGrade()
        {
            Assert.AreEqual(3, CheeseSpotLogic.IncomePerSecond(3));
        }

        [Test]
        public void IncomePerSecond_ClampsBelowMin()
        {
            Assert.AreEqual(1, CheeseSpotLogic.IncomePerSecond(0));
        }

        [Test]
        public void IncomePerSecond_ClampsAboveMax()
        {
            Assert.AreEqual(7, CheeseSpotLogic.IncomePerSecond(99));
        }

        [Test]
        public void SelectEarner_Empty_ReturnsNoEarner()
        {
            Assert.AreEqual(CheeseSpotLogic.NO_EARNER,
                CheeseSpotLogic.SelectEarner(CheeseSpotLogic.NO_EARNER, new ulong[0]));
        }

        [Test]
        public void SelectEarner_NoCurrent_PicksFirst()
        {
            Assert.AreEqual(5UL, CheeseSpotLogic.SelectEarner(CheeseSpotLogic.NO_EARNER, new ulong[] { 5, 7 }));
        }

        [Test]
        public void SelectEarner_CurrentStillPresent_Retained()
        {
            // 지점당 1마리 — 나중에 온 쥐(5)가 있어도 선점자(7) 유지
            Assert.AreEqual(7UL, CheeseSpotLogic.SelectEarner(7UL, new ulong[] { 5, 7 }));
        }

        [Test]
        public void SelectEarner_CurrentLeft_PicksFirstRemaining()
        {
            Assert.AreEqual(5UL, CheeseSpotLogic.SelectEarner(7UL, new ulong[] { 5 }));
        }

        [Test]
        public void Accumulate_BelowOne_NoPayout()
        {
            float rem = CheeseSpotLogic.Accumulate(0f, 1, 0.4f, out int whole);
            Assert.AreEqual(0, whole);
            Assert.AreEqual(0.4f, rem, 1e-4f);
        }

        [Test]
        public void Accumulate_CrossesOne_PaysAndKeepsRemainder()
        {
            float rem = CheeseSpotLogic.Accumulate(0.4f, 1, 0.7f, out int whole);
            Assert.AreEqual(1, whole);
            Assert.AreEqual(0.1f, rem, 1e-4f);
        }

        [Test]
        public void Accumulate_HighGrade_PaysProportionally()
        {
            // 등급 7, 1초 → 7치즈
            float rem = CheeseSpotLogic.Accumulate(0f, 7, 1f, out int whole);
            Assert.AreEqual(7, whole);
            Assert.AreEqual(0f, rem, 1e-4f);
        }
    }
}
```

`Assets/Scripts/Economy/CheeseSpotLogic.cs` (스텁 — 일부러 틀린 값):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점 수입 규칙(원작 약올리기 돈줄 대응). MonoBehaviour와 분리된
    /// 순수 함수라 EditMode 테스트가 가능하다.
    /// - 등급(1~7) = 초당 수입 (임시 밸런스 — 튜닝은 스펙 §12)
    /// - 지점당 동시 수입 1마리: 선점자가 남아 있는 한 유지
    /// </summary>
    public static class CheeseSpotLogic
    {
        public const ulong NO_EARNER = ulong.MaxValue;
        public const int MIN_GRADE = 1;
        public const int MAX_GRADE = 7;

        public static int IncomePerSecond(int _grade)
        {
            return 0; // 스텁
        }

        /// <summary>현 선점자가 반경 내에 남아 있으면 유지, 아니면 목록의 첫 후보.</summary>
        public static ulong SelectEarner(ulong _currentEarner, IReadOnlyList<ulong> _present)
        {
            return NO_EARNER; // 스텁
        }

        /// <summary>소수 누적 수입 → 정수 치즈 지급. 반환: 새 누적 잔여, out: 지급할 정수량.</summary>
        public static float Accumulate(float _accumulated, int _grade, float _deltaSeconds, out int _wholeCheese)
        {
            _wholeCheese = 0; // 스텁
            return 0f;
        }
    }
}
```

- [ ] **Step 2: RED 확인**

Run: focus-unity → `u_console` 에러 0 → `Tools/CTC/Run EditMode Tests`
Expected: `Failed: 9` (Empty_ReturnsNoEarner만 스텁이 우연히 통과)

- [ ] **Step 3: 구현**

```csharp
        public static int IncomePerSecond(int _grade)
        {
            return Mathf.Clamp(_grade, MIN_GRADE, MAX_GRADE);
        }

        public static ulong SelectEarner(ulong _currentEarner, IReadOnlyList<ulong> _present)
        {
            if (_present == null || _present.Count == 0) return NO_EARNER;

            if (_currentEarner != NO_EARNER)
            {
                for (int i = 0; i < _present.Count; i++)
                {
                    if (_present[i] == _currentEarner) return _currentEarner;
                }
            }
            return _present[0];
        }

        public static float Accumulate(float _accumulated, int _grade, float _deltaSeconds, out int _wholeCheese)
        {
            float total = _accumulated + IncomePerSecond(_grade) * _deltaSeconds;
            _wholeCheese = (int)total;
            return total - _wholeCheese;
        }
```

- [ ] **Step 4: GREEN 확인** — Expected: `Passed: 42, Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Economy CTC/Assets/Scripts/Tests/EditMode/CheeseSpotLogicTests.cs*
git commit -m "feat: add cheese spot income rules (TDD)"
```

---

### Task 2: CheeseWallet + QA 프로브 노출

**Files:**
- Create: `Assets/Scripts/Economy/CheeseWallet.cs`
- Modify: `Assets/Scripts/Network/MultiplayerQaProbe.cs`

- [ ] **Step 1: CheeseWallet 작성**

```csharp
using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 개인 치즈 지갑. 서버만 기록(수입·차감 판정 권위), 전원 읽기(자기 HUD·관전).
    /// 쥐·고양이 프리팹 양쪽에 붙는다 — 원작처럼 술래(고양이)도 수입 가능.
    /// </summary>
    public class CheeseWallet : NetworkBehaviour
    {
        private readonly NetworkVariable<int> m_Cheese = new NetworkVariable<int>();

        public int Cheese => m_Cheese.Value;

        /// <summary>서버 전용. 상점 차감은 Plan 6에서 TrySpend로 확장.</summary>
        public void AddCheese(int _amount)
        {
            if (!IsServer || _amount <= 0) return;
            m_Cheese.Value += _amount;
        }
    }
}
```

- [ ] **Step 2: 프로브에 잔액 노출**

`MultiplayerQaProbe.cs` — using에 `using CTC.Runtime.Economy;` 추가, `ProbeObject`에 필드 추가:

```csharp
        public int cheese;
```

`BuildSnapshot()`의 `s.objects.Add(new ProbeObject { ... })` 직전에 지갑 조회를 넣고 필드 세팅:

```csharp
                    CheeseWallet wallet = no.GetComponent<CheeseWallet>();
```

`new ProbeObject { ... }` 초기화에 추가:

```csharp
                        cheese = wallet != null ? wallet.Cheese : -1,
```

- [ ] **Step 3: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 4: Commit**

```bash
git add CTC/Assets/Scripts/Economy/CheeseWallet.cs* CTC/Assets/Scripts/Network/MultiplayerQaProbe.cs
git commit -m "feat: add networked cheese wallet and expose balance to QA probe"
```

---

### Task 3: CheeseSpot (서버 틱)

**Files:**
- Create: `Assets/Scripts/Economy/CheeseSpot.cs`

- [ ] **Step 1: 작성**

```csharp
using System.Collections.Generic;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점(원작 돈줄). 씬 오브젝트 — 서버만 반경 내 플레이어를 검사해
    /// 선점자 1명에게 초당 수입을 지급한다. 동기화는 CheeseWallet(NetworkVariable)이 담당.
    /// </summary>
    public class CheeseSpot : MonoBehaviour
    {
        private const int OVERLAP_BUFFER_SIZE = 16;

        [SerializeField] private int m_Grade = 1;
        [SerializeField] private float m_Radius = 1.5f;

        private ulong m_CurrentEarner = CheeseSpotLogic.NO_EARNER;
        private float m_Accumulated;
        private LobbyManager m_Lobby;
        private readonly List<ulong> m_Present = new List<ulong>();
        private readonly Dictionary<ulong, CheeseWallet> m_Wallets = new Dictionary<ulong, CheeseWallet>();
        private readonly Collider[] m_Overlap = new Collider[OVERLAP_BUFFER_SIZE];

        private void Start()
        {
            m_Lobby = FindFirstObjectByType<LobbyManager>();
        }

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (m_Lobby == null || !m_Lobby.IsSpawned || !m_Lobby.GameStarted) return;

            CollectPresent();
            m_CurrentEarner = CheeseSpotLogic.SelectEarner(m_CurrentEarner, m_Present);
            if (m_CurrentEarner == CheeseSpotLogic.NO_EARNER)
            {
                m_Accumulated = 0f;
                return;
            }

            m_Accumulated = CheeseSpotLogic.Accumulate(m_Accumulated, m_Grade, Time.deltaTime, out int whole);
            if (whole > 0 && m_Wallets.TryGetValue(m_CurrentEarner, out CheeseWallet wallet) && wallet != null)
            {
                wallet.AddCheese(whole);
            }
        }

        // CharacterController도 Collider라 OverlapSphere에 잡힌다.
        private void CollectPresent()
        {
            m_Present.Clear();
            m_Wallets.Clear();

            int count = Physics.OverlapSphereNonAlloc(transform.position, m_Radius, m_Overlap);
            for (int i = 0; i < count; i++)
            {
                CheeseWallet wallet = m_Overlap[i].GetComponentInParent<CheeseWallet>();
                if (wallet == null || !wallet.IsSpawned) continue;

                ulong owner = wallet.OwnerClientId;
                if (m_Wallets.ContainsKey(owner)) continue;

                m_Present.Add(owner);
                m_Wallets.Add(owner, wallet);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_Radius);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 3: Commit**

```bash
git add CTC/Assets/Scripts/Economy/CheeseSpot.cs*
git commit -m "feat: add server-authoritative cheese spot with single-earner income"
```

---

### Task 4: WalletHud

**Files:**
- Create: `Assets/Scripts/Economy/WalletHud.cs`

- [ ] **Step 1: 작성**

```csharp
using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>자기 지갑 잔액 HUD. 게임 시작 후에만 표시(로비 중 숨김).</summary>
    public class WalletHud : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private CheeseWallet m_LocalWallet;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show)
            {
                m_LocalWallet = null;
                return;
            }

            if (m_LocalWallet == null)
            {
                NetworkManager nm = NetworkManager.Singleton;
                NetworkObject local = nm != null && nm.SpawnManager != null
                    ? nm.SpawnManager.GetLocalPlayerObject()
                    : null;
                m_LocalWallet = local != null ? local.GetComponent<CheeseWallet>() : null;
            }

            m_Text.text = m_LocalWallet != null ? $"Cheese: {m_LocalWallet.Cheese}" : "Cheese: -";
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 3: Commit**

```bash
git add CTC/Assets/Scripts/Economy/WalletHud.cs*
git commit -m "feat: add cheese balance HUD"
```

---

### Task 5: SetupNetworkingScene — 지갑·지점·HUD 배선 + 씬 재생성

**Files:**
- Modify: `Assets/Scripts/Editor/SetupNetworkingScene.cs`

먼저 파일 전체를 읽고 현 구조(BuildGreyboxMap·BuildLobbyCanvas·SetFloat/SetRef 헬퍼)를 파악한 뒤 아래를 통합해라.

- [ ] **Step 1: 프리팹에 CheeseWallet 추가**

`CreatePlayerPrefab(...)`에서 `PlayerCameraBinder` 추가 직후:

```csharp
                root.AddComponent<CheeseWallet>();
```

(using에 `CTC.Runtime.Economy` 추가)

- [ ] **Step 2: SetInt 헬퍼 추가** (기존 SetFloat 옆)

```csharp
        private static void SetInt(Component c, string field, int value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
```

- [ ] **Step 3: 치즈 지점 배치 메서드 추가 + CreateGameScene에서 호출**

`CreateGameScene`의 `BuildGreyboxMap()` 호출 뒤에 `BuildCheeseSpots();` 추가하고 메서드 구현:

```csharp
        // 치즈 지점(원작 돈줄) — 구석=저등급, 개활지=고등급. G1 (3,1)은 QA용
        // (클론 넛지 TestMpNudgeLocalPlayer가 스폰(1,0,0)에서 도달하는 지점).
        private static readonly (Vector3 pos, int grade)[] CHEESE_SPOT_DEFS =
        {
            (new Vector3(-11f, 0f, -11f), 1),
            (new Vector3(3f, 0f, 1f), 1),
            (new Vector3(11f, 0f, -12f), 2),
            (new Vector3(-9f, 0f, 9f), 2),
            (new Vector3(11.5f, 0f, 8f), 3),
            (new Vector3(0f, 0f, -6f), 3),
        };

        private static void BuildCheeseSpots()
        {
            var yellow = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/URP/Prototype_512x512_Yellow.mat");
            var root = new GameObject("CheeseSpots");

            foreach ((Vector3 pos, int grade) in CHEESE_SPOT_DEFS)
            {
                var spotGo = new GameObject($"CheeseSpot_G{grade}");
                spotGo.transform.SetParent(root.transform, false);
                spotGo.transform.localPosition = pos;

                var spot = spotGo.AddComponent<CheeseSpot>();
                SetInt(spot, "m_Grade", grade);
                SetFloat(spot, "m_Radius", grade == 1 && pos.x == 3f ? 2f : 1.5f); // QA 지점만 넉넉히

                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Disc";
                Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>()); // 이동 방해·오버랩 오염 방지
                disc.transform.SetParent(spotGo.transform, false);
                disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                disc.transform.localScale = new Vector3(3f, 0.05f, 3f);
                if (yellow != null) disc.GetComponent<Renderer>().sharedMaterial = yellow;

                var labelGo = new GameObject("GradeLabel");
                labelGo.transform.SetParent(spotGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                var tmp = labelGo.AddComponent<TMPro.TextMeshPro>();
                tmp.text = grade.ToString();
                tmp.fontSize = 6f;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = Color.yellow;
            }
        }
```

- [ ] **Step 4: HUD 배선**

로비 캔버스를 만드는 코드(BuildLobbyCanvas 류) 마지막에, 캔버스 GO와 LobbyManager 참조가 있는 지점에서:

```csharp
            // 치즈 잔액 HUD (우상단, 게임 시작 후 WalletHud가 표시 제어)
            var hudTextGo = new GameObject("WalletText");
            hudTextGo.transform.SetParent(canvasGo.transform, false);
            var hudText = hudTextGo.AddComponent<TMPro.TextMeshProUGUI>();
            hudText.text = "Cheese: 0";
            hudText.fontSize = 32f;
            hudText.alignment = TMPro.TextAlignmentOptions.TopRight;
            var hudRect = hudTextGo.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(1f, 1f);
            hudRect.anchorMax = new Vector2(1f, 1f);
            hudRect.pivot = new Vector2(1f, 1f);
            hudRect.anchoredPosition = new Vector2(-24f, -24f);
            hudRect.sizeDelta = new Vector2(320f, 48f);
            hudTextGo.SetActive(false); // WalletHud가 게임 시작 시 켬

            var walletHud = canvasGo.AddComponent<WalletHud>();
            SetRef(walletHud, "m_Lobby", lobby);
            SetRef(walletHud, "m_Text", hudText);
```

(`canvasGo`·`lobby`는 실제 변수명에 맞춰라. LobbyManager 참조가 그 스코프에 없으면 시그니처에 전달 인자 추가)

- [ ] **Step 5: 컴파일 → 씬 재생성**

focus-unity → `u_console` 에러 0 → `u_editor_menu`로 `Tools/CTC/Setup Networking Scene`
Expected: `[SetupNetworkingScene] 완료` + 에러 0. 씬에 `CheeseSpots/CheeseSpot_G1..G3` 6개.

- [ ] **Step 6: EditMode 회귀** — Expected: `Passed: 42, Failed: 0`

- [ ] **Step 7: Commit**

```bash
git add CTC/Assets/Scripts/Editor/SetupNetworkingScene.cs CTC/Assets/Prefabs CTC/Assets/Scenes CTC/Assets/DefaultNetworkPrefabs.asset
git commit -m "feat: place graded cheese spots and wire wallet HUD into scene"
```

---

### Task 6: MPPM 2인 검증 (메인 세션 직접)

- [ ] **Step 1: 플레이 → 2인 접속 → 역할 → 시작**

`u_play` enter → 클론 probe 대기 → `u_play invoke NetworkManager.StartHost`(True 확인 — False면 포트 누수, 에디터 재시작) → 호스트 `LobbyManager.DevSelectCat` · 클론 `cmd_clone.json` `{"seq":N,"method":"TestMpSelectRoleMouse"}` → `LobbyManager.DevStartGame`

- [ ] **Step 2: 수입 검증**

클론 넛지 `{"seq":N+1,"method":"TestMpNudgeLocalPlayer"}` → 쥐가 QA 지점(3,0,1) 반경 도달 → 5초 대기 → `mppm_collect`
Expected: PlayerMouse의 `cheese`가 **0 초과이고 시간에 따라 증가** (G1 = 초당 1). PlayerCat `cheese: 0` (지점 밖). 양측 스냅샷 일치.

- [ ] **Step 3: HUD·비주얼 확인**

`u_screenshot` — 우상단 `Cheese: N` 표시, 노란 지점 디스크·등급 라벨 렌더 확인.

- [ ] **Step 4: 문서 갱신 + 커밋**

로드맵 Plan 4 행 완료 표기, 핸드오프 갱신.

```bash
git add CTC/docs
git commit -m "docs: mark Plan 4 cheese economy complete"
```

---

## 검증 요약 (완료 기준)

| # | 확인 | 기대 |
|---|---|---|
| 1 | EditMode | `Passed: 42, Failed: 0` |
| 2 | 지점 수입 | 지점 안 쥐만 초당 +등급, 지점 밖 0 |
| 3 | 지점당 1마리 | (로직 테스트로 커버 — SelectEarner 유지/승계) |
| 4 | 동기화 | host/clone 양측 `cheese` 값 일치 |
| 5 | HUD | 게임 시작 후 우상단 잔액, 로비 중 숨김 |
