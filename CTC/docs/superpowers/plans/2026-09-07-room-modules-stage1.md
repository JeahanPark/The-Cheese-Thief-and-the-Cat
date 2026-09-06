# 방 모듈 1단계 구현 계획 (2026-09-07)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 대형 맵의 방을 절차 큐브 대신 **손조립 모듈 프리팹**으로 세우고, BotLab 화면으로 "방으로 보이나"를 판정한다.

**Architecture:** 레이아웃 두뇌(`MapGenerator`·`Layout_Large01`·격자 A\*)는 그대로. `RoomRect.ModuleId`가 있으면 `MapBuilder`가 큐브 벽 대신 카탈로그의 모듈 프리팹을 방 중심에 놓고, 4변의 `DoorSocket`에 복도 연결 여부로 `DoorFrame`/`WallPlug`를 꽂는다. 모듈 안 큰 가구는 `Obstacle_*` 실물 BoxCollider라 `NavGridBaker`가 이름으로 자동 스탬프한다. 모듈 프리팹은 에디터 메뉴가 굽는다(셸 + v1 가구 좌표는 코드가 "손"이다) — 이미 있으면 덮어쓰지 않는다(함정 2).

**Tech Stack:** Unity 6000.5 / URP / EditMode NUnit / 기존 `MapAssets`·`SceneComposer` 패턴

**스펙:** `docs/superpowers/specs/2026-09-07-room-modules-design.md` (1단계만)

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Map/RoomModuleLogic.cs` (신규) | 순수: 문 변 판정 · 발자국 키 · 모듈 선택(시드) |
| `Assets/Scripts/Map/RoomModuleCatalog.cs` (신규) | SO: `{ModuleId, Prefab, Footprint, Role}[]` + `DoorFrame`·`WallPlug` |
| `Assets/Scripts/Map/RoomModuleSocket.cs` (신규) | 마커 컴포넌트: 소켓 종류 enum (2단계가 읽는다 — 1단계는 Door만 쓴다) |
| `Assets/Scripts/Map/MapData.cs` | `RoomRect.ModuleId` |
| `Assets/Scripts/Map/MapCandidatePool.cs` | `RoomCandidate.ModuleId` |
| `Assets/Scripts/Map/MapGenerator.cs` | `RoomOption.ModuleId` → `RoomRect` 전파 |
| `Assets/Scripts/Map/MapBuilder.cs` | `m_ModuleCatalog` · `BuildRoomModule` · 모듈 방 안 장애물 생략 |
| `Assets/Scripts/Editor/MapGeneratorWindow.cs` | `ToDto`에 `ModuleId` |
| `Assets/Scripts/Editor/LargeMapPoolBuilder.cs` | 지터 0 · 발자국 2종 · `ModuleId` 배정 |
| `Assets/Scripts/Editor/Setup/MapAssets.cs` | 모듈·카탈로그 경로 상수 |
| `Assets/Scripts/Editor/Setup/RoomModuleBuilder.cs` (신규) | 메뉴 `Tools/CTC/Map/Build Room Modules v1` — DoorFrame·WallPlug·모듈 2종·카탈로그 |
| `Assets/Scripts/Editor/Setup/SceneComposer.cs` | `MapBuilder.m_ModuleCatalog` 배선 |
| `Assets/Scripts/Tests/EditMode/RoomModuleLogicTests.cs` (신규) | 순수 로직 테스트 |
| `docs/design-vault/코드 다이어그램.md` | 맵 아트 절에 모듈 파이프라인 |

---

### Task 1: RoomModuleLogic (순수) — TDD

**Files:**
- Create: `Assets/Scripts/Map/RoomModuleLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/RoomModuleLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트**

