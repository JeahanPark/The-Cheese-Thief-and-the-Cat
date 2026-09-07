using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 상점 자리 프리팹 (테크 v3). 부수지 못하므로 `Health`·`RoleTag`가 없다 —
    /// 걸어 들어가는 구역이라 콜라이더도 전부 뺀다(몸을 막으면 상점이 아니라 기둥이다).
    ///
    /// `SetupNetworkingScene`의 다른 팩토리와 달리 **있으면 안 덮어쓴다**(CLAUDE.md 함정 2) —
    /// 자리는 스포너가 정하지만 생김새의 주인은 프리팹이라, 씬을 다시 구울 때마다
    /// 손으로 고친 비주얼이 지워지면 안 된다.
    /// </summary>
    public static class ShopSpotPrefabFactory
    {
        public const string PATH = "Assets/Prefabs/ShopSpot.prefab";

        // 반경 표시용 원반 머티리얼 — 기능색(주황)이라 팬트리 가구와 섞이지 않는다
        private const string DISC_MATERIAL =
            "Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/URP/Prototype_512x512_Orange.mat";

        // 가운데 세우는 진열대 — 팩이 없으면 원반만 남는다(마젠타 방지, 함정 6)
        private const string STAND_PREFAB =
            "Assets/polyperfect/Poly Universal Pack/- Prefabs/Fantasy/Bakery Fantasy/shelf-wall-a-pastry-fantasy.prefab";

        private const float DISC_HEIGHT = 0.05f;

        public static GameObject Build()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
            if (existing != null) return existing;   // 있으면 안 덮어쓴다(CLAUDE.md 함정 2)

            var root = new GameObject("ShopSpot");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<ShopSpot>();

                // 사거리를 눈으로 알 수 있어야 "여기서만 산다"가 규칙으로 읽힌다 —
                // 반경은 CSV(BalanceInfo.ShopSpotRadius)가 원본이라 여기서는 굽기만 한다.
                float radius = CTC.Runtime.Info.InfoManager.Instance.Balance.ShopSpotRadius;
                AddDisc(root, radius);
                AddStand(root);

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, PATH);
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddDisc(GameObject _root, float _radius)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = ShopSpot.DISC_NAME;
            disc.transform.SetParent(_root.transform, false);
            // 실린더는 높이 2가 기본 — 절반을 곱해야 실제 반경·두께가 된다.
            // 이건 에디터 미리보기용이다 — 실제 크기는 `ShopSpot`이 스폰할 때 지금 CSV 값으로 다시 맞춘다.
            disc.transform.localScale = new Vector3(_radius * 2f, DISC_HEIGHT * 0.5f, _radius * 2f);
            disc.transform.localPosition = new Vector3(0f, DISC_HEIGHT * 0.5f, 0f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());

            var mat = AssetDatabase.LoadAssetAtPath<Material>(DISC_MATERIAL);
            if (mat == null) Debug.LogError($"[ShopSpotPrefabFactory] 원반 머티리얼 없음: {DISC_MATERIAL}");
            else disc.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void AddStand(GameObject _root)
        {
            var standAsset = AssetDatabase.LoadAssetAtPath<GameObject>(STAND_PREFAB);
            if (standAsset == null)
            {
                Debug.LogWarning($"[ShopSpotPrefabFactory] 진열대 프리팹 없음(원반만): {STAND_PREFAB}");
                return;
            }

            var stand = (GameObject)PrefabUtility.InstantiatePrefab(standAsset);
            stand.name = "Stand";
            stand.transform.SetParent(_root.transform, false);
            stand.transform.localPosition = new Vector3(0f, DISC_HEIGHT, 0f);

            // 걸어 들어가는 구역이라 몸을 막으면 안 된다
            foreach (Collider col in stand.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }
        }
    }
}
