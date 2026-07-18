# Plan 11: 맵 생성 시스템 (후보풀 + 시드 랜덤 + 검증 + 씬 굽기) — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 하드코딩 맵(`BuildGreyboxMap`·`FORTRESS_SPOTS`·`CHEESE_SPOT_DEFS`)을 **후보풀에서 시드로 뽑아 검증하는 에디터 생성 툴 + `MapLayout` 에셋 라이브러리**로 대체하고, 씬 굽기·런타임 스폰이 에셋을 참조하게 데이터화한다.

**Architecture:** 순수 로직(선택·검증)은 `MapGenerator`(런타임 asmdef, 정적 함수, EditMode TDD). 데이터는 SO 2종 — `MapCandidatePool`(후보 슬롯, level-design 명세를 옮김) → 생성기 → `MapLayout`(확정 결과물). 에디터 툴이 시드로 `MapLayout`을 굽고, `SetupNetworkingScene`이 선택된 `MapLayout`을 읽어 방·통로·미로·치즈·요새를 씬에 생성한다. `LobbyManager`는 `FORTRESS_SPOTS` 하드코딩을 `MapLayout` 주입으로 교체(스폰 공식 로직 자체는 유지). 사용자 시안은 생성기를 거치지 않는 hand-authored `MapLayout` 1호로 등록.

**Tech Stack:** Unity 6000.5.2f1 · NGO 2.12 · ScriptableObject · 기존 씬 재생성 파이프라인 · EditMode NUnit TDD. 신규 패키지 없음.

**근거 문서:** [맵 생성 스펙](../specs/2026-07-16-map-generation-design.md) · [맵 후보풀 명세](../specs/2026-07-16-map-candidate-pool.md) · [게임 스펙 §8](../specs/2026-07-06-cheese-thief-and-cat-design.md)

---

## ⚠️ 선행 의존성 (Plan 10)

Plan 10(v3 요새 런타임)이 `LobbyManager.FORTRESS_SPOTS`·`SpawnPosition`·`SetupNetworkingScene.CreateFortressPrefab`·v1 맵 복원을 **이미 구현·커밋**했다(HEAD `a67af63` 기준). 이 계획은 그 위에서 진행한다:
- Task 6(씬 굽기)이 Plan 10의 `BuildGreyboxMap`(v1 부엌)을 `BuildFromLayout`으로 교체
- Task 7이 Plan 10의 `FORTRESS_SPOTS` 정적 배열을 `MapLayout` 주입으로 교체
- **Plan 10이 아직 진행 중이면** Task 1~5(신규 파일, 기존 코드 무변경)만 먼저 하고 Task 6~7은 Plan 10 머지 후 착수.

## 후보풀 명세 정정 (이 계획에서 반영)

level-design 명세(§5) 좌표 중 요새 풋프린트(중심±1.2)가 방 경계를 넘는 2건 — 풀 에셋 작성(Task 4) 시 **정정값** 사용:
| slot | 명세값 | 정정값 | 이유 |
|---|---|---|---|
| F1b (R4) | (13, 4.5) | **(12.5, 4.5)** | R4 max x=13.75, 13+1.2=14.2 초과 → 12.5+1.2=13.7 ✓ |
| F4 (R9) | (0, −12.5) | **(0, −12.3)** | R9 min z=−13.5, −12.5−1.2=−13.7 초과 → −12.3−1.2=−13.5 ✓ |

---

## 실행 환경 주의 (모든 태스크 공통)

