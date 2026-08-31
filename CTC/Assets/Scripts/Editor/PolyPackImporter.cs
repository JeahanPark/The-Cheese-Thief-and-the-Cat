using System.IO;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// Poly Universal Pack 선별 임포트 (2026-08-31, 계획 Task 9).
    ///
    /// 순서가 셋으로 갈라져 있는 이유 — 임포트는 비동기라 한 메뉴에 묶으면
    /// **리임포트가 끝나기 전에 다음 단계가 돌아** 마젠타·빈 참조가 생긴다(함정 6).
    /// ① 본체 임포트 → (끝나길 기다림) → ② URP 패치 임포트 → (또 기다림) → ③ 불용 카테고리 삭제.
    /// </summary>
    public static class PolyPackImporter
    {
        private const string PACKAGE_PATH =
            @"C:\Users\user\AppData\Roaming\Unity\Asset Store-5.x\polyperfect\3D ModelsProps\Poly Universal Pack.unitypackage";

        private const string PACK_ROOT = "Assets/polyperfect/Poly Universal Pack";
        private const string URP_PATCH = PACK_ROOT + "/Poly Universal Pack - URP.unitypackage";

        // 팬트리에 안 쓰는 카테고리 — Prefabs·Meshes·Materials가 같은 이름으로 짝지어 있다.
        // Steampunk는 지우되 빅토리안 선반 2종만 남기고 싶지만, 폴더째 지우는 편이 안전하다 —
        // 선반은 Fantasy(Bakery·Furniture)·Survival 것 4종으로 충분하다(2026-08-31 실사).
        private static readonly string[] UNUSED_CATEGORIES =
        {
            "4July", "Cars", "Christmas", "Easter", "Guns", "Halloween",
            "Movie Set", "People", "Primeval", "Racetrack", "Steampunk", "Valentine",
        };

        [MenuItem("Tools/CTC/Dev/Poly Pack ① 본체 임포트")]
        public static void ImportBase()
        {
            if (!File.Exists(PACKAGE_PATH))
            {
                Debug.LogError($"[PolyPack] 패키지가 없다: {PACKAGE_PATH}");
                return;
            }
            Debug.Log("[PolyPack] 본체 임포트 시작 — 수천 파일이라 몇 분 걸린다. 끝나면 ②를 돌려라");
            AssetDatabase.ImportPackage(PACKAGE_PATH, false);
        }

        [MenuItem("Tools/CTC/Dev/Poly Pack ② URP 패치")]
        public static void ImportUrpPatch()
        {
            string full = Path.GetFullPath(URP_PATCH);
            if (!File.Exists(full))
            {
                Debug.LogError($"[PolyPack] URP 패치가 없다(본체 임포트가 먼저다): {URP_PATCH}");
                return;
            }
            Debug.Log("[PolyPack] URP 패치 임포트 — 기본 머티리얼은 빌트인용이라 이게 없으면 마젠타다");
            AssetDatabase.ImportPackage(full, false);
        }

        // 팬트리가 실제로 세울 프리팹 — 이것들의 의존성만 남긴다(④).
        // 텍스처가 611MB라 카테고리 삭제(③)만으로는 리포에 못 싣는다(2026-08-31 실측 842MB).
        private static readonly string[] KEEP_PREFABS =
        {
            // 선반 (방 벽)
            PACK_ROOT + "/- Prefabs/Fantasy/Bakery Fantasy/shelf-wall-a-fantasy.prefab",
            PACK_ROOT + "/- Prefabs/Fantasy/Bakery Fantasy/shelf-wall-a-pastry-fantasy.prefab",
            PACK_ROOT + "/- Prefabs/Fantasy/Furniture Fantasy/shelf-simple-a-fantasy.prefab",
            PACK_ROOT + "/- Prefabs/Survival/shelf-wall-wood.prefab",
            // 궤짝 (복도 벽)
            PACK_ROOT + "/- Prefabs/Farm/Crates Farm/crate-wood-big.prefab",
            PACK_ROOT + "/- Prefabs/Farm/Crates Farm/crate-wood-medium.prefab",
            PACK_ROOT + "/- Prefabs/Farm/Crates Farm/crate-wood-medium-potatoes-a.prefab",
            PACK_ROOT + "/- Prefabs/Farm/Crates Farm/crate-wood-small.prefab",
            // 배럴·호박 (장애물)
            PACK_ROOT + "/- Prefabs/Fantasy/barrel-wood-a-closed-a-fantasy.prefab",
            PACK_ROOT + "/- Prefabs/Fantasy/barrel-wood-a-closed-b-fantasy.prefab",
            PACK_ROOT + "/- Prefabs/Fantasy/barrel-wood-b-closed-fantasy.prefab",
            PACK_ROOT + "/- Prefabs/Farm/Crops Farm/pumpkin-b.prefab",
            PACK_ROOT + "/- Prefabs/Farm/Crops Farm/pumpkin-ripe-a.prefab",
            // 거대 음식 (드레싱)
            PACK_ROOT + "/- Prefabs/Food/almond.prefab",
            PACK_ROOT + "/- Prefabs/Food/chestnut-a.prefab",
            PACK_ROOT + "/- Prefabs/Food/walnut.prefab",
            PACK_ROOT + "/- Prefabs/Food/strawberry.prefab",
        };

        /// <summary>
        /// 남길 프리팹의 의존성(메시·머티리얼·텍스처)만 살리고 팩의 나머지를 전부 지운다.
        /// 폴더 구조는 그대로 둔다 — 경로가 바뀌면 프리팹 참조가 끊긴다.
        /// </summary>
        [MenuItem("Tools/CTC/Dev/Poly Pack ④ 의존성만 남기고 걷어내기")]
        public static void KeepOnlyUsed()
        {
            var keep = new System.Collections.Generic.HashSet<string>();
            foreach (string prefab in KEEP_PREFABS)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null)
                {
                    Debug.LogWarning($"[PolyPack] 프리팹 없음(건너뜀): {prefab}");
                    continue;
                }
                foreach (string dep in AssetDatabase.GetDependencies(prefab, true))
                    keep.Add(dep.Replace('\\', '/'));
            }
            if (keep.Count == 0)
            {
                Debug.LogError("[PolyPack] 남길 게 하나도 안 잡혔다 — 경로를 다시 봐라. 아무것도 안 지운다");
                return;
            }

            string[] all = AssetDatabase.FindAssets("", new[] { PACK_ROOT });
            var toDelete = new System.Collections.Generic.List<string>();
            foreach (string guid in all)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (!keep.Contains(path)) toDelete.Add(path);
            }

            var failed = new System.Collections.Generic.List<string>();
            AssetDatabase.DeleteAssets(toDelete.ToArray(), failed);
            DeleteEmptyFolders(PACK_ROOT);
            AssetDatabase.Refresh();
            Debug.Log($"[PolyPack] 정리 — 유지 {keep.Count} · 삭제 {toDelete.Count - failed.Count} · 실패 {failed.Count}");
        }

        private static void DeleteEmptyFolders(string _root)
        {
            foreach (string dir in Directory.GetDirectories(_root))
            {
                string norm = dir.Replace('\\', '/');
                DeleteEmptyFolders(norm);
                bool empty = Directory.GetFileSystemEntries(norm).Length == 0
                    || System.Array.TrueForAll(Directory.GetFileSystemEntries(norm),
                        _e => _e.EndsWith(".meta"));
                if (empty) AssetDatabase.DeleteAsset(norm);
            }
        }

        [MenuItem("Tools/CTC/Dev/Poly Pack ③ 불용 카테고리 삭제")]
        public static void TrimUnused()
        {
            int removed = 0;
            // 카테고리 폴더는 "- Prefabs"·"- Meshes"·"- Materials"·"- Textures"·"- Animations" 아래에
            // 같은 이름으로 반복된다. 데모 씬은 통째로 뺀다.
            string[] roots =
            {
                PACK_ROOT + "/- Prefabs", PACK_ROOT + "/- Meshes", PACK_ROOT + "/- Materials",
                PACK_ROOT + "/- Textures", PACK_ROOT + "/- Animations",
            };
            foreach (string root in roots)
            {
                foreach (string category in UNUSED_CATEGORIES)
                {
                    string dir = $"{root}/{category}";
                    if (AssetDatabase.IsValidFolder(dir) && AssetDatabase.DeleteAsset(dir)) removed++;
                }
            }
            if (AssetDatabase.DeleteAsset(PACK_ROOT + "/- Demo Scenes")) removed++;
            if (AssetDatabase.DeleteAsset(PACK_ROOT + "/01 All Models")) removed++;
            if (AssetDatabase.DeleteAsset(PACK_ROOT + "/01 All Models.unity")) removed++;
            // 내장 패키지 원본도 치운다 — 리포에 이중으로 실릴 이유가 없다(HDRP는 안 쓴다).
            if (AssetDatabase.DeleteAsset(URP_PATCH)) removed++;
            if (AssetDatabase.DeleteAsset(PACK_ROOT + "/Poly Universal Pack - HDRP.unitypackage")) removed++;

            AssetDatabase.Refresh();
            Debug.Log($"[PolyPack] 삭제 {removed}건 — 유지: Farm·Fantasy·Food·Nature·Survival·Tools·Modular Parts·Drinks·Sports·Terrains");
        }
    }
}