```csharp
using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class RoomModuleLogicTests
    {
        private static RoomRect Room(float _cx, float _cy, float _w, float _h) =>
            new RoomRect { Id = "R", Center = new Vector2(_cx, _cy), Size = new Vector2(_w, _h) };

        private static CorridorSeg Seg(float _x1, float _y1, float _x2, float _y2, float _w = 3f) =>
            new CorridorSeg { From = new Vector2(_x1, _y1), To = new Vector2(_x2, _y2), Width = _w };

        /// <summary>변 한가운데에 복도 끝점이 닿으면 그 변이 문이다. 순서 N·S·E·W.</summary>
        [Test]
        public void DoorSides_MarksSidesTouchedByCorridorEnds()
        {
            RoomRect room = Room(0f, 0f, 12f, 11f);
            var corridors = new List<CorridorSeg>
            {
                Seg(6f, 0f, 11f, 0f),      // E 변에서 나감
                Seg(0f, -5.5f, 0f, -12f),  // S 변에서 나감
            };
            bool[] sides = RoomModuleLogic.DoorSides(room, corridors);
            CollectionAssert.AreEqual(new[] { false, true, true, false }, sides);
        }

        [Test]
        public void DoorSides_IgnoresCorridorsOfOtherRooms()
        {
            RoomRect room = Room(0f, 0f, 12f, 11f);
            var corridors = new List<CorridorSeg> { Seg(30f, 0f, 40f, 0f) };
            CollectionAssert.AreEqual(new[] { false, false, false, false }, RoomModuleLogic.DoorSides(room, corridors));
        }

        [Test]
        public void FootprintKey_BigAndSmall()
        {
            Assert.AreEqual(RoomModuleLogic.FOOTPRINT_BIG, RoomModuleLogic.FootprintKey(new Vector2(12f, 11f)));
            Assert.AreEqual(RoomModuleLogic.FOOTPRINT_SMALL, RoomModuleLogic.FootprintKey(new Vector2(10f, 9f)));
            Assert.AreEqual("", RoomModuleLogic.FootprintKey(new Vector2(7f, 7f)), "모르는 크기는 빈 키 — 큐브로 후퇴");
        }

        [Test]
        public void PickModule_DeterministicAndInRange()
        {
            var ids = new List<string> { "A", "B", "C" };
            string first = RoomModuleLogic.PickModule(ids, 5, 777);
            Assert.AreEqual(first, RoomModuleLogic.PickModule(ids, 5, 777));
            CollectionAssert.Contains(ids, first);
            Assert.AreEqual("", RoomModuleLogic.PickModule(new List<string>(), 5, 777));
        }

        [Test]
        public void SideYaw_FacesOutward()
        {
            Assert.AreEqual(0f, RoomModuleLogic.SideYaw(0));     // N → +z
            Assert.AreEqual(180f, RoomModuleLogic.SideYaw(1));   // S
            Assert.AreEqual(90f, RoomModuleLogic.SideYaw(2));    // E → +x
            Assert.AreEqual(270f, RoomModuleLogic.SideYaw(3));   // W
        }
    }
}
```

- [ ] **Step 2: `u_editor_asset refresh` → 콘솔에 `RoomModuleLogic` 없음 컴파일 에러 확인**

