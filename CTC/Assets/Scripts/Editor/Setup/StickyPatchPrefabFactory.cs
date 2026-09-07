using CTC.Runtime.Economy;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 끈끈이 장판 프리팹(테크 v3 계획 2a). 바닥에 눕힌 쿼드 1장이 전부다 —
    /// 감속은 <see cref="StickyPatch"/>가 거리로 판정하므로 콜라이더가 없다(몸을 막으면 함정이 아니라 벽이다).
    ///
    /// <see cref="ShopSpotPrefabFactory"/>와 같이 **있으면 안 덮어쓴다**(CLAUDE.md 함정 2) —
    /// 크기의 주인은 CSV(TechInfo P1)라 런타임이 다시 맞추고, 생김새의 주인은 프리팹이다.
    /// </summary>
    public static class StickyPatchPrefabFactory
    {
        public const string PATH = "Assets/Prefabs/StickyPatch.prefab";

        // 기능색(노랑) — 끈끈이라는 걸 색만 보고 알아야 한다. 팬트리 가구와 섞이지 않는다.
        private const string PATCH_MATERIAL =
            "Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/URP/Prototype_512x512_Yellow.mat";

        // 바닥과 겹쳐 z-fighting이 나지 않을 만큼만 띄운다
        private const float QUAD_HEIGHT = 0.02f;

        public static GameObject Build()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
            if (existing != null) return existing;   // 있으면 안 덮어쓴다(CLAUDE.md 함정 2)

            var root = new GameObject("StickyPatch");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<StickyPatch>();
                AddQuad(root);

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, PATH);
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddQuad(GameObject _root)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = StickyPatch.QUAD_NAME;
            quad.transform.SetParent(_root.transform, false);
            // 쿼드는 XY 평면이라 x축 90°로 눕혀야 바닥이 된다. 크기는 미리보기용 —
            // 실제 지름은 StickyPatch가 스폰 때 그 레벨의 반경으로 다시 맞춘다.
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localPosition = new Vector3(0f, QUAD_HEIGHT, 0f);
            quad.transform.localScale = new Vector3(5f, 5f, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            var mat = AssetDatabase.LoadAssetAtPath<Material>(PATCH_MATERIAL);
            if (mat == null) Debug.LogError($"[StickyPatchPrefabFactory] 장판 머티리얼 없음: {PATCH_MATERIAL}");
            else quad.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
