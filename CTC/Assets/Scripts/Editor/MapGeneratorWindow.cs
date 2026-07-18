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

        // 씬 굽기(Task 6)가 참조할 활성 레이아웃 1개를 즉시 굽는다 — 첫 유효 시드.
        // 육안 시각화는 Task 6(SetupNetworkingScene) 이후 가능.
        [MenuItem("Tools/CTC/Build Sample Map Layout")]
        public static void BuildSample()
        {
            var pool = AssetDatabase.LoadAssetAtPath<Pool>(PoolPath);
            if (pool == null) { Debug.LogError($"[MapGenerator] 후보풀 없음: {PoolPath} — 먼저 Build Map Candidate Pool"); return; }

            var dto = ToDto(pool);
            for (int s = 1; s <= 5000; s++)
            {
                var r = MapGenerator.Generate(dto, s);
                if (r == null) continue;
                SaveLayout("Assets/Prefabs/Maps/Layout_Sketch01.asset", s, "sample", r);
                Debug.Log($"[MapGenerator] 샘플 레이아웃 저장 (seed {s}) — 방 {r.Rooms.Count}·통로 {r.Corridors.Count}·치즈 {r.Cheese.Count}·요새 {r.Fortresses.Count}");
                return;
            }
            Debug.LogError("[MapGenerator] 5000시드 내 유효 맵 실패 — 후보풀 점검 필요");
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
        public static MapGenerator.PoolData ToDto(Pool p)
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
            string path = $"Assets/Prefabs/Maps/Layout_Seed{m_Preview.Seed}.asset";
            SaveLayout(path, m_Preview.Seed, $"seed:{m_Preview.Seed}", m_Preview);
            m_Status = $"저장 — {path}";
        }

        // 생성 결과(순수) → MapLayout 에셋. 창·시안 빌더 공용.
        public static void SaveLayout(string path, int seed, string source, MapGenerator.LayoutResult r)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Maps"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Maps");
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(path);
            if (layout == null) { layout = ScriptableObject.CreateInstance<MapLayout>(); AssetDatabase.CreateAsset(layout, path); }
            layout.Set(seed, source, r.Rooms, r.Corridors, r.Cheese, r.Fortresses);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }
    }
}