- [ ] **Step 3: 구현**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 방 모듈 조립의 순수 규칙(2026-09-07, EditMode TDD). 문 변 판정은
    /// <c>MapBuilder.BuildWallWithGaps</c>·<c>ObstacleFactory.PlanDoorScreens</c>와 **같은 기준**이다
    /// (끝점이 변에 닿는 복도) — 어긋나면 문이 없는 변에 문틀이 선다.
    /// </summary>
    public static class RoomModuleLogic
    {
        public const string FOOTPRINT_BIG = "Big";      // 12×11
        public const string FOOTPRINT_SMALL = "Small";  // 10×9

        public static readonly Vector2 BIG_SIZE = new Vector2(12f, 11f);
        public static readonly Vector2 SMALL_SIZE = new Vector2(10f, 9f);

        /// <summary>변 순서 0=N(+z) 1=S(-z) 2=E(+x) 3=W(-x) — MapBuilder와 같다.</summary>
        public static bool[] DoorSides(RoomRect _room, IReadOnlyList<CorridorSeg> _corridors)
        {
            var sides = new bool[4];
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side <= 1;
                float fixedCoord = side == 0 ? _room.Max.y : side == 1 ? _room.Min.y : side == 2 ? _room.Max.x : _room.Min.x;
                float min = horizontal ? _room.Min.x : _room.Min.y;
                float max = horizontal ? _room.Max.x : _room.Max.y;

                foreach (CorridorSeg c in _corridors)
                {
                    foreach (Vector2 p in new[] { c.From, c.To })
                    {
                        float perp = horizontal ? p.y : p.x;
                        float along = horizontal ? p.x : p.y;
                        if (Mathf.Abs(perp - fixedCoord) > c.Width * 0.5f + 0.6f) continue;
                        if (along < min - 0.5f || along > max + 0.5f) continue;
                        sides[side] = true;
                    }
                }
            }
            return sides;
        }

        /// <summary>크기 → 발자국 키. 모르는 크기는 빈 문자열(큐브 경로).</summary>
        public static string FootprintKey(Vector2 _size)
        {
            if (Approximately(_size, BIG_SIZE)) return FOOTPRINT_BIG;
            if (Approximately(_size, SMALL_SIZE)) return FOOTPRINT_SMALL;
            return "";
        }

        /// <summary>같은 발자국 후보 중 하나 — 시드·방 번호로 결정적.</summary>
        public static string PickModule(IReadOnlyList<string> _candidates, int _roomIndex, int _seed)
        {
            if (_candidates == null || _candidates.Count == 0) return "";
            int pick = (int)(PantryTextureLogic.Hash(_roomIndex, 23, _seed) * _candidates.Count) % _candidates.Count;
            return _candidates[pick];
        }

        /// <summary>변 번호 → 바깥을 보는 yaw(도). 문틀·막벽을 그 방향으로 세운다.</summary>
        public static float SideYaw(int _side) => _side == 0 ? 0f : _side == 1 ? 180f : _side == 2 ? 90f : 270f;

        private static bool Approximately(Vector2 _a, Vector2 _b) =>
            Mathf.Abs(_a.x - _b.x) < 0.01f && Mathf.Abs(_a.y - _b.y) < 0.01f;
    }
}
```

- [ ] **Step 4: refresh → `Tools/CTC/Run EditMode Tests` → 404 + 5 = 409 통과**

---

### Task 2: 자료구조 — `ModuleId` 전파

**Files:**
- Modify: `Assets/Scripts/Map/MapData.cs` (`RoomRect`)
- Modify: `Assets/Scripts/Map/MapCandidatePool.cs` (`RoomCandidate`)
- Modify: `Assets/Scripts/Map/MapGenerator.cs` (`RoomOption` 생성자·`TryBuild`)
- Modify: `Assets/Scripts/Editor/MapGeneratorWindow.cs` (`ToDto`)

- [ ] **Step 1: `RoomRect`에 `public string ModuleId;` 추가** (Size 아래, 주석: "비면 큐브 벽(옛 경로). 2026-09-07")
- [ ] **Step 2: `RoomCandidate`에 `public string ModuleId;` 추가**
- [ ] **Step 3: `RoomOption`에 `public readonly string ModuleId;` + 생성자 마지막 인자 `string moduleId = ""`**
- [ ] **Step 4: `TryBuild`의 `new RoomRect { ... }`에 `ModuleId = r.ModuleId` 추가**
- [ ] **Step 5: `ToDto`의 `new MapGenerator.RoomOption(...)`에 `r.ModuleId` 전달**
- [ ] **Step 6: refresh → 컴파일 에러 0 · EditMode 409 유지** (다른 풀 빌더는 필드를 안 채워 `null`/빈 문자열 → 큐브 경로)

---

### Task 3: 카탈로그 SO + 소켓 마커

**Files:**
- Create: `Assets/Scripts/Map/RoomModuleCatalog.cs`
- Create: `Assets/Scripts/Map/RoomModuleSocket.cs`

- [ ] **Step 1: 소켓 마커**

```csharp
using UnityEngine;

namespace CTC.Runtime.Map
{
    public enum RoomModuleSocketKind { Door, Cheese, Alcove, Box, Fortress, Light }

    /// <summary>모듈 프리팹 안의 자리 표식. 1단계는 Door만 쓴다 — 나머지는 2단계(자리 배선)가 읽는다.</summary>
    public class RoomModuleSocket : MonoBehaviour
    {
        [SerializeField] private RoomModuleSocketKind m_Kind;
        /// <summary>Door: 0=N 1=S 2=E 3=W. 다른 종류는 0부터 번호.</summary>
        [SerializeField] private int m_Index;

        public RoomModuleSocketKind Kind => m_Kind;
        public int Index => m_Index;

