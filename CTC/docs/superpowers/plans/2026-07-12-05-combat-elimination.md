# Plan 5: 전투 & 탈락 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 고양이 덮치기(부채꼴 근접, 쿨다운)로 쥐를 처치→탈락·관전 전환, 쥐 기본 무기(딱총 히트스캔)로 고양이 HP를 깎음, 처치 보상은 지갑으로.

**Architecture:** 판정 수학(데미지·쿨다운·부채꼴)은 순수 `CombatLogic`으로 TDD. HP는 `Health`(NetworkVariable) 하나로 쥐/고양이 공용 — 서버만 기록. 공격 입력은 소유자→ServerRpc→서버 판정(호스트 권위). 사망 시 서버가 despawn+보상, 클라이언트는 `SpectatorFallback`이 카메라를 생존자에게 재바인딩. 역할 식별은 `RoleTag`(직렬화) — 컴포넌트 유무 추론 금지.

**Tech Stack:** NGO 2.12(NetworkVariable·Rpc·서버 Raycast/Overlap), Input System(`Mouse.current`), 기존 Economy(CheeseWallet 보상 재사용)·Cameras(OrbitCameraController)·씬 재생성 파이프라인.

**스펙 근거:** [디자인 스펙](../specs/2026-07-06-cheese-thief-and-cat-design.md) §6(쥐 저HP·고양이 보스 HP·덮치기·처치 보상) · 로드맵 Plan 5

## 설계 결정 (원작 약올리기 대응 — 수치는 전부 임시 밸런스, 직렬화 필드로 튜닝)

| 항목 | 값 | 근거 |
|---|---|---|
| 쥐 HP | 100, 덮치기 데미지 60 (2방 처치) | 원작 시민 저HP — 1방 즉사는 HP 게이지·요새 회복(Plan 6)을 무의미화 |
| 덮치기 | 사거리 2.2m·전방 부채꼴 반각 60°·쿨다운 2.5s | 스펙 "근접, 쿨다운" |
| 고양이 HP | 1000 | 원작 질럿 1000 (실드는 생략) |
| 딱총(쥐 기본 무기) | 히트스캔 사거리 8m·데미지 5·쿨다운 0.8s | 원작 BB탄 딱총 티어 — 상점 도입 전 기본 지급 |
| 처치 보상 | 쥐 처치 시 고양이 지갑 +50 | 원작 "시민 죽여 수입" |
| 쥐 사망 | 오브젝트 despawn → 탈락(부활 없음) → 관전(카메라가 생존자 추적) | 원작 동일 |
| 고양이 HP 0 | 로그만 (승패 판정은 Plan 7) | 스코프 분리 |
| 공격 입력 | 마우스 좌클릭 (커서 잠금 중에만) | 카메라 조준과 일치 |

---

## 실행 환경 주의 (모든 태스크 공통)

