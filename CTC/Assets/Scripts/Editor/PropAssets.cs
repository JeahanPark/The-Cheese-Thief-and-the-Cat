using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 절차 생성 프롭 에셋의 **경로 단일 출처**와, 그것을 프리팹에 얹는 헬퍼.
    ///
    /// 경로를 굽는 쪽(<see cref="FortressMeshBuilder"/>·<see cref="PropMeshBuilder"/>)과
    /// 쓰는 쪽(SetupNetworkingScene 팩토리들)이 따로 들고 있으면 한쪽만 고쳤을 때
    /// **에러 없이 프리미티브로 되돌아간다** — 조용히 깨지는 종류라 한곳에 모았다.
    /// </summary>
    internal static class PropAssets
    {
        private const string MODELS_ROOT = "Assets/Models";
        private const string MATERIAL_FOLDER = "Assets/Materials/Props";

        public const string STRUCTURE_FOLDER = MODELS_ROOT + "/Structures";
        public const string PROP_FOLDER = MODELS_ROOT + "/Props";

        // 치즈 쐐기는 CheeseMeshBuilder가 굽는다 — 여기서는 **재사용만** 한다.
        public const string CHEESE_MESH = MODELS_ROOT + "/Cheese/Mesh_CheeseWedge.asset";
        public const string CHEESE_MATERIAL = MATERIAL_FOLDER + "/M_Cheese.mat";

        public const string FORTRESS_MESH = STRUCTURE_FOLDER + "/Mesh_CheeseFortress.asset";
        public const string FORTRESS_MATERIAL = MATERIAL_FOLDER + "/M_CheeseWall.mat";

        public const string WAREHOUSE_MESH = STRUCTURE_FOLDER + "/Mesh_CheeseWarehouse.asset";

        public const string ALCOVE_MESH = STRUCTURE_FOLDER + "/Mesh_AlcovePanel.asset";
        public const string ALCOVE_MATERIAL = MATERIAL_FOLDER + "/M_AlcovePanel.mat";

        public const string CRATE_MESH = PROP_FOLDER + "/Mesh_LootCrate.asset";
        public const string CRATE_MATERIAL = MATERIAL_FOLDER + "/M_LootCrate.mat";

        public const string TICKET_MESH = PROP_FOLDER + "/Mesh_JobTicket.asset";
        public const string HERO_TICKET_MATERIAL = MATERIAL_FOLDER + "/M_HeroTicket.mat";
        public const string GANG_TICKET_MATERIAL = MATERIAL_FOLDER + "/M_GangTicket.mat";

        /// <summary>
        /// 없으면 null과 경고. **에러가 아니라 경고인 이유**: 메시 빌더를 아직 안 돌린 상태에서도
        /// `Setup Networking Scene`은 끝까지 돌아야 한다. 부르는 쪽이 프리미티브로 되돌아간다.
        /// </summary>
        public static Mesh LoadMesh(string _path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(_path);
            if (mesh == null)
            {
                Debug.LogWarning($"[PropAssets] 메시 없음(프리미티브로 대체): {_path} — Tools/CTC의 메시 빌더를 먼저 돌려라");
            }
            return mesh;
        }

        public static Material LoadMaterial(string _path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            if (mat == null) Debug.LogWarning($"[PropAssets] 머티리얼 없음: {_path}");
            return mat;
        }

        /// <summary>
        /// 보이는 것만 담당하는 자식을 붙인다. 콜라이더는 절대 안 건드린다 —
        /// 밸런스가 걸린 치수는 프리미티브(Body)가 계속 들고 있고, 이 자식은 그림자와 그림뿐이다.
        /// </summary>
        public static GameObject AddVisual(GameObject _root, string _name, Mesh _mesh, Material _material,
            Vector3 _localPosition, Quaternion _localRotation)
        {
            var go = new GameObject(_name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = _localPosition;
            go.transform.localRotation = _localRotation;
            go.layer = _root.layer;

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        /// <summary>
        /// 프리미티브에서 **그리는 부분만** 떼어 낸다(콜라이더는 남긴다).
        /// 새 메시를 얹으면서 원래 큐브를 지우지 않는 이유가 이것이다 — 콜라이더가 곧 밸런스다.
        /// </summary>
        public static void StripRenderer(GameObject _go)
        {
            var renderer = _go.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer);

            var filter = _go.GetComponent<MeshFilter>();
            if (filter != null) Object.DestroyImmediate(filter);
        }

        /// <summary>루트 아래 모든 자식에서 렌더러를 뗀다 — 예외로 둘 하나만 남긴다(새 시각물).</summary>
        public static void StripRenderersExcept(GameObject _root, GameObject _keep)
        {
            var filters = _root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                if (filter == null) continue;
                if (_keep != null && filter.gameObject == _keep) continue;
                StripRenderer(filter.gameObject);
            }
        }
    }
}