        public void Set(RoomModuleSocketKind _kind, int _index) { m_Kind = _kind; m_Index = _index; }
    }
}
```

- [ ] **Step 2: 카탈로그**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    public enum RoomModuleRole { Normal, Fortress, Center, Landmark }

    /// <summary>방 모듈 목록(2026-09-07). 에디터 메뉴가 굽고 SceneComposer가 MapBuilder에 배선한다.</summary>
    [CreateAssetMenu(fileName = "RoomModuleCatalog", menuName = "CTC/Room Module Catalog")]
    public class RoomModuleCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string ModuleId;
            public GameObject Prefab;
            public string Footprint;      // RoomModuleLogic.FOOTPRINT_*
            public RoomModuleRole Role;
        }

        [SerializeField] private List<Entry> m_Entries = new List<Entry>();
        [SerializeField] private GameObject m_DoorFrame;
        [SerializeField] private GameObject m_WallPlug;

        public IReadOnlyList<Entry> Entries => m_Entries;
        public GameObject DoorFrame => m_DoorFrame;
        public GameObject WallPlug => m_WallPlug;

        public bool TryGet(string _moduleId, out GameObject _prefab)
        {
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_Entries[i].ModuleId == _moduleId && m_Entries[i].Prefab != null)
                {
                    _prefab = m_Entries[i].Prefab;
                    return true;
                }
            }
            _prefab = null;
            return false;
        }

        /// <summary>발자국·역할이 맞는 모듈 id 목록 — 풀 빌더가 시드로 하나 고른다.</summary>
        public List<string> IdsFor(string _footprint, RoomModuleRole _role)
        {
            var ids = new List<string>();
            foreach (Entry e in m_Entries)
                if (e.Footprint == _footprint && e.Role == _role) ids.Add(e.ModuleId);
            return ids;
        }

        public void Set(List<Entry> _entries, GameObject _doorFrame, GameObject _wallPlug)
        {
            m_Entries = _entries;
            m_DoorFrame = _doorFrame;
            m_WallPlug = _wallPlug;
        }
    }
}
```

- [ ] **Step 3: refresh → 컴파일 0**

---

### Task 4: MapBuilder — 모듈 경로

**Files:**
- Modify: `Assets/Scripts/Map/MapBuilder.cs`

- [ ] **Step 1: 필드** — `m_CheeseVisualMaterial` 아래:

```csharp
        // 방 모듈 카탈로그(2026-09-07). 없거나 RoomRect.ModuleId가 비면 큐브 벽(옛 경로).
        [SerializeField] private RoomModuleCatalog m_ModuleCatalog;
```

- [ ] **Step 2: `BuildFromLayout`의 방 루프 교체**

```csharp
            foreach (RoomRect r in _layout.Rooms)
            {
                if (!BuildRoomModule(r, _layout.Corridors)) BuildRoomWalls(r, _layout.Corridors);
            }
```

- [ ] **Step 3: `BuildRoomModule` 추가** (`BuildRoomWalls` 위)

```csharp
        /// <summary>
        /// 모듈 프리팹으로 방을 세운다(2026-09-07). 카탈로그에 없으면 false — 큐브로 후퇴.
        /// 4변의 DoorSocket에 복도 연결 여부로 문틀/막벽을 꽂는다. 모듈 안 `Wall_*`·`Obstacle_*`
        /// 박스는 `NavGridBaker`가 이름으로 스탬프하므로 여기서 격자를 따로 손대지 않는다.
        /// </summary>
        private bool BuildRoomModule(RoomRect _r, IReadOnlyList<CorridorSeg> _corridors)
        {
            if (m_ModuleCatalog == null || string.IsNullOrEmpty(_r.ModuleId)) return false;
            if (!m_ModuleCatalog.TryGet(_r.ModuleId, out GameObject prefab)) return false;

            GameObject module = Instantiate(prefab, new Vector3(_r.Center.x, 0f, _r.Center.y), Quaternion.identity, m_Root.transform);
            module.name = $"Module_{_r.Id}_{_r.ModuleId}";

            bool[] doors = RoomModuleLogic.DoorSides(_r, _corridors);
            foreach (RoomModuleSocket socket in module.GetComponentsInChildren<RoomModuleSocket>(true))
            {
                if (socket.Kind != RoomModuleSocketKind.Door) continue;
                int side = socket.Index;
                if (side < 0 || side > 3) continue;

                GameObject piece = doors[side] ? m_ModuleCatalog.DoorFrame : m_ModuleCatalog.WallPlug;
                if (piece == null) continue;
                GameObject go = Instantiate(piece, socket.transform.position, socket.transform.rotation, module.transform);
                go.name = (doors[side] ? "DoorFrame_" : "Wall_Plug_") + _r.Id + "_" + side;
            }
            return true;
        }
```

- [ ] **Step 4: `BuildObstacles` — 모듈 방 안 장애물 생략** (루프 첫 줄에):