- 컴파일: CTC에서 `powershell -NoProfile -File tools/focus-unity.ps1` → MCP `u_console` CS 에러 0. `[McpBridge] Pipe is broken`은 노이즈.
- EditMode: `u_editor_menu` → `Tools/CTC/Run EditMode Tests` → `C:/Users/user/AppData/LocalLow/DefaultCompany/CTC/TestResults.xml`. **현재 Passed 42 / Failed 0.**
- 컴파일 에러 시 테스트 실행 불가 → TDD는 "테스트+틀린 값 스텁" 동시 작성으로 RED.
- 신규 코드: `m_` 멤버·`_` 매개변수·public 필드 금지·UPPER_SNAKE_CASE 상수·레거시 `Input.*` 금지·CC transform 직접 세팅 금지(회전은 허용, 위치는 CC.enabled 토글 후에만).
- 커밋: 저장소 루트에서 `CTC/` 접두·.meta 포함·끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`. `ProjectSettings/*`·`Assets/Settings/*`·`.claude/*`·`screenshot.png` 금지.

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Combat/CombatLogic.cs` (신규) | 순수: 데미지 적용·사망 판정·쿨다운·부채꼴 판정 |
| `Assets/Scripts/Combat/Health.cs` (신규) | HP NetworkVariable + 서버 Died 이벤트 |
| `Assets/Scripts/Combat/RoleTag.cs` (신규) | 프리팹에 역할(RoleId) 직렬화 |
| `Assets/Scripts/Combat/CatPounce.cs` (신규) | 고양이 덮치기: 입력→ServerRpc→부채꼴 판정·데미지·보상 |
| `Assets/Scripts/Combat/MousePopgun.cs` (신규) | 쥐 딱총: 입력→ServerRpc→Raycast→고양이 데미지 |
| `Assets/Scripts/Combat/PlayerDeath.cs` (신규) | 서버: 사망 → despawn |
| `Assets/Scripts/Combat/CombatHud.cs` (신규) | 자기 HP + 고양이 HP 텍스트 |
| `Assets/Scripts/Cameras/SpectatorFallback.cs` (신규) | 로컬 플레이어 소멸 시 생존자에게 카메라 재바인딩 |
| `Assets/Scripts/Tests/EditMode/CombatLogicTests.cs` (신규) | TDD |
| `Assets/Scripts/Testing/TestManager.cs` (수정) | QA: 순간이동·덮치기·사격 시나리오 |
| `Assets/Scripts/Network/MultiplayerQaProbe.cs` (수정) | hp 노출 + 클론 사격 커맨드 |
| `Assets/Scripts/Editor/SetupNetworkingScene.cs` (수정) | 프리팹·씬 배선 |

---

### Task 1: CombatLogic (TDD)

**Files:**
- Create: `Assets/Scripts/Combat/CombatLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/CombatLogicTests.cs`

- [ ] **Step 1: 테스트 + 스텁 작성**

`Assets/Scripts/Tests/EditMode/CombatLogicTests.cs`:

```csharp
using CTC.Runtime.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class CombatLogicTests
    {
        [Test]
        public void ApplyDamage_Reduces()
        {
            Assert.AreEqual(40, CombatLogic.ApplyDamage(100, 60));
        }

        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            Assert.AreEqual(0, CombatLogic.ApplyDamage(30, 60));
        }

        [Test]
        public void ApplyDamage_NegativeDamage_Ignored()
        {
            Assert.AreEqual(100, CombatLogic.ApplyDamage(100, -5));
        }

        [Test]
        public void IsDead_ZeroHp_True()
        {
            Assert.IsTrue(CombatLogic.IsDead(0));
            Assert.IsFalse(CombatLogic.IsDead(1));
        }

        [Test]
        public void Cooldown_NotElapsed_False()
        {
            Assert.IsFalse(CombatLogic.IsCooldownReady(10.0, 11.0, 2.5f));
        }

        [Test]
        public void Cooldown_Elapsed_True()
        {
            Assert.IsTrue(CombatLogic.IsCooldownReady(10.0, 12.5, 2.5f));
        }

        [Test]
        public void PounceArc_InRangeInAngle_True()
        {
            Assert.IsTrue(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0.5f, 0f, 1.5f), 2.2f, 60f));
        }

        [Test]
        public void PounceArc_OutOfRange_False()
        {
            Assert.IsFalse(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 3f), 2.2f, 60f));
        }

        [Test]
        public void PounceArc_BehindAttacker_False()
        {
            Assert.IsFalse(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), 2.2f, 60f));
        }

        [Test]
        public void PounceArc_IgnoresHeightDifference()
        {
            // y 차이는 무시(평면 판정) — 점프/단차에서 판정 누락 방지
            Assert.IsTrue(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0.8f, 1f), 2.2f, 60f));
        }
    }
}
```

`Assets/Scripts/Combat/CombatLogic.cs` (스텁):

```csharp
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 전투 판정 수학. MonoBehaviour와 분리된 순수 함수 — EditMode TDD.
    /// 서버(호스트)만 이 판정으로 실제 데미지를 적용한다.
    /// </summary>
    public static class CombatLogic
    {
        public static int ApplyDamage(int _currentHp, int _damage)
        {
            return _currentHp; // 스텁
        }

        public static bool IsDead(int _hp)
        {
            return false; // 스텁
        }

        public static bool IsCooldownReady(double _lastUseTime, double _now, float _cooldownSeconds)
        {
            return false; // 스텁
        }

        /// <summary>전방 부채꼴(평면) 판정 — y 차이는 무시한다.</summary>
        public static bool IsInPounceArc(Vector3 _attackerPos, Vector3 _attackerForward, Vector3 _targetPos, float _range, float _halfAngleDeg)
        {
            return false; // 스텁
        }
    }
}
```

- [ ] **Step 2: RED 확인**

Run: focus-unity → 에러 0 → EditMode 실행
Expected: `Failed: 6` (NegativeDamage·NotElapsed·OutOfRange·Behind 4개는 스텁이 우연히 통과)

- [ ] **Step 3: 구현**

```csharp
        public static int ApplyDamage(int _currentHp, int _damage)
        {
            return Mathf.Max(0, _currentHp - Mathf.Max(0, _damage));
        }

        public static bool IsDead(int _hp)
        {
            return _hp <= 0;
        }

        public static bool IsCooldownReady(double _lastUseTime, double _now, float _cooldownSeconds)
        {
            return _now - _lastUseTime >= _cooldownSeconds;
        }

        public static bool IsInPounceArc(Vector3 _attackerPos, Vector3 _attackerForward, Vector3 _targetPos, float _range, float _halfAngleDeg)
        {
            Vector3 to = _targetPos - _attackerPos;
            to.y = 0f;
            if (to.sqrMagnitude > _range * _range) return false;
            if (to.sqrMagnitude < 1e-6f) return true;

            Vector3 forward = _attackerForward;
            forward.y = 0f;
            return Vector3.Angle(forward, to) <= _halfAngleDeg;
        }
```

- [ ] **Step 4: GREEN 확인** — Expected: `Passed: 52, Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Combat CTC/Assets/Scripts/Tests/EditMode/CombatLogicTests.cs*
git commit -m "feat: add combat math (damage, cooldown, pounce arc) (TDD)"
```

---

### Task 2: Health + RoleTag + 프로브 hp 노출

**Files:**
- Create: `Assets/Scripts/Combat/Health.cs`, `Assets/Scripts/Combat/RoleTag.cs`
- Modify: `Assets/Scripts/Network/MultiplayerQaProbe.cs`

- [ ] **Step 1: Health 작성**

```csharp
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>HP. 서버만 기록, 전원 읽기(HUD). 사망 이벤트는 서버에서만 발화.</summary>
    public class Health : NetworkBehaviour
    {
        [SerializeField] private int m_MaxHp = 100;

        private readonly NetworkVariable<int> m_Hp = new NetworkVariable<int>();

        public int Hp => m_Hp.Value;
        public int MaxHp => m_MaxHp;
        public bool IsDead => CombatLogic.IsDead(m_Hp.Value);

        /// <summary>서버 전용 사망 알림 (1회).</summary>
        public event System.Action<Health> Died;

        public override void OnNetworkSpawn()
        {
            if (IsServer) m_Hp.Value = m_MaxHp;
        }

        public void TakeDamage(int _damage)
        {
            if (!IsServer || IsDead) return;

            m_Hp.Value = CombatLogic.ApplyDamage(m_Hp.Value, _damage);
            if (IsDead) Died?.Invoke(this);
        }
    }
}
```

- [ ] **Step 2: RoleTag 작성**

```csharp
using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>프리팹에 박아두는 역할 식별 — 컴포넌트 유무 추론 대신 명시 태그.</summary>
    public class RoleTag : MonoBehaviour
    {
        [SerializeField] private RoleId m_Role = RoleId.None;

        public RoleId Role => m_Role;
    }
}
```

- [ ] **Step 3: 프로브에 hp 노출**

`MultiplayerQaProbe.cs` — using에 `using CTC.Runtime.Combat;` 추가. `ProbeObject`에:

```csharp
        public int hp;
```

`BuildSnapshot()`의 wallet 조회 옆에:

```csharp
                    Health health = no.GetComponent<Health>();
```

`new ProbeObject { ... }`에:

```csharp
                        hp = health != null ? health.Hp : -1,
```

- [ ] **Step 4: 컴파일 확인** — focus-unity → 에러 0

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Combat/Health.cs* CTC/Assets/Scripts/Combat/RoleTag.cs* CTC/Assets/Scripts/Network/MultiplayerQaProbe.cs
git commit -m "feat: add networked health, role tag, and QA probe hp"
```

---

### Task 3: CatPounce + MousePopgun

**Files:**
- Create: `Assets/Scripts/Combat/CatPounce.cs`, `Assets/Scripts/Combat/MousePopgun.cs`

- [ ] **Step 1: CatPounce 작성**

```csharp
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 고양이 덮치기. 소유자 좌클릭 → ServerRpc → 서버가 전방 부채꼴 내
    /// 살아있는 쥐 전원에게 데미지. 처치 시 고양이 지갑에 보상.
    /// </summary>
    public class CatPounce : NetworkBehaviour
    {
        [SerializeField] private float m_Range = 2.2f;
        [SerializeField] private float m_HalfAngleDeg = 60f;
        [SerializeField] private int m_Damage = 60;
        [SerializeField] private float m_CooldownSeconds = 2.5f;
        [SerializeField] private int m_KillReward = 50;

        private double m_LastUseTime = -999.0;

        private void Update()
        {
            if (!IsOwner || Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            PounceRpc();
        }

        /// <summary>QA 자동화 진입점 (TestManager/wesqa).</summary>
        public void DevPounce() => PounceRpc();

        [Rpc(SendTo.Server)]
        private void PounceRpc()
        {
            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, m_CooldownSeconds)) return;
            m_LastUseTime = now;

            CheeseWallet myWallet = GetComponent<CheeseWallet>();

            foreach (Health target in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (target.IsDead) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                if (!CombatLogic.IsInPounceArc(
                        transform.position, transform.forward, target.transform.position,
                        m_Range, m_HalfAngleDeg)) continue;

                target.TakeDamage(m_Damage);
                if (target.IsDead && myWallet != null)
                {
                    myWallet.AddCheese(m_KillReward);
                }
            }
        }
    }
}
```

- [ ] **Step 2: MousePopgun 작성**

```csharp
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 쥐 기본 무기 딱총(원작 BB탄 딱총 티어). 소유자 좌클릭 → 카메라 전방으로
    /// ServerRpc → 서버 Raycast → 고양이만 데미지. 상점 티어 확장은 Plan 6.
    /// </summary>
    public class MousePopgun : NetworkBehaviour
    {
        private const float MUZZLE_HEIGHT = 0.3f;

        [SerializeField] private float m_Range = 8f;
        [SerializeField] private int m_Damage = 5;
        [SerializeField] private float m_CooldownSeconds = 0.8f;

        private double m_LastUseTime = -999.0;

        private void Update()
        {
            if (!IsOwner || Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Vector3 dir = Camera.main != null ? Camera.main.transform.forward : transform.forward;
            dir.y = 0f;
            FireRpc(dir.normalized);
        }

        /// <summary>QA: 가장 가까운 고양이를 향해 발사.</summary>
        public void DevShootAtNearestCat()
        {
            Health nearest = null;
            float best = float.MaxValue;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                RoleTag tag = h.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat || h.IsDead) continue;

                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = h; }
            }
            if (nearest == null) return;

            Vector3 dir = nearest.transform.position - transform.position;
            dir.y = 0f;
            FireRpc(dir.normalized);
        }

        [Rpc(SendTo.Server)]
        private void FireRpc(Vector3 _direction)
        {
            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, m_CooldownSeconds)) return;
            m_LastUseTime = now;

            Vector3 origin = transform.position + Vector3.up * MUZZLE_HEIGHT;
            // 자기 CC에 막히지 않도록 반경 밖에서 시작
            origin += _direction * 0.4f;

            if (!Physics.Raycast(origin, _direction, out RaycastHit hit, m_Range)) return;

            Health target = hit.collider.GetComponentInParent<Health>();
            if (target == null || target.IsDead) return;

            RoleTag tag = target.GetComponent<RoleTag>();
            if (tag == null || tag.Role != RoleId.Cat) return;

            target.TakeDamage(m_Damage);
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인** — focus-unity → 에러 0

- [ ] **Step 4: Commit**

```bash
git add CTC/Assets/Scripts/Combat/CatPounce.cs* CTC/Assets/Scripts/Combat/MousePopgun.cs*
git commit -m "feat: add cat pounce and mouse popgun with server-side hit judgment"
```

---

### Task 4: PlayerDeath + SpectatorFallback

**Files:**
- Create: `Assets/Scripts/Combat/PlayerDeath.cs`
- Create: `Assets/Scripts/Cameras/SpectatorFallback.cs`

- [ ] **Step 1: PlayerDeath 작성**

```csharp
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 서버: 사망 처리. 쥐 = despawn(탈락·부활 없음, 원작 동일).
    /// 고양이 HP 0 = 로그만 (승패 판정은 Plan 7 매치 플로우).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : NetworkBehaviour
    {
        private Health m_Health;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            m_Health = GetComponent<Health>();
            m_Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (m_Health != null) m_Health.Died -= OnDied;
        }

        private void OnDied(Health _health)
        {
            RoleTag tag = GetComponent<RoleTag>();
            if (tag != null && tag.Role == RoleId.Cat)
            {
                Debug.Log($"[PlayerDeath] 고양이({OwnerClientId}) HP 0 — 승패 판정은 Plan 7");
                return;
            }

            Debug.Log($"[PlayerDeath] 쥐({OwnerClientId}) 탈락 — despawn");
            NetworkObject.Despawn(true);
        }
    }
}
```

- [ ] **Step 2: SpectatorFallback 작성**

`Assets/Scripts/Cameras/SpectatorFallback.cs`:

```csharp
using CTC.Runtime.Lobby;
using CTC.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 관전 폴백: 게임 중 로컬 플레이어가 사라지면(탈락) 카메라를
    /// 아무 생존 플레이어에게 재바인딩한다. (MECCHA식 자유 관전의 MVP)
    /// </summary>
    [RequireComponent(typeof(OrbitCameraController))]
    public class SpectatorFallback : MonoBehaviour
    {
        private const float SPECTATE_FOV = 60f;
        private const float SPECTATE_DISTANCE = 5f;

        [SerializeField] private LobbyManager m_Lobby;

        private OrbitCameraController m_Orbit;
        private Transform m_Current;

        private void Awake()
        {
            m_Orbit = GetComponent<OrbitCameraController>();
        }

        private void Update()
        {
            if (m_Lobby == null || !m_Lobby.IsSpawned || !m_Lobby.GameStarted) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;
            if (nm.SpawnManager.GetLocalPlayerObject() != null) return; // 아직 살아 있음

            if (m_Current != null) return; // 이미 관전 대상 추적 중

            PlayerMovement any = FindFirstObjectByType<PlayerMovement>();
            if (any == null) return;

            m_Current = any.transform;
            m_Orbit.Bind(m_Current, SPECTATE_FOV, SPECTATE_DISTANCE);
            Debug.Log($"[SpectatorFallback] 관전 전환 → {any.name}");
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인** — focus-unity → 에러 0

- [ ] **Step 4: Commit**

```bash
git add CTC/Assets/Scripts/Combat/PlayerDeath.cs* CTC/Assets/Scripts/Cameras/SpectatorFallback.cs*
git commit -m "feat: eliminate dead mice and fall back to spectator camera"
```

---

### Task 5: CombatHud + QA 훅 (TestManager·프로브)

**Files:**
- Create: `Assets/Scripts/Combat/CombatHud.cs`
- Modify: `Assets/Scripts/Testing/TestManager.cs`
- Modify: `Assets/Scripts/Network/MultiplayerQaProbe.cs`

- [ ] **Step 1: CombatHud 작성**

```csharp
using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>좌상단 아래쪽: 자기 HP + 고양이 HP(공동 목표 게이지).</summary>
    public class CombatHud : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            NetworkManager nm = NetworkManager.Singleton;
            NetworkObject local = nm != null && nm.SpawnManager != null
                ? nm.SpawnManager.GetLocalPlayerObject()
                : null;
            Health myHealth = local != null ? local.GetComponent<Health>() : null;

            Health catHealth = null;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                RoleTag tag = h.GetComponent<RoleTag>();
                if (tag != null && tag.Role == RoleId.Cat) { catHealth = h; break; }
            }

            string mine = myHealth != null ? $"HP {myHealth.Hp}/{myHealth.MaxHp}" : "SPECTATING";
            string cat = catHealth != null ? $"CAT {catHealth.Hp}/{catHealth.MaxHp}" : "CAT -";
            m_Text.text = $"{mine}   {cat}";
        }
    }
}
```

- [ ] **Step 2: TestManager에 QA 시나리오 추가**

`Assets/Scripts/Testing/TestManager.cs` — 기존 Test* 메서드들 옆에 추가 (파일의 기존 using·패턴 확인 후 통합. `CTC.Runtime.Combat` using 추가):

```csharp
        /// <summary>로컬 플레이어를 다른 플레이어 바로 앞으로 순간이동(전투 QA용, CC 토글).</summary>
        public void TestTeleportToOtherPlayer()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) { GameDebug.LogWarning("[TestManager] 로컬 플레이어 없음"); return; }

            foreach (var kv in nm.SpawnManager.SpawnedObjects)
            {
                var other = kv.Value;
                if (other == null || other == local || other.GetComponent<CTC.Runtime.Player.PlayerMovement>() == null) continue;

                var cc = local.GetComponent<UnityEngine.CharacterController>();
                UnityEngine.Vector3 pos = other.transform.position - other.transform.forward * 1.2f;
                if (cc != null) cc.enabled = false;
                local.transform.position = pos;
                local.transform.LookAt(other.transform.position);
                if (cc != null) cc.enabled = true;
                GameDebug.Log($"[TestManager] TeleportToOtherPlayer → {pos}");
                return;
            }
        }

        /// <summary>로컬 플레이어(고양이)의 덮치기 실행.</summary>
        public void TestPounce()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var pounce = local != null ? local.GetComponent<CatPounce>() : null;
            if (pounce == null) { GameDebug.LogWarning("[TestManager] CatPounce 없음"); return; }
            pounce.DevPounce();
        }

        /// <summary>로컬 플레이어(쥐)가 가장 가까운 고양이에게 딱총 발사.</summary>
        public void TestShootAtCat()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var gun = local != null ? local.GetComponent<MousePopgun>() : null;
            if (gun == null) { GameDebug.LogWarning("[TestManager] MousePopgun 없음"); return; }
            gun.DevShootAtNearestCat();
        }
```

- [ ] **Step 3: 프로브 클론 커맨드 추가**

`MultiplayerQaProbe.cs`의 `ExecuteCloneCommand` switch에:

```csharp
                case "TestMpShootAtCat":
                    ShootAtCat();
                    break;
```

메서드 추가 (`SelectRole` 아래):

```csharp
        // 클론 쥐가 가장 가까운 고양이에게 딱총 발사 — 전투 QA용.
        private void ShootAtCat()
        {
            NetworkObject local = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject()
                : null;
            var gun = local != null ? local.GetComponent<Combat.MousePopgun>() : null;
            if (gun == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] ShootAtCat — MousePopgun 없음");
                return;
            }
            gun.DevShootAtNearestCat();
        }
```

(파일 네임스페이스가 `CTC.Runtime.Networking`이라 `Combat.MousePopgun`으로 형제 네임스페이스 참조 — using이 이미 있으면 짧게)

- [ ] **Step 4: 컴파일 확인** — focus-unity → 에러 0

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Combat/CombatHud.cs* CTC/Assets/Scripts/Testing/TestManager.cs CTC/Assets/Scripts/Network/MultiplayerQaProbe.cs
git commit -m "feat: add combat HUD and QA hooks for pounce/shoot scenarios"
```

---

### Task 6: SetupNetworkingScene 배선 + 씬 재생성

**Files:**
- Modify: `Assets/Scripts/Editor/SetupNetworkingScene.cs`

먼저 파일을 읽고 현 구조에 통합해라. using에 `CTC.Runtime.Combat` 추가.

- [ ] **Step 1: 프리팹 배선**

`CreatePlayerPrefab(...)` 시그니처에 역할·전투 파라미터 추가가 필요하다. 현재 호출 2곳을 다음 값으로:

- PlayerCat: `role=RoleId.Cat, maxHp=1000`, `CatPounce` 부착 (직렬화 기본값 사용 — 세터 불필요)
- PlayerMouse: `role=RoleId.Mouse, maxHp=100`, `MousePopgun` 부착

`CreatePlayerPrefab` 본문의 `CheeseWallet` 추가 직후에:

```csharp
                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", maxHp);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)role); // enum은 intValue로

                root.AddComponent<PlayerDeath>();

                if (role == RoleId.Cat) root.AddComponent<CatPounce>();
                else root.AddComponent<MousePopgun>();
```

(`SetInt`는 기존 헬퍼. enum 직렬화 필드는 `so.FindProperty(...).intValue = (int)role`이 동작하지만, `SerializedProperty.enumValueIndex`가 필요한 경우 실제 동작 확인 후 조정 — RoleId는 None=0/Mouse=1/Cat=2 연속이라 intValue로 충분)

- [ ] **Step 2: 카메라·HUD 배선**

- OrbitCamera GO 생성부에 `SpectatorFallback` 추가 + `SetRef(spectator, "m_Lobby", lobby)` (lobby 참조가 그 스코프에 없으면 인자로 전달)
- 로비 캔버스의 WalletText 배선 아래에 CombatText 추가:

```csharp
            var combatTextGo = new GameObject("CombatText");
            combatTextGo.transform.SetParent(canvasGo.transform, false);
            var combatText = combatTextGo.AddComponent<TextMeshProUGUI>();
            combatText.text = "HP -   CAT -";
            combatText.fontSize = 32f;
            combatText.alignment = TextAlignmentOptions.TopLeft;
            var combatRect = combatTextGo.GetComponent<RectTransform>();
            combatRect.anchorMin = new Vector2(0f, 1f);
            combatRect.anchorMax = new Vector2(0f, 1f);
            combatRect.pivot = new Vector2(0f, 1f);
            combatRect.anchoredPosition = new Vector2(24f, -110f); // ConnectionUI(IMGUI) 아래
            combatRect.sizeDelta = new Vector2(480f, 48f);
            combatTextGo.SetActive(false);

            var combatHud = canvasGo.AddComponent<CombatHud>();
            SetRef(combatHud, "m_Lobby", lobby);
            SetRef(combatHud, "m_Text", combatText);
```

- [ ] **Step 3: 컴파일 → 씬 재생성 → 회귀**

focus-unity → 에러 0 → `Tools/CTC/Setup Networking Scene` → 에러 0 → EditMode `Passed: 52, Failed: 0`

- [ ] **Step 4: Commit**

```bash
git add CTC/Assets/Scripts/Editor/SetupNetworkingScene.cs CTC/Assets/Prefabs CTC/Assets/Scenes CTC/Assets/Materials CTC/Assets/DefaultNetworkPrefabs.asset
git commit -m "feat: wire combat components into prefabs, camera, and HUD"
```

---

### Task 7: MPPM 2인 검증 (메인 세션 직접)

- [ ] **Step 1: 접속·역할·시작** — 기존 절차 (호스트 Cat / 클론 Mouse / DevStartGame)

- [ ] **Step 2: 사격 검증** — 클론 `cmd_clone.json` `{"seq":N,"method":"TestMpShootAtCat"}` 반복 3회(쿨다운 0.8s 간격) → `mppm_collect`: PlayerCat `hp`가 1000→985 (5×3), 양측 일치

- [ ] **Step 3: 덮치기·탈락 검증** — 호스트에서 wesqa(포트 5001) `Invoke("TestTeleportToOtherPlayer")` → `Invoke("TestPounce")` ×2 (쿨다운 2.5s 간격, 60×2=120>100) → `mppm_collect`: PlayerMouse 오브젝트 소멸, PlayerCat `cheese` +50, 클론 화면은 관전 전환(클론 콘솔 로그 `[SpectatorFallback]`)

- [ ] **Step 4: HUD 스크린샷** — `u_screenshot`: 좌상단 `HP … CAT …` 렌더

- [ ] **Step 5: 문서 갱신 + 커밋** — 로드맵 Plan 5 완료 표기, 핸드오프 갱신

## 검증 요약 (완료 기준)

| # | 확인 | 기대 |
|---|---|---|
| 1 | EditMode | `Passed: 52, Failed: 0` |
| 2 | 딱총 | 고양이 HP 감소(발당 5), 양측 동기화, 쥐끼리는 피해 없음(로직상 Cat 태그만) |
| 3 | 덮치기 | 부채꼴 내 쥐 60 데미지, 2방 탈락(despawn), 보상 +50 |
| 4 | 관전 | 탈락한 클론 카메라가 생존자 추적 |
| 5 | HUD | HP/CAT 표시, 게임 시작 후만 |
