using System.Linq;
using System.Reflection;
using CTC.Runtime.Cameras;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Managers;
using CTC.Runtime.Map;
using CTC.Runtime.Match;
using CTC.Runtime.Networking;
using CTC.Runtime.Player;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CTC.Editor
{
    // SetupNetworkingScene의 일부 — 머티리얼·박스·SerializedObject 세터·해시 베이크 (개편 4단계 분할, 2026-07-31)
    public static partial class SetupNetworkingScene
    {
        // 미끼·쥐 전용 레이어 — 둘의 충돌을 매트릭스에서 끊는다(DecoyLayers).
        // 레이어가 없으면 빈 슬롯(8번 이후)에 만든다.
        private static int EnsureLayer(string _layerName)
        {
            int existing = LayerMask.NameToLayer(_layerName);
            if (existing >= 0) return existing;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[SetupNetworkingScene] TagManager.asset을 열 수 없다 — 미끼 레이어 미생성");
                return -1;
            }

            var tagManager = new SerializedObject(asset[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = _layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log($"[SetupNetworkingScene] '{_layerName}' 레이어 생성 (index {i})");
                return i;
            }

            Debug.LogError($"[SetupNetworkingScene] 빈 레이어 슬롯이 없다 — '{_layerName}' 미생성");
            return -1;
        }

        // Quirky 원본 머티리얼의 색/텍스처를 URP SoftSurfaceGraph 머티리얼로 복사한다.
        // (원본은 빌트인 CGPROGRAM 셰이더라 URP에서 마젠타)
        private static Material CreateUrpMaterial(string _name, string _sourceMatPath)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsDir))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(UrpShaderPath);
            var source = AssetDatabase.LoadAssetAtPath<Material>(_sourceMatPath);

            var mat = new Material(shader) { name = _name };
            if (source != null)
            {
                if (source.HasProperty("_Color")) mat.SetColor("_Color", source.GetColor("_Color"));
                if (source.HasProperty("_MainTex")) mat.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                if (source.HasProperty("_Emission")) mat.SetFloat("_Emission", source.GetFloat("_Emission"));
            }

            string path = MaterialsDir + "/" + _name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 프리미티브 메시만 필요할 때 — 임시 오브젝트를 만들어 메시만 빼고 버린다.
        private static Mesh PrimitiveMesh(PrimitiveType _type)
        {
            GameObject temp = GameObject.CreatePrimitive(_type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        // NGO는 에디터 OnValidate에서 GlobalObjectIdHash = GlobalObjectId 문자열의 XXHash32로
        // 굽지만, 저장 전 임시 오브젝트에서는 무효한 id(targetObjectId=0)로 구워져 충돌한다.
        // 저장(영속화) 후 동일 계산을 직접 수행해 SerializedObject로 쓴다 — OnValidate 재실행 시에도
        // 같은 값이 나오므로 이후 에디터 churn이 없다. (필드·XXHash 모두 internal → 리플렉션)
        private static void BakeGlobalObjectIdHash(NetworkObject _networkObject)
        {
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(_networkObject);
            if (globalId.identifierType == 0 || globalId.targetObjectId == 0)
            {
                Debug.LogError($"[SetupNetworkingScene] {_networkObject.name} GlobalObjectId 무효({globalId}) — 해시 베이크 불가");
                return;
            }

            System.Type xxHash = typeof(NetworkObject).Assembly.GetType("Unity.Netcode.XXHash");
            MethodInfo hash32 = xxHash.GetMethod("Hash32", new[] { typeof(string) });
            uint hash = (uint)hash32.Invoke(null, new object[] { globalId.ToString() });

            var so = new SerializedObject(_networkObject);
            so.FindProperty("GlobalObjectIdHash").uintValue = hash;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 로드 시 OnValidate가 메모리만 교정(dirty 없음)하는 경우 위 Apply가 no-op이 되어
            // 디스크의 스테일/충돌 값이 영영 안 고쳐진다 → 무조건 dirty로 재직렬화 강제
            EditorUtility.SetDirty(_networkObject);
        }

        // 베이크 결과 최종 검증 — 0이거나 충돌하면 NGO ScenePlacedObjects 등록이 깨진다.
        private static void VerifyUniqueHashes(params NetworkObject[] _networkObjects)
        {
            var seen = new System.Collections.Generic.Dictionary<uint, string>();
            foreach (NetworkObject no in _networkObjects)
            {
                uint hash = new SerializedObject(no).FindProperty("GlobalObjectIdHash").uintValue;
                if (hash == 0)
                    Debug.LogError($"[SetupNetworkingScene] {no.name} GlobalObjectIdHash=0");
                else if (seen.TryGetValue(hash, out string other))
                    Debug.LogError($"[SetupNetworkingScene] GlobalObjectIdHash 충돌: {no.name} == {other} ({hash})");
                else
                    seen.Add(hash, no.name);
            }
        }

        private static Material GridboxMaterial(string _name)
        {
            string path = $"{GridboxUrpDir}/Prototype_512x512_{_name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) Debug.LogError($"[SetupNetworkingScene] Gridbox 머티리얼 없음: {path}");
            return mat;
        }

        private static GameObject AddBox(GameObject _parent, Material _material, string _name, Vector3 _center, Vector3 _size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.SetParent(_parent.transform, false);
            box.transform.localPosition = _center;
            box.transform.localScale = _size;
            box.GetComponent<Renderer>().sharedMaterial = _material;
            return box;
        }

        // 대각 통로용 회전 박스 (AddBox + 회전)
        private static GameObject AddOrientedBox(
            GameObject _parent, Material _material, string _name, Vector3 _center, Quaternion _rotation, Vector3 _size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.SetParent(_parent.transform, false);
            box.transform.localPosition = _center;
            box.transform.localRotation = _rotation;
            box.transform.localScale = _size;
            box.GetComponent<Renderer>().sharedMaterial = _material;
            return box;
        }

        private static AudioClip LoadClip(string _relativePath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + _relativePath + ".ogg");
            if (clip == null) Debug.LogError($"[Setup] AudioClip 없음: Assets/Audio/{_relativePath}.ogg");
            return clip;
        }

        // 직렬화 private 필드 세팅 유틸 (프리팹 저장 전 인스턴스에 적용)
        private static void SetFloat(Component _c, string _field, float _value)
        {
            var so = new SerializedObject(_c);
            so.FindProperty(_field).floatValue = _value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Component _c, string _field, int _value)
        {
            var so = new SerializedObject(_c);
            so.FindProperty(_field).intValue = _value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Component _c, string _field, Object _value)
        {
            var so = new SerializedObject(_c);
            so.FindProperty(_field).objectReferenceValue = _value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefArray(Component _c, string _field, Object[] _values)
        {
            var so = new SerializedObject(_c);
            SerializedProperty prop = so.FindProperty(_field);
            prop.arraySize = _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