```csharp
                if (InsideModuleRoom(_layout, o.Pos)) continue;   // 모듈이 제 가구로 시야를 끊는다
```
와 헬퍼:
```csharp
        private static bool InsideModuleRoom(MapLayout _layout, Vector2 _pos)
        {
            foreach (RoomRect r in _layout.Rooms)
                if (!string.IsNullOrEmpty(r.ModuleId) && r.Contains(_pos)) return true;
            return false;
        }
```
(`BuildObstacles` 루프 변수명은 그 파일에서 확인해 맞춘다.)

- [ ] **Step 5: refresh → 컴파일 0**

---

### Task 5: 에디터 — 모듈 프리팹 빌더

**Files:**
- Modify: `Assets/Scripts/Editor/Setup/MapAssets.cs`
- Create: `Assets/Scripts/Editor/Setup/RoomModuleBuilder.cs`

- [ ] **Step 1: 경로 상수** (`MapAssets`):

```csharp
        // ── 방 모듈(2026-09-07) ──
        public const string MODULE_DIR = "Assets/Prefabs/Maps/Modules";
        public const string MODULE_CATALOG = "Assets/Prefabs/Maps/RoomModuleCatalog.asset";
        public const string DOOR_FRAME_PREFAB = MODULE_DIR + "/DoorFrame.prefab";
        public const string WALL_PLUG_PREFAB = MODULE_DIR + "/WallPlug.prefab";
        public const string SACK_PREFAB = PACK + "/Farm/Crops Farm/pumpkin-b.prefab";   // 자루 대용(팩에 자루 없음)
```

- [ ] **Step 2: `RoomModuleBuilder`** — 메뉴 `Tools/CTC/Map/Build Room Modules v1`. 규칙:
  - 프리팹이 **이미 있으면 건너뛴다**(함정 2) — 손으로 고친 걸 지우지 않는다. 다시 굽고 싶으면 파일을 지운다.
  - 머티리얼은 `MapAssets.LoadMaterial`(없으면 경고 + 마젠타 방지 위해 중단, 함정 6).
  - 셸: 바닥 `Floor_Module`(layer 2, 격자 무시) · 변마다 벽 2조각 `Wall_{N|S|E|W}_{L|R}` (문 슬롯 폭 `DOOR_SLOT = 3.6f` 비움, 높이 5, 두께 0.5) · 모서리 기둥 `Pillar_*`(0.7×5×0.7, 콜라이더 없음 — 벽 안) · 상단 들보 `Rim_*`(콜라이더 없음, 높이 4.8~5.2)
  - 소켓: `Doors/DoorSocket_{N|S|E|W}` — 변 한가운데, 바깥을 본다(`RoomModuleLogic.SideYaw`) + `RoomModuleSocket(Door, side)`
  - 가구(v1, 좌표 = 손조립): 선반은 `Obstacle_Shelf_*` BoxCollider(실물, 렌더 AABB) + 트리거 박스(페이드) · 궤짝더미 `Obstacle_Crate_*` · 배럴 `Obstacle_Barrel_*` · 호박·견과는 트리거만. `MapDresser.StripPhysics`와 같은 방식으로 원본 콜라이더·리지드바디를 지운 뒤 박스를 새로 단다.
  - 모듈 2종: `Module_Big_Pantry01`(12×11) — 북·서 벽에 선반 2배(2m 단위 타일링, 문 슬롯 피함), 남동 구석 궤짝더미 3, 중앙 약간 남쪽에 배럴 2(시야 끊기), 호박 3 흩기 / `Module_Small_Cellar01`(10×9) — 동벽 선반, 북서 배럴 3, 궤짝 2, 견과 4.
  - `DoorFrame`: 기둥 2(0.4×3.2×0.5, 슬롯 양끝, 콜라이더 없음 — 벽이 이미 막는다) + 상인방(3.6×0.4×0.5, y 3.2, 콜라이더 없음) · `WallPlug`: `Wall_Plug` 박스(3.6×5×0.5, 실물) + 앞에 궤짝 1(트리거)
  - 카탈로그 에셋 `MapAssets.MODULE_CATALOG` 생성/갱신: Entries 2 + DoorFrame + WallPlug
  - 마지막에 `AssetDatabase.SaveAssets()` + 로그 "[RoomModules] 모듈 N · 문틀 · 막벽 · 카탈로그"

- [ ] **Step 3: refresh → 컴파일 0 → `u_editor_menu Tools/CTC/Map/Build Room Modules v1` → 콘솔 로그 확인 · `unity-scanner read Assets/Prefabs/Maps/Modules/Module_Big_Pantry01.prefab`로 소켓 4·Obstacle_ 박스 있음 확인**

