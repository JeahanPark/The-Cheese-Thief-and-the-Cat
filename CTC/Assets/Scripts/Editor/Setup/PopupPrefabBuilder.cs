using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 팝업 프리팹을 코드로 굽고 Addressables에 등록한다 — Tools > CTC > Build Popup Prefabs (2026-09-01).
    ///
    /// UI를 코드로 짓는 방식은 그대로다. 달라진 건 **산출물이 씬이 아니라 프리팹**이라는 것뿐이다.
    /// 그래야 어느 화면에서든 <see cref="CTC.Runtime.UI.Popup.PopupManager"/>가 같은 창을 불러올 수 있다.
    ///
    /// **주소 = 클래스명**(WES 규약). 손으로 등록하지 않는다 — 오타가 나면 런타임에만 드러난다.
    /// </summary>
    public static partial class PopupPrefabBuilder
    {
        private const string POPUP_DIR = "Assets/GameResource/UI/Popup";
        private const string GROUP_NAME = "Popup";

        [MenuItem("Tools/CTC/Build Popup Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder();
            BuildOptionsPopupPrefab();
            BuildTextInputPopupPrefab();   // 2026-09-14 대기실 앞단
            BuildMessagePopupPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[PopupPrefab] 완료 — 팝업 프리팹 + Addressables 등록");
        }

        /// <summary>옵션창 — 4개 화면이 공유하는 첫 이주 대상.</summary>
        private static void BuildOptionsPopupPrefab()
        {
            // 화면 전체를 덮는 루트 — 딤이 이 사각형을 채운다(BasePopup).
            var root = new GameObject("OptionsPopup", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 기존 생성 코드를 그대로 재사용한다 — 창의 모양이 갈라지면 안 된다.
            SetupNetworkingScene.BuildOptionsPopup(root, null);

            string path = $"{POPUP_DIR}/OptionsPopup.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            RegisterAddressable(path, "OptionsPopup");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/GameResource"))
                AssetDatabase.CreateFolder("Assets", "GameResource");
            if (!AssetDatabase.IsValidFolder("Assets/GameResource/UI"))
                AssetDatabase.CreateFolder("Assets/GameResource", "UI");
            if (!AssetDatabase.IsValidFolder(POPUP_DIR))
                AssetDatabase.CreateFolder("Assets/GameResource/UI", "Popup");
        }

        /// <summary>Addressables 그룹 `Popup`에 주소를 박는다. 없으면 설정·그룹부터 만든다.</summary>
        private static void RegisterAddressable(string _assetPath, string _address)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("[PopupPrefab] Addressables 설정을 못 만들었다");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(GROUP_NAME);
            if (group == null)
            {
                group = settings.CreateGroup(GROUP_NAME, false, false, true, null,
                    typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
                    typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));
            }

            string guid = AssetDatabase.AssetPathToGUID(_assetPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = _address;   // 클래스명과 같아야 한다 — PopupStackLogic.AddressOf
            entry.SetLabel(GROUP_NAME, true, true);

            EditorUtility.SetDirty(settings);
            Debug.Log($"[PopupPrefab] 등록: {_address} → {_assetPath}");
        }
    }
}
