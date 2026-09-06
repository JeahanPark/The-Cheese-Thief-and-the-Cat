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
        public const string ExpandedPoolPath = "Assets/Prefabs/Maps/MapCandidatePool_Expanded.asset";
        public const string LargePoolPath = "Assets/Prefabs/Maps/MapCandidatePool_Large.asset";
        private Pool m_Pool;
        private int m_Seed = 1;
        private MapGenerator.LayoutResult m_Preview;
        private string m_Status = "";

        [MenuItem("Tools/CTC/Generate Map Layout")]
        public static void Open() => GetWindow<MapGeneratorWindow>("Map Generator");

        // 씬 굽기(Task 6)가 참조할 활성 레이아웃 1개를 즉시 굽는다 — 첫 유효 시드.
        // 육안 시각화는 Task 6(SetupNetworkingScene) 이후 가능.
        [MenuItem("Tools/CTC/Build Sample Map Layout")]
        public static void BuildSample()
        {
            BakeFirstValid(PoolPath, "Assets/Prefabs/Maps/Layout_Sketch01.asset", "기본 맵");
        }

        /// <summary>확장 풀(Build Expanded Map Pool 산출)로 확장 레이아웃을 굽는다.</summary>
        [MenuItem("Tools/CTC/Build Expanded Map Layout")]
        public static void BuildExpanded()
        {
            BakeFirstValid(ExpandedPoolPath, "Assets/Prefabs/Maps/Layout_Expanded01.asset", "확장 맵");
        }

        /// <summary>대형 풀(Build Large Map Pool 산출)로 120×120 레이아웃을 굽는다 — 기준 구성 2:8.</summary>
        [MenuItem("Tools/CTC/Build Large Map Layout")]
        public static void BuildLarge()
        {
            BakeFirstValid(LargePoolPath, "Assets/Prefabs/Maps/Layout_Large01.asset", "대형 맵");
        }

        // 첫 유효 시드로 레이아웃을 굽는다. 풀의 캔버스 크기·표시 이름을 레이아웃에 함께 새긴다.
        private static void BakeFirstValid(string _poolPath, string _layoutPath, string _fallbackName)
        {
            var pool = AssetDatabase.LoadAssetAtPath<Pool>(_poolPath);
            if (pool == null) { Debug.LogError($"[MapGenerator] 후보풀 없음: {_poolPath}"); return; }

            string displayName = string.IsNullOrEmpty(pool.DisplayName) ? _fallbackName : pool.DisplayName;
            var dto = ToDto(pool);
            for (int s = 1; s <= 5000; s++)
            {
                var r = MapGenerator.Generate(dto, s);
                if (r == null) continue;
                SaveLayout(_layoutPath, s, "sample", r, pool.HalfExtent, displayName,
                    pool.RecommendedMiceMin, pool.RecommendedMiceMax);
                Debug.Log($"[MapGenerator] {displayName} 저장 (seed {s}) — 방 {r.Rooms.Count}·통로 {r.Corridors.Count}" +
                          $"·치즈 {r.Cheese.Count}·요새 {r.Fortresses.Count}·반경 {pool.HalfExtent}" +
                          $"·권장 쥐 {pool.RecommendedMiceMin}~{pool.RecommendedMiceMax}");
                return;
            }
            Debug.LogError($"[MapGenerator] 5000시드 내 유효 맵 실패 — {_poolPath} 점검 필요");
        }

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

        // SO → 순수 DTO (생성기 입력)
        public static MapGenerator.PoolData ToDto(Pool _p)
        {
            var rooms = _p.Rooms.Select(r => new MapGenerator.RoomOption(
                r.Id, r.Center, r.Size, (MapGenerator.Tag)(int)r.Tag, r.AlwaysInclude, r.ModuleId ?? "")).ToList();
            var corr = _p.Corridors.Select(c => new MapGenerator.CorridorOption(
                c.Id, c.RoomA, c.RoomB, c.Waypoints.ToArray(), c.Width)).ToList();
            var cheese = _p.CheeseSlots.Select(c => new MapGenerator.CheeseSlotOption(
                c.RoomId, c.Pos, c.MinGrade, c.MaxGrade, c.Fixed, c.FixedGrade, c.Radius)).ToList();
            var fort = _p.FortressSlots.Select(f => new MapGenerator.FortressSlotOption(
                f.RoomId, f.Pos, f.YawDeg, f.AlwaysInclude)).ToList();
            return new MapGenerator.PoolData(rooms, corr, new List<MazePreset>(_p.Mazes), cheese, fort,
                _p.MinRooms, _p.MaxRooms, _p.CheeseSets, _p.MaxFortresses);
        }

        private void Generate(int _seed)
        {
            m_Preview = MapGenerator.Generate(ToDto(m_Pool), _seed);
            m_Status = m_Preview != null ? $"seed {_seed} → 유효 맵" : $"seed {_seed} → 실패(재시도 소진)";
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
            string path = $"Assets/Prefabs/Maps/Layout_Seed{m_Preview.Seed}.asset";
            SaveLayout(path, m_Preview.Seed, $"seed:{m_Preview.Seed}", m_Preview);
            m_Status = $"저장 — {path}";
        }

        // 생성 결과(순수) → MapLayout 에셋. 창·시안 빌더 공용.
        // halfExtent·displayName은 풀에서 온다 — 런타임 MapBuilder가 바닥·경계벽을 이걸로 만든다.
        public static void SaveLayout(string _path, int _seed, string _source, MapGenerator.LayoutResult _r,
            float _halfExtent = 15f, string _displayName = "",
            int _recommendedMiceMin = 2, int _recommendedMiceMax = 3)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Maps"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Maps");
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(_path);
            if (layout == null) { layout = ScriptableObject.CreateInstance<MapLayout>(); AssetDatabase.CreateAsset(layout, _path); }
            layout.Set(_seed, _source, _r.Rooms, _r.Corridors, _r.Cheese, _r.Fortresses, _halfExtent, _displayName,
                _recommendedMiceMin, _recommendedMiceMax);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }
    }
}