- 컴파일: `u_editor_asset(action: refresh)` → `powershell -NoProfile -File tools/focus-unity.ps1` → ~12s → `u_console` CS 에러 0
- EditMode: `Tools/CTC/Run EditMode Tests` → `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CTC\TestResults.xml`. **Plan 10 완료 기준 Passed 81.** 이 계획 완료 시 **+N** (Task별 명시)
- 신규 코드 `m_`/`_`·`UPPER_SNAKE_CASE` 컨벤션. 기존 파일은 그 파일 스타일 유지(`LobbyManager`는 무접두 `catPrefab` 스타일)
- 커밋: 저장소 루트에서 `CTC/` 접두·.meta 포함·끝에 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`. `ProjectSettings/*`·`Assets/Settings/*`·`.claude/*`·`screenshot.png` 커밋 금지
- 새 NetworkObject 프리팹 없음(요새 프리팹은 Plan 10이 생성) → `BakeGlobalObjectIdHash`·`NetworkObjectHashTests` 추가 등록 불요
- SO 애셋은 `.asset`이라 씬 재생성과 무관하게 유지됨

## 파일 구조

| 파일 | 작업 | 책임 |
|---|---|---|
| `Assets/Scripts/Map/MapData.cs` | Create | `RoomRect`·`CorridorSeg`·`CheesePlacement`·`FortressPlacement`·`MazePreset` 직렬화 구조체 |
| `Assets/Scripts/Map/MapLayout.cs` | Create | 확정 결과물 SO (seed·source·rooms·corridors·cheese·fortresses) |
| `Assets/Scripts/Map/MapCandidatePool.cs` | Create | 후보 슬롯 SO (방·통로·미로·치즈밴드·요새 후보) |
| `Assets/Scripts/Map/MapGenerator.cs` | Create | 순수 정적 생성 로직 — 방선택·통로연결·치즈/요새배정 |
| `Assets/Scripts/Map/MapValidator.cs` | Create | 순수 정적 검증 V1~V12 (bool + 실패사유) |
| `Assets/Scripts/Editor/MapGeneratorWindow.cs` | Create | `Tools/CTC/Generate Map Layout` 에디터 창 (시드·생성·저장·프리뷰) |
| `Assets/Scripts/Editor/MapCandidatePoolBuilder.cs` | Create | 후보풀 명세를 SO 에셋으로 굽는 1회성 메뉴 |
| `Assets/Scripts/Editor/SetupNetworkingScene.cs` | Modify | `BuildFromLayout(MapLayout)`로 맵 지오메트리·치즈·요새 생성 |
| `Assets/Scripts/Lobby/LobbyManager.cs` | Modify | `FORTRESS_SPOTS` 정적 → `MapLayout` 주입 참조 |
| `Assets/Scripts/Player/PlayerMovement.cs` | Modify | 소유자 텔레포트가 주입된 layout 사용 (변경 최소) |
| `Assets/Scripts/Tests/EditMode/MapValidatorTests.cs` | Create | V1~V12 단위 테스트 |
| `Assets/Scripts/Tests/EditMode/MapGeneratorTests.cs` | Create | 결정성·완전성·100시드 통과율 테스트 |
| `Assets/Prefabs/Maps/MapCandidatePool.asset` | Create(툴) | 후보풀 에셋 |
| `Assets/Prefabs/Maps/Layout_Sketch01.asset` | Create(툴) | 사용자 시안 = MapLayout 1호 |

---

### Task 1: 맵 데이터 구조체 + SO 2종 스키마 (TDD 아님 — 데이터 정의)

**Files:**
- Create: `Assets/Scripts/Map/MapData.cs`, `Assets/Scripts/Map/MapLayout.cs`, `Assets/Scripts/Map/MapCandidatePool.cs`

- [ ] **Step 1: MapData.cs 작성** — 직렬화 구조체 (좌표는 전부 x,z 평면. y는 굽기 시 상수):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>방(지형) 사각형 — center/size는 x(동)·z(북) 평면. 경계 = center ± size/2.</summary>
    [System.Serializable]
    public struct RoomRect
    {
        public string Id;
        public Vector2 Center;
        public Vector2 Size;

        public Vector2 Min => Center - Size * 0.5f;
        public Vector2 Max => Center + Size * 0.5f;
        public bool Contains(Vector2 p) =>
            p.x >= Min.x && p.x <= Max.x && p.y >= Min.y && p.y <= Max.y;
    }

    /// <summary>통로/미로 세그먼트 — 중심선 두 끝점(x,z) + 폭. 굽기 시 폭만큼의 바닥 통로 박스.</summary>
    [System.Serializable]
    public struct CorridorSeg
    {
        public Vector2 From;
        public Vector2 To;
        public float Width;
    }

    /// <summary>치즈 지점 — 위치(x,z)·등급 1~7·반경.</summary>
    [System.Serializable]
    public struct CheesePlacement
    {
        public Vector2 Pos;
        public int Grade;
        public float Radius;
    }

    /// <summary>요새 자리 — 위치(x,z)·입구 yaw(도). 입구 앞 1.8m가 쥐 스폰.</summary>
    [System.Serializable]
    public struct FortressPlacement
    {
        public Vector2 Pos;
        public float YawDeg;
    }

    /// <summary>미로 프리셋 — 세그먼트 묶음 + 배타 방 id(같은 영역이면 둘 중 하나만).</summary>
    [System.Serializable]
    public struct MazePreset
    {
        public string Id;
        public string ExclusiveRoomId;   // 이 방과 동시 선택 금지
        public List<CorridorSeg> Segments;
    }
}
```

- [ ] **Step 2: MapLayout.cs 작성** — 확정 결과물 SO:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 생성기(또는 손작업)가 만든 확정 맵. 씬 굽기·런타임 스폰이 이 에셋을 읽는다.
    /// source = "seed:12345" 또는 "hand-authored".
    /// </summary>
    [CreateAssetMenu(fileName = "MapLayout", menuName = "CTC/Map Layout")]
    public class MapLayout : ScriptableObject
    {
        [SerializeField] private int m_Seed;
        [SerializeField] private string m_Source;
        [SerializeField] private List<RoomRect> m_Rooms = new();
        [SerializeField] private List<CorridorSeg> m_Corridors = new();
        [SerializeField] private List<CheesePlacement> m_Cheese = new();
        [SerializeField] private List<FortressPlacement> m_Fortresses = new();

        public int Seed => m_Seed;
        public string Source => m_Source;
        public IReadOnlyList<RoomRect> Rooms => m_Rooms;
        public IReadOnlyList<CorridorSeg> Corridors => m_Corridors;
        public IReadOnlyList<CheesePlacement> Cheese => m_Cheese;
        public IReadOnlyList<FortressPlacement> Fortresses => m_Fortresses;

        /// <summary>생성기/툴이 결과를 채울 때 사용(에디터 전용 경로).</summary>
        public void Set(int seed, string source, List<RoomRect> rooms, List<CorridorSeg> corridors,
            List<CheesePlacement> cheese, List<FortressPlacement> fortresses)
        {
            m_Seed = seed;
            m_Source = source;
            m_Rooms = rooms;
            m_Corridors = corridors;
            m_Cheese = cheese;
            m_Fortresses = fortresses;
        }
    }
}
```

- [ ] **Step 3: MapCandidatePool.cs 작성** — 후보 슬롯 SO. 후보풀 명세 §1~5 필드를 그대로:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 손검수한 후보 슬롯 모음(level-design 명세를 데이터화). 생성기가 시드로 뽑아 MapLayout을 만든다.
    /// 좌표를 바꾸면 MapValidator 임계값(요새-치즈 이격 등)도 함께 검토.
    /// </summary>
    [CreateAssetMenu(fileName = "MapCandidatePool", menuName = "CTC/Map Candidate Pool")]
    public class MapCandidatePool : ScriptableObject
    {
        [System.Serializable]
        public struct RoomCandidate
        {
            public string Id;
            public Vector2 Center;
            public Vector2 Size;
            public RoomTag Tag;
            public bool AlwaysInclude;   // R3·R5
        }

        [System.Serializable]
        public struct CorridorCandidate
        {
            public string Id;
            public string RoomA;
            public string RoomB;
            public List<Vector2> Waypoints;   // 중심선 절점 (>=2)
            public float Width;
        }

        [System.Serializable]
        public struct CheeseSlot
        {
            public string RoomId;
            public Vector2 Pos;
            public int MinGrade;   // 밴드 하한
            public int MaxGrade;   // 밴드 상한
            public bool Fixed;     // G1 고정
            public int FixedGrade; // Fixed일 때 등급
            public float Radius;
        }

        [System.Serializable]
        public struct FortressSlot
        {
            public string RoomId;
            public Vector2 Pos;
            public float YawDeg;
            public bool AlwaysInclude;   // F0
        }

        public enum RoomTag { Outer, Middle, Center }

        [SerializeField] private List<RoomCandidate> m_Rooms = new();
        [SerializeField] private List<CorridorCandidate> m_Corridors = new();
        [SerializeField] private List<MazePreset> m_Mazes = new();
        [SerializeField] private List<CheeseSlot> m_CheeseSlots = new();
        [SerializeField] private List<FortressSlot> m_FortressSlots = new();

        public IReadOnlyList<RoomCandidate> Rooms => m_Rooms;
        public IReadOnlyList<CorridorCandidate> Corridors => m_Corridors;
        public IReadOnlyList<MazePreset> Mazes => m_Mazes;
        public IReadOnlyList<CheeseSlot> CheeseSlots => m_CheeseSlots;
        public IReadOnlyList<FortressSlot> FortressSlots => m_FortressSlots;

        public void Set(List<RoomCandidate> rooms, List<CorridorCandidate> corridors, List<MazePreset> mazes,
            List<CheeseSlot> cheese, List<FortressSlot> fortress)
        {
            m_Rooms = rooms; m_Corridors = corridors; m_Mazes = mazes;
            m_CheeseSlots = cheese; m_FortressSlots = fortress;
        }
    }
}
```

- [ ] **Step 4: 컴파일 확인** — refresh + focus-unity → `u_console` CS 에러 0. (테스트 변동 없음 — 데이터만)

- [ ] **Step 5: Commit**

```bash
git add -A CTC/Assets/Scripts/Map
git commit -m "feat: map data structs + MapLayout/MapCandidatePool SO schemas

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: MapValidator — 정적 검증 V1~V12 (TDD)

**Files:**
- Create: `Assets/Scripts/Map/MapValidator.cs`, `Assets/Scripts/Tests/EditMode/MapValidatorTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `MapValidatorTests.cs`:

```csharp
using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MapValidatorTests
    {
        private static RoomRect Room(string id, float cx, float cz, float sx, float sz) =>
            new RoomRect { Id = id, Center = new Vector2(cx, cz), Size = new Vector2(sx, sz) };

        private static CorridorSeg Seg(float ax, float az, float bx, float bz, float w = 1.6f) =>
            new CorridorSeg { From = new Vector2(ax, az), To = new Vector2(bx, bz), Width = w };

        // R3(중앙)·R5(남서)만 있고 통로 1개로 이은 최소 유효 맵
        private static (List<RoomRect>, List<CorridorSeg>) MinimalConnected()
        {
            var rooms = new List<RoomRect> { Room("R3", 0, 1.5f, 7, 9), Room("R5", -10.5f, -10.5f, 7, 8) };
            var corr = new List<CorridorSeg> { Seg(0f, -3f, -10.5f, -6.5f) };
            return (rooms, corr);
        }

        [Test]
        public void V4_OverlappingRooms_Fails()
        {
            var rooms = new List<RoomRect> { Room("A", 0, 0, 5, 5), Room("B", 2, 2, 5, 5) };
            Assert.IsFalse(MapValidator.NoRoomOverlap(rooms));
        }

        [Test]
        public void V4_SeparatedRooms_Passes()
        {
            var rooms = new List<RoomRect> { Room("A", 0, 0, 5, 5), Room("B", 10, 10, 5, 5) };
            Assert.IsTrue(MapValidator.NoRoomOverlap(rooms));
        }

        [Test]
        public void V3_AllRoomsReachable_Passes()
        {
            var (rooms, corr) = MinimalConnected();
            Assert.IsTrue(MapValidator.AllConnected(rooms, corr, "R3"));
        }

        [Test]
        public void V3_OrphanRoom_Fails()
        {
            var rooms = new List<RoomRect> { Room("R3", 0, 1.5f, 7, 9), Room("R5", -10.5f, -10.5f, 7, 8) };
            var corr = new List<CorridorSeg>(); // 통로 없음 → R5 고아
            Assert.IsFalse(MapValidator.AllConnected(rooms, corr, "R3"));
        }

        [Test]
        public void V6_NarrowCorridor_Fails()
        {
            var corr = new List<CorridorSeg> { Seg(0, 0, 5, 0, 1.2f) };
            Assert.IsFalse(MapValidator.CorridorsWideEnough(corr, 1.6f));
        }

        [Test]
        public void V11_CheeseGradesComplete_Passes()
        {
            var cheese = new List<CheesePlacement>();
            for (int g = 1; g <= 7; g++)
                cheese.Add(new CheesePlacement { Pos = new Vector2(g, 0), Grade = g, Radius = 2f });
            Assert.IsTrue(MapValidator.CheeseGradesComplete(cheese));
        }

        [Test]
        public void V11_MissingGrade_Fails()
        {
            var cheese = new List<CheesePlacement>();
            for (int g = 1; g <= 6; g++) // 7 누락
                cheese.Add(new CheesePlacement { Pos = new Vector2(g, 0), Grade = g, Radius = 2f });
            Assert.IsFalse(MapValidator.CheeseGradesComplete(cheese));
        }

        [Test]
        public void V9_FortressCheeseTooClose_Fails()
        {
            var forts = new List<FortressPlacement> { new() { Pos = new Vector2(0, 0), YawDeg = 0 } };
            var cheese = new List<CheesePlacement> { new() { Pos = new Vector2(2f, 0), Grade = 1, Radius = 2f } };
            Assert.IsFalse(MapValidator.FortressCheeseSpacing(forts, cheese, 3.7f));
        }

        [Test]
        public void V9_FortressCheeseFarEnough_Passes()
        {
            var forts = new List<FortressPlacement> { new() { Pos = new Vector2(0, 0), YawDeg = 0 } };
            var cheese = new List<CheesePlacement> { new() { Pos = new Vector2(4f, 0), Grade = 1, Radius = 2f } };
            Assert.IsTrue(MapValidator.FortressCheeseSpacing(forts, cheese, 3.7f));
        }
    }
}
```

- [ ] **Step 2: EditMode 실행 → 컴파일 실패 확인** — `MapValidator` 없음 CS 에러

- [ ] **Step 3: MapValidator.cs 구현**:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 생성 결과의 하드 게이트 검사(후보풀 명세 §6-A V1~V12). 전부 순수 함수 — EditMode 테스트.
    /// 실패한 시드는 생성기가 폐기하고 다음 시드로 재시도한다.
    /// </summary>
    public static class MapValidator
    {
        public const float MIN_CORRIDOR_WIDTH = 1.6f;
        public const float MIN_FORTRESS_CHEESE_DIST = 3.7f;

        // V4: 방-방 AABB 상호 미침범
        public static bool NoRoomOverlap(IReadOnlyList<RoomRect> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                    if (AabbOverlap(rooms[i], rooms[j])) return false;
            return true;
        }

        private static bool AabbOverlap(RoomRect a, RoomRect b) =>
            a.Min.x < b.Max.x && a.Max.x > b.Min.x && a.Min.y < b.Max.y && a.Max.y > b.Min.y;

        // V3: 모든 방이 통로 그래프로 start에서 도달 (통로 끝점이 방 경계에 접하면 연결로 간주)
        public static bool AllConnected(
            IReadOnlyList<RoomRect> rooms, IReadOnlyList<CorridorSeg> corridors, string startId)
        {
            var adj = BuildRoomAdjacency(rooms, corridors);
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(startId);
            visited.Add(startId);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                if (!adj.TryGetValue(cur, out var nbrs)) continue;
                foreach (string n in nbrs)
                    if (visited.Add(n)) queue.Enqueue(n);
            }
            return visited.Count == rooms.Count;
        }

        // 통로 세그먼트 끝점이 어느 방 경계(±0.6 여유)에 닿는지로 방-방 인접 구성.
        // 세그먼트 체인(미로 포함)은 끝점 공유(±0.2)로 이어 하나의 통로망으로 병합.
        private static Dictionary<string, HashSet<string>> BuildRoomAdjacency(
            IReadOnlyList<RoomRect> rooms, IReadOnlyList<CorridorSeg> corridors)
        {
            // 각 세그먼트 끝점이 닿는 방 목록
            var adj = new Dictionary<string, HashSet<string>>();
            foreach (var r in rooms) adj[r.Id] = new HashSet<string>();

            // 세그먼트를 유니온-파인드로 묶어 통로망 컴포넌트 형성
            var uf = new UnionFind(corridors.Count);
            for (int i = 0; i < corridors.Count; i++)
                for (int j = i + 1; j < corridors.Count; j++)
                    if (EndpointsTouch(corridors[i], corridors[j])) uf.Union(i, j);

            // 컴포넌트별로 닿는 방 집합 → 그 방들끼리 상호 연결
            var compRooms = new Dictionary<int, HashSet<string>>();
            for (int i = 0; i < corridors.Count; i++)
            {
                int root = uf.Find(i);
                if (!compRooms.TryGetValue(root, out var set)) { set = new HashSet<string>(); compRooms[root] = set; }
                foreach (var r in rooms)
                {
                    if (PointNearRoom(corridors[i].From, r) || PointNearRoom(corridors[i].To, r))
                        set.Add(r.Id);
                }
            }
            foreach (var set in compRooms.Values)
                foreach (string a in set)
                    foreach (string b in set)
                        if (a != b) adj[a].Add(b);
            return adj;
        }

        private static bool EndpointsTouch(CorridorSeg a, CorridorSeg b)
        {
            const float EPS = 0.25f;
            return Vector2.Distance(a.From, b.From) < EPS || Vector2.Distance(a.From, b.To) < EPS
                || Vector2.Distance(a.To, b.From) < EPS || Vector2.Distance(a.To, b.To) < EPS;
        }

        private static bool PointNearRoom(Vector2 p, RoomRect r)
        {
            const float PAD = 0.7f; // 통로 폭 절반 + 여유 — 경계 접점 판정
            return p.x >= r.Min.x - PAD && p.x <= r.Max.x + PAD
                && p.y >= r.Min.y - PAD && p.y <= r.Max.y + PAD;
        }

        // V6: 모든 통로 폭 >= min
        public static bool CorridorsWideEnough(IReadOnlyList<CorridorSeg> corridors, float min)
        {
            foreach (var c in corridors)
                if (c.Width < min - 1e-4f) return false;
            return true;
        }

        // V11: 등급 다중집합 == {1..7} 정확히
        public static bool CheeseGradesComplete(IReadOnlyList<CheesePlacement> cheese)
        {
            if (cheese.Count != 7) return false;
            var seen = new HashSet<int>();
            foreach (var c in cheese)
                if (c.Grade < 1 || c.Grade > 7 || !seen.Add(c.Grade)) return false;
            return seen.Count == 7;
        }

        // V9: 모든 요새-치즈 중심거리 >= min
        public static bool FortressCheeseSpacing(
            IReadOnlyList<FortressPlacement> forts, IReadOnlyList<CheesePlacement> cheese, float min)
        {
            foreach (var f in forts)
                foreach (var c in cheese)
                    if (Vector2.Distance(f.Pos, c.Pos) < min) return false;
            return true;
        }

        // 소형 유니온-파인드 (세그먼트 컴포넌트용)
        private class UnionFind
        {
            private readonly int[] m_Parent;
            public UnionFind(int n) { m_Parent = new int[n]; for (int i = 0; i < n; i++) m_Parent[i] = i; }
            public int Find(int x) { while (m_Parent[x] != x) { m_Parent[x] = m_Parent[m_Parent[x]]; x = m_Parent[x]; } return x; }
            public void Union(int a, int b) { m_Parent[Find(a)] = Find(b); }
        }
    }
}
```

- [ ] **Step 4: EditMode 실행 → 통과 확인** — Expected: **Passed 90** (81 + 신규 9)
- [ ] **Step 5: Commit** — `feat: map layout validator (V3/V4/V6/V9/V11 pure checks)`

---

### Task 3: MapGenerator — 방선택·통로연결·치즈/요새 배정 (TDD)

**Files:**
- Create: `Assets/Scripts/Map/MapGenerator.cs`, `Assets/Scripts/Tests/EditMode/MapGeneratorTests.cs`

생성 알고리즘 (결정적, `System.Random(seed)`):
1. **방 선택** — `AlwaysInclude`(R3·R5) 먼저, 나머지 후보를 셔플해 총 5~6개가 되게 채움. 남부 배타쌍(미로A⇔R9, 미로B⇔R8) 위반 시 재추출.
2. **통로 선택** — 선택된 방쌍에 해당하는 `CorridorCandidate`를 전부 채택(조건 충족분). 남부는 미로 프리셋으로 연결.
3. **미로 배치** — 선택된 방과 배타 안 겹치는 미로 0~2개 채택(남부 슬롯 채움).
4. **치즈 배정** — 슬롯에서 G1 고정 → G7(중앙/미로 심부) → G6 → G2~G5(외곽·중간 밴드 오름차순). 총 7개 소진.
5. **요새 배정** — F0 고정 첫째, 나머지 선택 외곽/중간 방에 라운드로빈으로 6개(부족분 F1b 보조).
6. **검증** — `MapValidator` 전항 통과 시 반환, 아니면 다음 시드로 재시도(최대 200회).

- [ ] **Step 1: 실패 테스트 작성** — `MapGeneratorTests.cs` (풀은 코드로 구성해 SO 의존 제거):

```csharp
using System.Collections.Generic;
using System.Linq;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MapGeneratorTests
    {
        // 테스트용 최소 풀 — R3·R5(필수) + 외곽 R1·R2·R6 + 미로A. 후보풀 명세 좌표 축약.
        private static MapGenerator.PoolData BuildTestPool()
        {
            var rooms = new List<MapGenerator.RoomOption>
            {
                new("R3", new Vector2(0, 1.5f), new Vector2(7, 9), MapGenerator.Tag.Center, true),
                new("R5", new Vector2(-10.5f, -10.5f), new Vector2(7, 8), MapGenerator.Tag.Outer, true),
                new("R1", new Vector2(10.5f, 12f), new Vector2(4.5f, 5.5f), MapGenerator.Tag.Outer, false),
                new("R2", new Vector2(-12.5f, 3f), new Vector2(4, 6), MapGenerator.Tag.Outer, false),
                new("R6", new Vector2(-11f, 11f), new Vector2(5, 5), MapGenerator.Tag.Outer, false),
            };
            var corr = new List<MapGenerator.CorridorOption>
            {
                new("C1", "R3", "R2", new[] { new Vector2(-3.5f, 2.5f), new Vector2(-10.5f, 2.5f) }, 1.6f),
                new("C5", "R2", "R6", new[] { new Vector2(-12.5f, 6f), new Vector2(-11f, 8.5f) }, 1.6f),
                new("C10", "R5", "R2", new[] { new Vector2(-11f, -6.5f), new Vector2(-12.5f, 0f) }, 1.6f),
                new("C6b", "R6", "R1", new[] { new Vector2(-8.5f, 12f), new Vector2(8.25f, 12f) }, 1.6f),
            };
            // 치즈/요새 슬롯은 §4·§5 축약 — 등급 완전 소진 가능하게 구성
            var cheese = new List<MapGenerator.CheeseSlotOption>
            {
                new("R5", new Vector2(-9, -7), 1, 1, true, 1, 2f),   // G1 고정
                new("R2", new Vector2(-11.5f, 5), 2, 3, false, 0, 2f),
                new("R6", new Vector2(-9, 9.5f), 2, 3, false, 0, 2f),
                new("R1", new Vector2(8.75f, 10), 3, 4, false, 0, 2f),
                new("R3", new Vector2(-2, 4), 6, 6, false, 0, 2f),
                new("R3", new Vector2(0, -2), 7, 7, false, 0, 2f),
                new("R2", new Vector2(-13, 4), 4, 5, false, 0, 2f),
                new("R6", new Vector2(-12, 12), 4, 5, false, 0, 2f),
            };
            var fort = new List<MapGenerator.FortressSlotOption>
            {
                new("R5", new Vector2(-11, -12), 0f, true),
                new("R2", new Vector2(-13, 1.5f), 90f, false),
                new("R6", new Vector2(-12, 12), 180f, false),
                new("R1", new Vector2(11.5f, 13), 180f, false),
            };
            return new MapGenerator.PoolData(rooms, corr, new List<MazePreset>(), cheese, fort);
        }

        [Test]
        public void Generate_SameSeed_Deterministic()
        {
            var pool = BuildTestPool();
            var a = MapGenerator.Generate(pool, 12345);
            var b = MapGenerator.Generate(pool, 12345);
            Assert.IsNotNull(a);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
                Assert.AreEqual(a.Rooms[i].Id, b.Rooms[i].Id);
        }

        [Test]
        public void Generate_AlwaysIncludesRequiredRooms()
        {
            var pool = BuildTestPool();
            var layout = MapGenerator.Generate(pool, 777);
            Assert.IsNotNull(layout);
            var ids = layout.Rooms.Select(r => r.Id).ToHashSet();
            Assert.IsTrue(ids.Contains("R3"));
            Assert.IsTrue(ids.Contains("R5"));
        }

        [Test]
        public void Generate_G1_FixedAtAnchor()
        {
            var pool = BuildTestPool();
            var layout = MapGenerator.Generate(pool, 42);
            Assert.IsNotNull(layout);
            var g1 = layout.Cheese.First(c => c.Grade == 1);
            Assert.Less(Vector2.Distance(g1.Pos, new Vector2(-9, -7)), 0.05f);
        }

        [Test]
        public void Generate_CheeseGradesComplete()
        {
            var pool = BuildTestPool();
            var layout = MapGenerator.Generate(pool, 99);
            Assert.IsNotNull(layout);
            Assert.IsTrue(MapValidator.CheeseGradesComplete(layout.Cheese));
        }

        [Test]
        public void Generate_100Seeds_HighSuccessRate()
        {
            var pool = BuildTestPool();
            int ok = 0;
            for (int s = 0; s < 100; s++)
                if (MapGenerator.Generate(pool, s) != null) ok++;
            Assert.GreaterOrEqual(ok, 90, $"생성 성공 {ok}/100 (기준 90)");
        }
    }
}
```

- [ ] **Step 2: EditMode 실행 → 컴파일 실패 확인** — `MapGenerator` 없음

- [ ] **Step 3: MapGenerator.cs 구현** — 순수 로직. `PoolData`는 SO 비의존 입력 DTO(테스트·에디터 공용). 생성 결과는 `LayoutResult`(SO 아님, 순수 데이터):

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 순수 결정적 맵 생성기. SO에 의존하지 않는 DTO(PoolData)를 받아 LayoutResult를 만든다 —
    /// EditMode 테스트가 SO 없이 호출, 에디터 툴이 SO를 DTO로 변환해 호출.
    /// 검증(MapValidator) 실패 시 다음 시드로 재시도(최대 MAX_RETRY).
    /// </summary>
    public static class MapGenerator
    {
        private const int MAX_RETRY = 200;
        private const int MIN_ROOMS = 5;
        private const int MAX_ROOMS = 6;

        public enum Tag { Outer, Middle, Center }

        public readonly struct RoomOption
        {
            public readonly string Id; public readonly Vector2 Center, Size;
            public readonly Tag Tag; public readonly bool Always;
            public RoomOption(string id, Vector2 c, Vector2 s, Tag t, bool a)
            { Id = id; Center = c; Size = s; Tag = t; Always = a; }
        }
        public readonly struct CorridorOption
        {
            public readonly string Id, A, B; public readonly Vector2[] Waypoints; public readonly float Width;
            public CorridorOption(string id, string a, string b, Vector2[] wp, float w)
            { Id = id; A = a; B = b; Waypoints = wp; Width = w; }
        }
        public readonly struct CheeseSlotOption
        {
            public readonly string RoomId; public readonly Vector2 Pos;
            public readonly int MinGrade, MaxGrade, FixedGrade; public readonly bool Fixed; public readonly float Radius;
            public CheeseSlotOption(string room, Vector2 p, int min, int max, bool fix, int fg, float r)
            { RoomId = room; Pos = p; MinGrade = min; MaxGrade = max; Fixed = fix; FixedGrade = fg; Radius = r; }
        }
        public readonly struct FortressSlotOption
        {
            public readonly string RoomId; public readonly Vector2 Pos; public readonly float Yaw; public readonly bool Always;
            public FortressSlotOption(string room, Vector2 p, float yaw, bool always)
            { RoomId = room; Pos = p; Yaw = yaw; Always = always; }
        }

        public class PoolData
        {
            public readonly List<RoomOption> Rooms;
            public readonly List<CorridorOption> Corridors;
            public readonly List<MazePreset> Mazes;
            public readonly List<CheeseSlotOption> Cheese;
            public readonly List<FortressSlotOption> Fortress;
            public PoolData(List<RoomOption> r, List<CorridorOption> c, List<MazePreset> m,
                List<CheeseSlotOption> ch, List<FortressSlotOption> f)
            { Rooms = r; Corridors = c; Mazes = m; Cheese = ch; Fortress = f; }
        }

        public class LayoutResult
        {
            public int Seed;
            public List<RoomRect> Rooms = new();
            public List<CorridorSeg> Corridors = new();
            public List<CheesePlacement> Cheese = new();
            public List<FortressPlacement> Fortresses = new();
        }

        /// <summary>시드로 유효 맵 생성. 재시도 소진 시 null.</summary>
        public static LayoutResult Generate(PoolData pool, int seed)
        {
            for (int attempt = 0; attempt < MAX_RETRY; attempt++)
            {
                var rng = new System.Random(seed + attempt * 7919);
                LayoutResult r = TryBuild(pool, rng, seed);
                if (r != null && Validate(r)) return r;
            }
            return null;
        }

        private static LayoutResult TryBuild(PoolData pool, System.Random rng, int seed)
        {
            // 1. 방 선택
            var chosen = pool.Rooms.Where(r => r.Always).ToList();
            var rest = pool.Rooms.Where(r => !r.Always).OrderBy(_ => rng.Next()).ToList();
            int target = MIN_ROOMS + rng.Next(0, MAX_ROOMS - MIN_ROOMS + 1);
            foreach (var r in rest)
            {
                if (chosen.Count >= target) break;
                chosen.Add(r);
            }
            var chosenIds = chosen.Select(r => r.Id).ToHashSet();

            var result = new LayoutResult { Seed = seed };
            result.Rooms = chosen.Select(r => new RoomRect { Id = r.Id, Center = r.Center, Size = r.Size }).ToList();

            // 2. 통로 — 양 끝 방이 모두 선택됐으면 채택
            foreach (var c in pool.Corridors)
            {
                if (!chosenIds.Contains(c.A) || !chosenIds.Contains(c.B)) continue;
                for (int i = 0; i < c.Waypoints.Length - 1; i++)
                    result.Corridors.Add(new CorridorSeg { From = c.Waypoints[i], To = c.Waypoints[i + 1], Width = c.Width });
            }

            // 3. 미로 — 배타 방 미선택 시 채택 (남부 슬롯)
            foreach (var m in pool.Mazes)
            {
                if (!string.IsNullOrEmpty(m.ExclusiveRoomId) && chosenIds.Contains(m.ExclusiveRoomId)) continue;
                if (m.Segments != null) result.Corridors.AddRange(m.Segments);
            }

            // 4. 치즈 배정
            if (!AssignCheese(pool, chosenIds, result)) return null;

            // 5. 요새 배정
            if (!AssignFortresses(pool, chosenIds, result)) return null;

            return result;
        }

        // G1 고정 → G7·G6(중앙/미로) → G2~G5(외곽·중간 밴드). 등급 완전 소진 실패 시 null.
        private static bool AssignCheese(PoolData pool, HashSet<string> chosenIds, LayoutResult result)
        {
            var avail = pool.Cheese.Where(s => chosenIds.Contains(s.RoomId)).ToList();
            var used = new HashSet<int>();
            var placed = new List<CheesePlacement>();

            // 고정 슬롯
            foreach (var s in avail.Where(s => s.Fixed))
            {
                placed.Add(new CheesePlacement { Pos = s.Pos, Grade = s.FixedGrade, Radius = s.Radius });
                used.Add(s.FixedGrade);
            }
            // 나머지 등급 7..2 내림차순으로, 밴드 맞는 슬롯에서 배정(고등급 우선 = 중앙/미로 심부)
            var free = avail.Where(s => !s.Fixed).ToList();
            var usedSlots = new HashSet<int>();
            for (int g = 7; g >= 1; g--)
            {
                if (used.Contains(g)) continue;
                int pick = -1;
                for (int i = 0; i < free.Count; i++)
                {
                    if (usedSlots.Contains(i)) continue;
                    if (g >= free[i].MinGrade && g <= free[i].MaxGrade) { pick = i; break; }
                }
                if (pick < 0) return false; // 이 등급 채울 슬롯 없음 → 폐기
                usedSlots.Add(pick);
                placed.Add(new CheesePlacement { Pos = free[pick].Pos, Grade = g, Radius = free[pick].Radius });
                used.Add(g);
            }
            result.Cheese = placed;
            return used.Count == 7;
        }

        // F0 고정 첫째 + 선택 외곽/중간 방에 라운드로빈으로 총 6개(부족분은 남은 슬롯).
        private static bool AssignFortresses(PoolData pool, HashSet<string> chosenIds, LayoutResult result)
        {
            var avail = pool.Fortress.Where(s => chosenIds.Contains(s.RoomId)).ToList();
            var placed = new List<FortressPlacement>();
            foreach (var s in avail.Where(s => s.Always))
                placed.Add(new FortressPlacement { Pos = s.Pos, YawDeg = s.Yaw });
            foreach (var s in avail.Where(s => !s.Always))
            {
                if (placed.Count >= 6) break;
                placed.Add(new FortressPlacement { Pos = s.Pos, YawDeg = s.Yaw });
            }
            result.Fortresses = placed;
            return placed.Count >= 1; // 최소 F0. 쥐 수 초과분은 런타임에서 요새 공유(SpawnPosition % 개수)
        }

        private static bool Validate(LayoutResult r) =>
            MapValidator.NoRoomOverlap(r.Rooms)
            && MapValidator.AllConnected(r.Rooms, r.Corridors, "R3")
            && MapValidator.CorridorsWideEnough(r.Corridors, MapValidator.MIN_CORRIDOR_WIDTH)
            && MapValidator.CheeseGradesComplete(r.Cheese)
            && MapValidator.FortressCheeseSpacing(r.Fortresses, r.Cheese, MapValidator.MIN_FORTRESS_CHEESE_DIST);
    }
}
```

- [ ] **Step 4: EditMode 실행 → 통과 확인** — Expected: **Passed 95** (90 + 신규 5). 100시드 통과율 테스트가 <90이면 테스트 풀 슬롯 밴드를 조정(치즈 슬롯이 등급 소진 가능한지 재확인)
- [ ] **Step 5: Commit** — `feat: deterministic seed map generator with retry-on-invalid`

---

### Task 4: 후보풀 에셋 빌더 (에디터 1회성 메뉴)

**Files:**
- Create: `Assets/Scripts/Editor/MapCandidatePoolBuilder.cs`
- Create(툴 실행 결과): `Assets/Prefabs/Maps/MapCandidatePool.asset`

- [ ] **Step 1: MapCandidatePoolBuilder.cs 작성** — 후보풀 명세 §1~5 값(§ 정정 반영)을 코드로 담아 `.asset` 생성:

```csharp
using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using Pool = CTC.Runtime.Map.MapCandidatePool;

namespace CTC.Editor
{
    /// <summary>
    /// 후보풀 명세(docs/.../2026-07-16-map-candidate-pool.md §1~5)를 SO 에셋으로 굽는다.
    /// F1b·F4는 요새 풋프린트가 방을 넘던 명세값을 정정(계획 §정정표).
    /// Tools > CTC > Build Map Candidate Pool
    /// </summary>
    public static class MapCandidatePoolBuilder
    {
        private const string PoolPath = "Assets/Prefabs/Maps/MapCandidatePool.asset";

        [MenuItem("Tools/CTC/Build Map Candidate Pool")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Maps"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Maps");

            var rooms = new List<Pool.RoomCandidate>
            {
                Room("R3", 0, 1.5f, 7, 9, Pool.RoomTag.Center, true),
                Room("R5", -10.5f, -10.5f, 7, 8, Pool.RoomTag.Outer, true),
                Room("R1", 10.5f, 12f, 4.5f, 5.5f, Pool.RoomTag.Outer, false),
                Room("R2", -12.5f, 3f, 4, 6, Pool.RoomTag.Outer, false),
                Room("R4", 11f, 2f, 5.5f, 7, Pool.RoomTag.Middle, false),
                Room("R6", -11f, 11f, 5, 5, Pool.RoomTag.Outer, false),
                Room("R7", 0, 12f, 5, 4, Pool.RoomTag.Middle, false),
                Room("R8", 11f, -10f, 5, 6, Pool.RoomTag.Outer, false),
                Room("R9", 0, -11f, 5, 5, Pool.RoomTag.Outer, false),
            };

            var corr = new List<Pool.CorridorCandidate>
            {
                Corr("C1", "R3", "R2", 1.6f, V(-3.5f, 2.5f), V(-10.5f, 2.5f)),
                Corr("C2", "R3", "R4", 1.6f, V(3.5f, 2.5f), V(8.25f, 2.5f)),
                Corr("C3", "R3", "R7", 1.6f, V(0, 6), V(0, 10)),
                Corr("C5", "R2", "R6", 1.6f, V(-12.5f, 6), V(-13.3f, 7.5f), V(-11, 8.5f)),
                Corr("C6a", "R6", "R7", 1.6f, V(-8.5f, 12), V(-2.5f, 12)),
                Corr("C6b", "R7", "R1", 1.6f, V(2.5f, 12), V(8.25f, 12)),
                Corr("C6c", "R6", "R1", 1.6f, V(-8.5f, 13), V(8.25f, 13)),
                Corr("C7", "R1", "R4", 1.6f, V(11, 9.25f), V(11, 5.5f)),
                Corr("C10", "R5", "R2", 1.6f, V(-11, -6.5f), V(-12.5f, -6.5f), V(-12.5f, 0)),
            };

            var mazes = new List<MazePreset>
            {
                MazeA(), MazeB(),
            };

            var cheese = new List<Pool.CheeseSlot>
            {
                Cheese("R5", -9, -7, 1, 1, true, 1, 2f),           // G1 고정
                Cheese("R2", -11.5f, 5, 2, 3, false, 0, 1.5f),
                Cheese("R6", -9, 9.5f, 2, 3, false, 0, 1.5f),
                Cheese("R1", 8.75f, 10, 2, 3, false, 0, 1.5f),
                Cheese("R8", 9, -8, 2, 3, false, 0, 1.5f),
                Cheese("R9", 1.8f, -9, 3, 4, false, 0, 1.5f),
                Cheese("R4", 10, 2, 4, 5, false, 0, 1.5f),
                Cheese("R7", 0, 11, 4, 5, false, 0, 1.5f),
                Cheese("R3", -2, 4, 6, 6, false, 0, 1.5f),
                Cheese("R3", 0, -2, 7, 7, false, 0, 1.5f),
            };

            var fort = new List<Pool.FortressSlot>
            {
                Fort("R5", -11, -12, 0f, true),      // F0 고정
                Fort("R8", 12, -11, 0f, false),
                Fort("R6", -12, 12, 180f, false),
                Fort("R1", 11.5f, 13, 180f, false),
                Fort("R9", 0, -12.3f, 0f, false),    // 정정: -12.5 → -12.3
                Fort("R2", -13, 1.5f, 90f, false),
                Fort("R4", 12.5f, 4.5f, 270f, false),// 정정: 13 → 12.5
            };

            var pool = AssetDatabase.LoadAssetAtPath<Pool>(PoolPath);
            if (pool == null) { pool = ScriptableObject.CreateInstance<Pool>(); AssetDatabase.CreateAsset(pool, PoolPath); }
            pool.Set(rooms, corr, mazes, cheese, fort);
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapCandidatePoolBuilder] 후보풀 저장 — 방 {rooms.Count}·통로 {corr.Count}·미로 {mazes.Count}·치즈 {cheese.Count}·요새 {fort.Count}");
        }

        private static Vector2 V(float x, float z) => new Vector2(x, z);

        private static Pool.RoomCandidate Room(string id, float cx, float cz, float sx, float sz, Pool.RoomTag tag, bool always) =>
            new() { Id = id, Center = V(cx, cz), Size = V(sx, sz), Tag = tag, AlwaysInclude = always };

        private static Pool.CorridorCandidate Corr(string id, string a, string b, float w, params Vector2[] wp) =>
            new() { Id = id, RoomA = a, RoomB = b, Width = w, Waypoints = new List<Vector2>(wp) };

        private static Pool.CheeseSlot Cheese(string room, float x, float z, int min, int max, bool fix, int fg, float r) =>
            new() { RoomId = room, Pos = V(x, z), MinGrade = min, MaxGrade = max, Fixed = fix, FixedGrade = fg, Radius = r };

        private static Pool.FortressSlot Fort(string room, float x, float z, float yaw, bool always) =>
            new() { RoomId = room, Pos = V(x, z), YawDeg = yaw, AlwaysInclude = always };

        private static CorridorSeg Seg(float ax, float az, float bx, float bz) =>
            new() { From = V(ax, az), To = V(bx, bz), Width = 1.6f };

        // 미로-A (남중앙) — 명세 §3
        private static MazePreset MazeA() => new()
        {
            Id = "MazeA", ExclusiveRoomId = "R9",
            Segments = new List<CorridorSeg>
            {
                Seg(1.7f, -4, 1.7f, -12), Seg(1.7f, -7, -3, -7), Seg(-3, -7, -3, -12.5f),
                Seg(-3, -12.5f, -4, -12.5f), Seg(1.7f, -8, 4, -8), Seg(1.7f, -12, 3, -12), Seg(3, -12, 3, -13.5f),
            },
        };

        // 미로-B (남동) — 명세 §3
        private static MazePreset MazeB() => new()
        {
            Id = "MazeB", ExclusiveRoomId = "R8",
            Segments = new List<CorridorSeg>
            {
                Seg(10, -2, 10, -13), Seg(10, -8, 7, -8), Seg(10, -5, 13, -5),
                Seg(13, -5, 13, -11), Seg(13, -11, 10, -11), Seg(10, -13, 8, -13),
            },
        };
    }
}
```

- [ ] **Step 2: 컴파일 → 메뉴 실행** — refresh + focus-unity → CS 에러 0 → `u_editor_menu`: `Tools/CTC/Build Map Candidate Pool` → 콘솔 "후보풀 저장" 로그
- [ ] **Step 3: 에셋 확인** — `u_editor_asset`로 `Assets/Prefabs/Maps/MapCandidatePool.asset` 존재 확인
- [ ] **Step 4: Commit** — 에셋 포함:

```bash
git add -A CTC/Assets/Scripts/Editor/MapCandidatePoolBuilder.cs CTC/Assets/Prefabs/Maps
git commit -m "feat: candidate pool asset builder (level-design spec + footprint fixes)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: 맵 생성 에디터 창 + 시안 등록

**Files:**
- Create: `Assets/Scripts/Editor/MapGeneratorWindow.cs`
- Create(툴): `Assets/Prefabs/Maps/Layout_Sketch01.asset`, 생성 맵들

- [ ] **Step 1: MapGeneratorWindow.cs 작성** — 시드 입력·생성·검증·저장·씬 프리뷰 없이 좌표 로그:

```csharp
using System.Collections.Generic;
using System.Linq;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using Pool = CTC.Runtime.Map.MapCandidatePool;

namespace CTC.Editor
{
    /// <summary>
    /// 후보풀에서 시드로 MapLayout을 굽는 에디터 창. "Randomize"로 시드를 굴려
    /// 유효 맵이 나올 때까지 생성 → 마음에 들면 "Save as Asset".
    /// Tools > CTC > Generate Map Layout
    /// </summary>
    public class MapGeneratorWindow : EditorWindow
    {
        private const string PoolPath = "Assets/Prefabs/Maps/MapCandidatePool.asset";
        private Pool m_Pool;
        private int m_Seed = 1;
        private MapGenerator.LayoutResult m_Preview;
        private string m_Status = "";

        [MenuItem("Tools/CTC/Generate Map Layout")]
        public static void Open() => GetWindow<MapGeneratorWindow>("Map Generator");

        private void OnEnable() => m_Pool = AssetDatabase.LoadAssetAtPath<Pool>(PoolPath);

        private void OnGUI()
        {
            m_Pool = (Pool)EditorGUILayout.ObjectField("Candidate Pool", m_Pool, typeof(Pool), false);
            m_Seed = EditorGUILayout.IntField("Seed", m_Seed);

            using (new EditorGUI.DisabledScope(m_Pool == null))
            {
                if (GUILayout.Button("Generate")) Generate(m_Seed);
                if (GUILayout.Button("Randomize (find valid)")) RandomizeValid();
                using (new EditorGUI.DisabledScope(m_Preview == null))
                    if (GUILayout.Button("Save as Asset")) Save();
            }

            EditorGUILayout.HelpBox(m_Status, MessageType.Info);
            if (m_Preview != null)
                EditorGUILayout.LabelField(
                    $"rooms {m_Preview.Rooms.Count} · corridors {m_Preview.Corridors.Count} · " +
                    $"cheese {m_Preview.Cheese.Count} · fortress {m_Preview.Fortresses.Count}");
        }

        private MapGenerator.PoolData ToDto(Pool p)
        {
            var rooms = p.Rooms.Select(r => new MapGenerator.RoomOption(
                r.Id, r.Center, r.Size, (MapGenerator.Tag)(int)r.Tag, r.AlwaysInclude)).ToList();
            var corr = p.Corridors.Select(c => new MapGenerator.CorridorOption(
                c.Id, c.RoomA, c.RoomB, c.Waypoints.ToArray(), c.Width)).ToList();
            var cheese = p.CheeseSlots.Select(c => new MapGenerator.CheeseSlotOption(
                c.RoomId, c.Pos, c.MinGrade, c.MaxGrade, c.Fixed, c.FixedGrade, c.Radius)).ToList();
            var fort = p.FortressSlots.Select(f => new MapGenerator.FortressSlotOption(
                f.RoomId, f.Pos, f.YawDeg, f.AlwaysInclude)).ToList();
            return new MapGenerator.PoolData(rooms, corr, new List<MazePreset>(p.Mazes), cheese, fort);
        }

        private void Generate(int seed)
        {
            m_Preview = MapGenerator.Generate(ToDto(m_Pool), seed);
            m_Status = m_Preview != null ? $"seed {seed} → 유효 맵" : $"seed {seed} → 실패(재시도 소진)";
        }

        private void RandomizeValid()
        {
            for (int i = 0; i < 500; i++)
            {
                int s = Random.Range(1, 1_000_000);
                var r = MapGenerator.Generate(ToDto(m_Pool), s);
                if (r != null) { m_Seed = s; m_Preview = r; m_Status = $"seed {s} → 유효 맵"; return; }
            }
            m_Status = "500회 내 유효 맵 실패 — 풀 점검 필요";
        }

        private void Save()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Maps"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Maps");
            string path = $"Assets/Prefabs/Maps/Layout_Seed{m_Preview.Seed}.asset";
            var layout = ScriptableObject.CreateInstance<MapLayout>();
            layout.Set(m_Preview.Seed, $"seed:{m_Preview.Seed}", m_Preview.Rooms, m_Preview.Corridors,
                m_Preview.Cheese, m_Preview.Fortresses);
            AssetDatabase.CreateAsset(layout, path);
            AssetDatabase.SaveAssets();
            m_Status = $"저장 — {path}";
        }
    }
}
```

- [ ] **Step 2: 컴파일 → 창 실행 → 유효 맵 저장** — refresh + focus-unity → CS 에러 0 → `Tools/CTC/Generate Map Layout` → Randomize → Save (Layout_SeedNNNN.asset 1개 생성)
- [ ] **Step 3: 시안 = 손작업 MapLayout 1호 작성** — `MapCandidatePoolBuilder`에 시안 등록 메뉴 추가(부록 조합: R1·R2·R3·R4·R5 + 미로A·B, 치즈 §4 규칙, 요새 F0·F5·F3·F1). `Tools/CTC/Build Sketch01 Layout` → `Layout_Sketch01.asset`(source="hand-authored") 저장. 이 조합이 `MapValidator` 전항 통과하는지 저장 전 assert
- [ ] **Step 4: Commit** — `feat: map generator window + hand-authored sketch layout`

---

### Task 6: 씬 굽기 — SetupNetworkingScene이 MapLayout을 읽어 생성 (Plan 10 머지 후)

**Files:**
- Modify: `Assets/Scripts/Editor/SetupNetworkingScene.cs`

- [ ] **Step 1: 선택 MapLayout 상수 추가** — `GameScenePath` 아래:

```csharp
        private const string ActiveLayoutPath = "Assets/Prefabs/Maps/Layout_Sketch01.asset";
```

- [ ] **Step 2: BuildGreyboxMap·BuildCheeseSpots·CHEESE_SPOT_DEFS 제거, BuildFromLayout 신설** — Plan 10의 `BuildGreyboxMap`(v1 부엌)과 `CHEESE_SPOT_DEFS`를 삭제하고:

```csharp
        // MapLayout(생성 툴 산출) 하나를 읽어 방 바닥·벽, 통로 바닥, 치즈 지점을 굽는다.
        // 요새는 런타임(LobbyManager)이 layout.Fortresses로 스폰 — 여기선 지오메트리만.
        private static MapLayout LoadActiveLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(ActiveLayoutPath);
            if (layout == null) Debug.LogError($"[SetupNetworkingScene] MapLayout 없음: {ActiveLayoutPath} — 먼저 Generate Map Layout");
            return layout;
        }

        private const float ROOM_WALL_HEIGHT = 3f;
        private const float ROOM_WALL_THICK = 0.5f;
        private const float CORRIDOR_WALL_HEIGHT = 1.6f;

        private static void BuildFromLayout(MapLayout layout)
        {
            if (layout == null) return;
            Material wall = GridboxMaterial("Grey2");
            Material floorMat = GridboxMaterial("Grey4");
            var root = new GameObject("GeneratedMap");

            // 방: 4벽(입출구는 통로가 겹쳐 뚫는 방식이 아니라, 방 벽은 통로 접점만 개방)
            foreach (RoomRect r in layout.Rooms)
                BuildRoomWalls(root, wall, floorMat, r, layout.Corridors);

            // 통로: 중심선 세그먼트마다 폭만큼 바닥 + 양측 낮은 벽
            foreach (CorridorSeg c in layout.Corridors)
                BuildCorridor(root, wall, floorMat, c);
        }
```

`BuildRoomWalls`·`BuildCorridor`는 세그먼트/사각형을 `AddBox`로 까는 헬퍼(방 경계 4면, 통로 접점 좌표엔 개구부). 통로 접점 판정은 `MapValidator.PointNearRoom` 재사용 대신 각 벽 세그먼트가 통로 끝점과 겹치면 스킵:

```csharp
        private static void BuildRoomWalls(
            GameObject root, Material wall, Material floorMat, RoomRect r, IReadOnlyList<CorridorSeg> corridors)
        {
            // 바닥
            AddBox(root, floorMat, $"Floor_{r.Id}",
                new Vector3(r.Center.x, 0.02f, r.Center.y), new Vector3(r.Size.x, 0.04f, r.Size.y));

            // 통로가 접하는 방 변엔 개구부 — 각 변을 통로 접점 기준으로 분할해 세그먼트로 깐다.
            // 접점: 통로 끝점이 이 방 경계(±0.6)에 있는 것들의 해당 축 좌표.
            BuildWallWithGaps(root, wall, r, corridors, side: 0); // N (+z)
            BuildWallWithGaps(root, wall, r, corridors, side: 1); // S (-z)
            BuildWallWithGaps(root, wall, r, corridors, side: 2); // E (+x)
            BuildWallWithGaps(root, wall, r, corridors, side: 3); // W (-x)
        }
```

> **구현 주의:** `BuildWallWithGaps`는 Plan 10 `BuildTunnelWall`의 "구멍 뚫린 세그먼트" 로직과 동형 — 변의 고정축 좌표에서 통로 폭(1.6)만큼 개구부를 남기고 나머지를 벽 세그먼트로. 통로 접점 좌표는 `corridors`에서 `From`/`To`가 해당 변 근처(±0.6)인 것을 모아 산출. `BuildCorridor`는 세그먼트를 방향에 맞춰 폭 1.6 바닥 박스 1개로 (벽은 던전 외곽만 필요하면 생략 가능 — 그레이박스 단계는 바닥만으로 이동 제약 성립하지 않으므로, 통로 양측에 CORRIDOR_WALL_HEIGHT 벽 2개 추가).

- [ ] **Step 3: 치즈 지점을 layout에서** — `BuildCheeseSpots`를 layout 기반으로 교체:

```csharp
        private static void BuildCheeseSpots(MapLayout layout)
        {
            var root = new GameObject("CheeseSpots");
            foreach (CheesePlacement cp in layout.Cheese)
            {
                var spotGo = new GameObject($"CheeseSpot_G{cp.Grade}");
                spotGo.transform.SetParent(root.transform, false);
                spotGo.transform.localPosition = new Vector3(cp.Pos.x, 0f, cp.Pos.y);
                var spot = spotGo.AddComponent<CheeseSpot>();
                SetInt(spot, "m_Grade", cp.Grade);
                SetFloat(spot, "m_Radius", cp.Radius);
                // (디스크·라벨 생성은 기존 코드 재사용 — cp.Grade로 GradeDiscMaterial)
                AddCheeseVisual(spotGo, cp.Grade);
            }
        }
```

(기존 `BuildCheeseSpots`의 디스크·TMP 라벨 부분을 `AddCheeseVisual(GameObject, int)` 헬퍼로 추출해 재사용.)

- [ ] **Step 4: CreateGameScene 배선 교체** — `BuildGreyboxMap(); BuildCheeseSpots();` →

```csharp
            MapLayout layout = LoadActiveLayout();
            BuildFromLayout(layout);
            BuildCheeseSpots(layout);
            SetRef(lobby, "m_ActiveLayout", layout);   // Task 7에서 필드 추가
```

Plan 10의 `CreateFortressPrefab`·요새 참조 배선은 유지(요새 프리팹은 런타임 스폰).

- [ ] **Step 5: 컴파일 → 씬 재생성 → 육안** — refresh + focus-unity → CS 에러 0 → `Tools/CTC/Setup Networking Scene` → `u_screenshot`(SceneView top-down)로 방-통로 구조 확인. 고아 방·끊긴 통로 없어야 함
- [ ] **Step 6: Commit** — 씬 포함: `feat: bake scene geometry from MapLayout (rooms/corridors/cheese)`

---

### Task 7: 런타임 데이터화 — LobbyManager가 MapLayout에서 요새 스폰 (Plan 10 머지 후)

**Files:**
- Modify: `Assets/Scripts/Lobby/LobbyManager.cs`, `Assets/Scripts/Tests/EditMode/LobbyRoleLogicTests.cs`

- [ ] **Step 1: 테스트 갱신** — Plan 10의 `SpawnPosition(roles, index)`가 정적 `FORTRESS_SPOTS`를 쓰던 것을, layout 주입형으로. `LobbyRoleLogicTests`의 `SpawnPosition_Mouse_AtOwnFortressEntrance`를 layout 인자 버전으로 교체:

```csharp
        [Test]
        public void SpawnPosition_Mouse_AtOwnFortressEntrance()
        {
            var roles = new List<RoleId> { RoleId.Cat, RoleId.Mouse, RoleId.Mouse };
            var forts = new List<(Vector3 pos, float yaw)>
            {
                (new Vector3(-11f, 0f, -12f), 0f),
                (new Vector3(9f, 0f, -12.5f), 0f),
            };
            AssertPos(new Vector3(-11f, 0.1f, -10.2f), LobbyManager.SpawnPosition(roles, 1, forts));
            AssertPos(new Vector3(9f, 0.1f, -10.7f), LobbyManager.SpawnPosition(roles, 2, forts));
        }
```

(`SpawnPosition_Cat_CenterLine`은 forts 무관 — 빈 리스트 전달)

- [ ] **Step 2: EditMode → 컴파일 실패 확인** — 3인자 오버로드 없음

- [ ] **Step 3: LobbyManager 구현** — `FORTRESS_SPOTS` 정적 배열 삭제, `MapLayout` 주입:

```csharp
        [SerializeField] private CTC.Runtime.Map.MapLayout m_ActiveLayout;

        public const float FORTRESS_ENTRANCE_OFFSET_METERS = 1.8f;

        // layout의 요새자리를 (pos, yaw) 리스트로 — 없으면 빈 리스트(고양이 전용 테스트 등)
        private List<(Vector3 pos, float yaw)> FortressSpots()
        {
            var list = new List<(Vector3, float)>();
            if (m_ActiveLayout == null) return list;
            foreach (var f in m_ActiveLayout.Fortresses)
                list.Add((new Vector3(f.Pos.x, 0f, f.Pos.y), f.YawDeg));
            return list;
        }

        public static Vector3 SpawnPosition(
            IReadOnlyList<RoleId> roles, int index, IReadOnlyList<(Vector3 pos, float yaw)> fortresses)
        {
            int nth = 0;
            for (int i = 0; i < index; i++)
                if (roles[i] == roles[index]) nth++;

            if (roles[index] == RoleId.Cat) return new Vector3(nth * 2f, 0.1f, 0f);

            if (fortresses.Count == 0) return new Vector3(0f, 0.1f, 0f); // 방어 — layout 미배선
            (Vector3 pos, float yaw) = fortresses[nth % fortresses.Count];
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 spawn = pos + dir * FORTRESS_ENTRANCE_OFFSET_METERS;
            spawn.y = 0.1f;
            return spawn;
        }

        public Vector3 SpawnPositionOf(int index) => SpawnPosition(SnapshotRoles(), index, FortressSpots());
```

`SpawnPlayers()`에서 초기 요새 스폰(Plan 10의 `SpawnInitialFortress`)은 layout 자리를 쓰게:

```csharp
        private void SpawnPlayers()
        {
            List<RoleId> roles = SnapshotRoles();
            var forts = FortressSpots();
            for (int i = 0; i < _players.Count; i++)
            {
                LobbyPlayerState p = _players[i];
                GameObject prefab = p.Role == RoleId.Cat ? catPrefab : mousePrefab;
                Vector3 pos = SpawnPosition(roles, i, forts);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId);
                if (p.Role == RoleId.Mouse)
                    SpawnInitialFortress(MouseOrderOf(roles, i), forts);
            }
        }

        private void SpawnInitialFortress(int mouseOrder, List<(Vector3 pos, float yaw)> forts)
        {
            if (fortressPrefab == null || forts.Count == 0) return;
            (Vector3 pos, float yaw) = forts[mouseOrder % forts.Count];
            GameObject go = Instantiate(fortressPrefab, pos, Quaternion.Euler(0f, yaw, 0f));
            go.GetComponent<NetworkObject>().Spawn(true);
        }
```

- [ ] **Step 4: PlayerMovement 소유자 텔레포트** — Plan 10에서 `lobby.SpawnPositionOf(idx)`를 이미 쓰면 무변경. 정적 `SpawnPosition(idx, count)` 잔재가 있으면 `lobby.SpawnPositionOf(idx)`로 교체
- [ ] **Step 5: 컴파일 → EditMode** — Expected: **Passed 95** (Task 3 이후 유지 — 스폰 테스트는 형태만 바뀜). layout 미배선 씬은 방어분기로 스폰 (0,0.1,0)
- [ ] **Step 6: Commit** — `feat: LobbyManager spawns fortresses from injected MapLayout`

---

### Task 8: MPPM 2인 검증 (메인 세션 직접)

사전: `Tools/CTC/Setup Networking Scene` 재실행(Sketch01 layout 배선). MPPM Player 2 활성.

| # | 시나리오 | 방법 | 기대 |
|---|---|---|---|
| 1 | 접속·시작 | 표준 절차 → `mppm_collect` | 정상 시작, 타이머 감소 |
| 2 | 방-통로 지오메트리 | `u_screenshot` top-down | 방 5개·통로·미로 렌더, 고아/끊김 0 |
| 3 | 요새 스폰 위치 | `mppm_collect` objects | CheeseFortress N개 == layout.Fortresses, 쥐0 요새 ≈ (-11,-12) |
| 4 | 쥐 스폰 = 요새 앞 | 클론 쥐 pos | ≈ (-11, -10.2) |
| 5 | 고양이 통로 통과 | 호스트 고양이 `TestMpNudge`류로 통로 이동 | 끼임 0, 방↔방 이동 성공 |
| 6 | G1 수입 | 클론 넛지 → 10s | 클론 쥐 cheese 증가 |
| 7 | 치즈 등급 완전성 | `mppm_collect` | G1~G7 전부 씬에 존재 |
| 8 | 회귀 | EditMode 95 · 재시작 후 요새 despawn · 포트 잔존 0 | 전부 통과 |

**판정:** 기술 문제 자동 수정 후 재시도. 기획적 진행 불가만 보고 중단.

## 검증 요약 (완료 기준)

EditMode **95** (Validator 9 + Generator 5, 스폰 테스트 형태 변경) · 후보풀 에셋 · 생성 툴로 유효 맵 저장 가능 · 시안 = Layout_Sketch01 · 씬이 layout에서 방-통로-치즈 굽기 · 런타임 요새 스폰이 layout 참조 · 100시드 통과율 ≥90% · MPPM 방-통로 이동/스폰 정상

## Self-Review 반영

- **스펙 커버리지:** 생성 스펙 §3(후보풀+시드) → Task 1·3·4·5 / §검증 → Task 2 / 씬 굽기 → Task 6 / 런타임 데이터화 → Task 7 / 시안 1호 → Task 5. 후보풀 명세 §1~6 → Task 4(데이터)·Task 2(검증). 전항 커버.
- **Plan 10 의존:** Task 6·7은 Plan 10의 `FORTRESS_SPOTS`·`CreateFortressPrefab`·`SpawnInitialFortress`를 전제 — 미완이면 Task 1~5 선행.
- **타입 일관성:** `MapLayout.Fortresses`(`FortressPlacement`) ↔ `LobbyManager.FortressSpots()`(`(Vector3,float)`) 변환 명시. `MapGenerator.LayoutResult`(순수) ↔ `MapLayout`(SO) `Set()`으로 이관.
- **미해결(구현 중 확정):** `BuildWallWithGaps` 개구부 분할 로직은 Plan 10 `BuildTunnelWall` 재사용 — 정확한 접점 분할은 Task 6에서 실측 조정. 100시드 통과율이 낮으면 후보풀 치즈 밴드 재조정.