---

### Task 6: 풀 빌더 — 지터 0 · 발자국 2종 · 모듈 배정

**Files:**
- Modify: `Assets/Scripts/Editor/LargeMapPoolBuilder.cs`
- Modify: `Assets/Scripts/Editor/Setup/SceneComposer.cs`

- [ ] **Step 1: 상수** — `JITTER = 0f` (주석: 모듈 문 소켓이 변 한가운데라 지터가 있으면 복도가 안 맞는다, 2026-09-07). `WIDTHS/HEIGHTS` 삭제 → 방 크기는 `RoomModuleLogic.BIG_SIZE`/`SMALL_SIZE` 중 시드로 택1 (요새방·중앙은 BIG).
- [ ] **Step 2: 모듈 배정** — `Build()` 시작에 카탈로그 로드 `AssetDatabase.LoadAssetAtPath<RoomModuleCatalog>(MapAssets.MODULE_CATALOG)` (없으면 경고 + `ModuleId = ""`). 방마다 `RoomModuleLogic.PickModule(catalog.IdsFor(RoomModuleLogic.FootprintKey(size), RoomModuleRole.Normal), index, BUILD_SEED)` → `RoomCandidate.ModuleId`. 1단계는 요새방·중앙도 `Normal` 후보를 쓴다(2단계에서 역할별로).
- [ ] **Step 3: `SceneComposer`** — `SetRef(mapBuilder, "m_CheeseVisualMaterial", ...)` 아래:

```csharp
            // 방 모듈(2026-09-07) — 없으면 큐브 벽 그대로
            SetRef(mapBuilder, "m_ModuleCatalog",
                AssetDatabase.LoadAssetAtPath<CTC.Runtime.Map.RoomModuleCatalog>(MapAssets.MODULE_CATALOG));
```

- [ ] **Step 4: refresh → 컴파일 0**

---

### Task 7: 굽기 순서 + 화면 판정

- [ ] **Step 1: `Tools/CTC/Map/Build Room Modules v1`** (이미 했으면 건너뜀)
- [ ] **Step 2: `Tools/CTC/Build Large Map Pool`** → 로그 "방 49"
- [ ] **Step 3: `Tools/CTC/Build Large Map Layout`** → 로그 "대형 맵 저장 (seed N)"
- [ ] **Step 4: `Tools/CTC/Setup Networking Scene`** (벽감·상자·장애물 재계획 + 카탈로그 배선) → 리임포트 끝난 뒤
- [ ] **Step 5: `Tools/CTC/Setup BotLab Scene (봇 전용 실험실)`**
- [ ] **Step 6: BotLab 플레이** (`m_MapIndex`가 대형=2인지 확인) → `u_screenshot` 부감 + `TestGetNavGridInfo` · `TestGetNavStats`(60초 후) — giveups 0 · 봇이 가구를 우회
- [ ] **Step 7: 인게임 시점 사진** — `Tools/CTC/Dev/맵 사진 (1920×1080)`(포토부스)로 4장 → `docs/store-assets/screenshots/`가 아니라 `%TEMP%\ctc-modules\`에 저장(판정용)
- [ ] **Step 8: EditMode 409 유지 · 콘솔 에러 0**

---

### Task 8: 다이어그램 (규칙 8)

- [ ] `코드 다이어그램.md` 맵 아트 파이프라인 절에 `RoomModuleCatalog`·`RoomModuleLogic`·`RoomModuleSocket`·`MapBuilder.BuildRoomModule` 클래스/시퀀스 추가

---

## 자기 검토
- 스펙 1단계 커버: 셸 메뉴 ✅(Task 5) · 카탈로그 ✅(3) · 로직 ✅(1) · MapBuilder 분기 ✅(4) · 모듈 1개(→ 2개, 발자국 2종이라) ✅(5) · BotLab 판정 ✅(7)
- 타입 일치: `RoomModuleLogic.DoorSides(RoomRect, IReadOnlyList<CorridorSeg>)` · `RoomModuleCatalog.TryGet/IdsFor/DoorFrame/WallPlug` · `RoomModuleSocket.Kind/Index/Set` — Task 4·5·6이 같은 이름을 쓴다
- 2단계로 미룬 것: 소켓→치즈/벽감/상자/요새 자리, 역할별 모듈, 조명 소켓, 배치 검수 메뉴
