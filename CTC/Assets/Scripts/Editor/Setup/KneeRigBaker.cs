using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 쥐 모델의 뒷다리에 **무릎**을 넣는다 — 정강이 트랜스폼을 세우고 파생 메시의 가중치를 나눈다(2026-09-13).
    ///
    /// **왜 유니티에서 하나**: 팩 FBX 모델을 갈아 끼우지 않기 위해서다. 블렌더에서 메시까지 다시
    /// 내보내면 LOD 넷·셰이프키 30개·바인드포즈·100배 스케일(CLAUDE.md 블렌더 표)을 전부 다시 맞춰야
    /// 한다. 대신 이미 있는 파생 메시(<see cref="EyeSplitMeshBaker"/>가 굽는 <c>Mesh_*_EyeSplit</c>)에
    /// 뼈 하나를 더하고, 프리팹의 모델 계층에 트랜스폼 둘을 더한다.
    ///
    /// **정강이의 쉬는 자세는 추측하지 않는다.** 블렌더가 내보낸 <c>CTC_Mouse_Clips.fbx</c>를 유니티가
    /// 임포트한 계층에서 <c>shin.B.*</c>의 로컬 트랜스폼을 그대로 읽는다 — 클립 커브가 그 자세를 기준으로
    /// 찍혔으므로 여기가 한 치라도 다르면 무릎이 엉뚱하게 꺾인다. 같은 이유로 **허벅지의 로컬 자세가
    /// FBX와 모델에서 같은지** 먼저 대조하고, 다르면 굽지 않는다.
    ///
    /// **가중치 공식은 블렌더와 같다**(<see cref="KneeWeightLogic"/>). 무릎을 안 굽히는 클립에서는
    /// 정강이가 허벅지를 그대로 따라가므로 정점 위치가 원래와 **똑같다**(EditMode가 정점마다 확인한다).
    /// </summary>
    public static class KneeRigBaker
    {
        public const string CLIPS_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";
        public const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";
        public static readonly string[] SIDES = { "L", "R" };

        public static string ThighName(string _side) => "leg.B." + _side;
        public static string ShinName(string _side) => "shin.B." + _side;

        private const float POSE_EPSILON = 1e-4f;

        /// <summary>
        /// 모델에 무릎을 넣는다. **여러 번 불러도 같다** — 이미 넣었으면 트랜스폼·가중치를 다시 나누지 않는다.
        /// 파생 메시를 새로 구우면(<see cref="EyeSplitMeshBaker"/>) 가중치가 팩 원본으로 돌아가므로
        /// 그 뒤에 반드시 다시 부른다.
        /// </summary>
        public static bool Apply(GameObject _model)
        {
            if (_model == null) return false;

            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(CLIPS_FBX);
            if (reference == null)
            {
                Debug.LogError($"[KneeRigBaker] 기준 FBX가 없다: {CLIPS_FBX}");
                return false;
            }

            var shins = new Transform[SIDES.Length];
            for (int i = 0; i < SIDES.Length; i++)
            {
                shins[i] = EnsureShin(_model.transform, reference.transform, SIDES[i]);
                if (shins[i] == null) return false;
            }

            int baked = 0;
            foreach (SkinnedMeshRenderer skin in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (BakeRenderer(skin, shins)) baked++;
            }
            Debug.Log($"[KneeRigBaker] 무릎을 넣었다: {_model.name} (렌더러 {baked}개)");
            return baked > 0;
        }

        private static Transform EnsureShin(Transform _model, Transform _reference, string _side)
        {
            Transform refThigh = FindDeep(_reference, ThighName(_side));
            Transform refShin = refThigh != null ? refThigh.Find(ShinName(_side)) : null;
            if (refShin == null)
            {
                Debug.LogError($"[KneeRigBaker] {CLIPS_FBX}에 '{ShinName(_side)}'가 없다 — " +
                               "블렌더에서 tools/blender/add_knees.py를 돌리고 다시 내보냈나?");
                return null;
            }

            Transform thigh = FindDeep(_model, ThighName(_side));
            if (thigh == null)
            {
                Debug.LogError($"[KneeRigBaker] 모델에 '{ThighName(_side)}'가 없다: {_model.name}");
                return null;
            }

            if (!SamePose(thigh, refThigh))
            {
                Debug.LogError($"[KneeRigBaker] '{ThighName(_side)}'의 로컬 자세가 FBX와 모델에서 다르다 — " +
                               $"모델 {thigh.localPosition}/{thigh.localRotation.eulerAngles}/{thigh.localScale} · " +
                               $"FBX {refThigh.localPosition}/{refThigh.localRotation.eulerAngles}/{refThigh.localScale}. " +
                               "이대로 무릎을 넣으면 클립이 엉뚱하게 꺾인다 — 굽지 않는다");
                return null;
            }

            Transform shin = thigh.Find(ShinName(_side));
            if (shin == null)
            {
                shin = new GameObject(ShinName(_side)).transform;
                shin.SetParent(thigh, false);
            }
            shin.localPosition = refShin.localPosition;
            shin.localRotation = refShin.localRotation;
            shin.localScale = refShin.localScale;
            shin.gameObject.layer = thigh.gameObject.layer;
            return shin;
        }

        private static bool BakeRenderer(SkinnedMeshRenderer _skin, Transform[] _shins)
        {
            Mesh mesh = _skin.sharedMesh;
            if (mesh == null) return false;

            // 이미 붙인 정강이를 뺀 **원래 뼈 목록**을 먼저 세운다 — 여러 번 불러도 같은 결과가 되게.
            var bones = new List<Transform>();
            foreach (Transform b in _skin.bones)
            {
                if (b != null && b.name.StartsWith("shin.B.")) continue;
                bones.Add(b);
            }

            var thighIndex = new int[SIDES.Length];
            for (int i = 0; i < SIDES.Length; i++)
            {
                thighIndex[i] = bones.FindIndex(b => b != null && b.name == ThighName(SIDES[i]));
                if (thighIndex[i] < 0) return false;   // 뒷다리가 없는 렌더러(총 등)
            }

            string path = AssetDatabase.GetAssetPath(mesh);
            if (!path.EndsWith(".asset"))
            {
                Debug.LogError($"[KneeRigBaker] '{_skin.name}'이 파생 메시가 아니라 원본({path})을 물고 있다 — " +
                               "팩 FBX를 고칠 수는 없다. EyeSplitMeshBaker가 먼저 돌아야 한다");
                return false;
            }

            Matrix4x4[] bindposes = mesh.bindposes;
            if (bindposes.Length == bones.Count + SIDES.Length)
            {
                // 메시는 이미 굽혔다 — 렌더러의 뼈 목록만 맞춘다.
            }
            else if (bindposes.Length == bones.Count)
            {
                BakeMesh(mesh, bindposes, thighIndex, bones.Count, _shins);
            }
            else
            {
                Debug.LogError($"[KneeRigBaker] '{_skin.name}' 바인드포즈 {bindposes.Length}개 · 뼈 {bones.Count}개 — 맞지 않는다");
                return false;
            }

            bones.AddRange(_shins);
            _skin.bones = bones.ToArray();
            return true;
        }

        private static void BakeMesh(Mesh _mesh, Matrix4x4[] _bindposes, int[] _thighIndex, int _boneCount, Transform[] _shins)
        {
            var bindposes = new Matrix4x4[_boneCount + _shins.Length];
            System.Array.Copy(_bindposes, bindposes, _boneCount);
            for (int i = 0; i < _shins.Length; i++)
            {
                // 정강이 월드→로컬 = (무릎 로컬 TRS)⁻¹ × 허벅지 월드→로컬. 지금 자세와 무관하게 바인드에서 곧장 나온다.
                Matrix4x4 shinLocal = Matrix4x4.TRS(_shins[i].localPosition, _shins[i].localRotation, _shins[i].localScale);
                bindposes[_boneCount + i] = shinLocal.inverse * _bindposes[_thighIndex[i]];
            }

            Vector3[] vertices = _mesh.vertices;
            BoneWeight[] weights = _mesh.boneWeights;
            int moved = 0;
            for (int v = 0; v < vertices.Length; v++)
            {
                for (int i = 0; i < _shins.Length; i++)
                {
                    Vector3 p = _bindposes[_thighIndex[i]].MultiplyPoint3x4(vertices[v]);
                    float share = KneeWeightLogic.ShinShare(KneeWeightLogic.AxisParam(p, _shins[i].localPosition));
                    if (share <= 0f) continue;
                    if (Split(ref weights[v], _thighIndex[i], _boneCount + i, share)) moved++;
                }
            }

            _mesh.bindposes = bindposes;
            _mesh.boneWeights = weights;
            EditorUtility.SetDirty(_mesh);
            Debug.Log($"[KneeRigBaker] {_mesh.name}: 정점 {moved}개를 정강이와 나눴다");
        }

        /// <summary>
        /// 한 정점의 허벅지 몫 일부를 정강이로 옮긴다. 빈 슬롯이 없으면 옮기지 않는다(경고) —
        /// 뒷다리 정점은 영향이 둘뿐이라 실제로는 안 걸린다.
        /// </summary>
        public static bool Split(ref BoneWeight _w, int _thigh, int _shin, float _share)
        {
            var slots = new List<KeyValuePair<int, float>>
            {
                new KeyValuePair<int, float>(_w.boneIndex0, _w.weight0),
                new KeyValuePair<int, float>(_w.boneIndex1, _w.weight1),
                new KeyValuePair<int, float>(_w.boneIndex2, _w.weight2),
                new KeyValuePair<int, float>(_w.boneIndex3, _w.weight3),
            };

            int at = slots.FindIndex(s => s.Key == _thigh && s.Value > 0f);
            if (at < 0) return false;
            int free = slots.FindIndex(s => s.Value <= 0f);
            if (free < 0)
            {
                Debug.LogWarning("[KneeRigBaker] 영향 슬롯 4개가 다 차 있어 정강이를 못 넣었다");
                return false;
            }

            float w = slots[at].Value;
            slots[at] = new KeyValuePair<int, float>(_thigh, w * (1f - _share));
            slots[free] = new KeyValuePair<int, float>(_shin, w * _share);
            slots.Sort((a, b) => b.Value.CompareTo(a.Value));

            _w.boneIndex0 = slots[0].Value > 0f ? slots[0].Key : 0; _w.weight0 = slots[0].Value;
            _w.boneIndex1 = slots[1].Value > 0f ? slots[1].Key : 0; _w.weight1 = slots[1].Value;
            _w.boneIndex2 = slots[2].Value > 0f ? slots[2].Key : 0; _w.weight2 = slots[2].Value;
            _w.boneIndex3 = slots[3].Value > 0f ? slots[3].Key : 0; _w.weight3 = slots[3].Value;
            return true;
        }

        private static bool SamePose(Transform _a, Transform _b)
        {
            // 팩 리그는 뼈 위치가 1/100 단위라 절대 오차로 재면 무의미하다 — 크기 대비로 잰다.
            float posTol = POSE_EPSILON * Mathf.Max(_a.localPosition.magnitude, 1e-6f);
            float scaleTol = POSE_EPSILON * Mathf.Max(_a.localScale.magnitude, 1e-6f);
            return (_a.localPosition - _b.localPosition).magnitude <= posTol
                && Quaternion.Angle(_a.localRotation, _b.localRotation) <= 0.05f
                && (_a.localScale - _b.localScale).magnitude <= scaleTol;
        }

        private static Transform FindDeep(Transform _root, string _name)
        {
            if (_root.name == _name) return _root;
            for (int i = 0; i < _root.childCount; i++)
            {
                Transform found = FindDeep(_root.GetChild(i), _name);
                if (found != null) return found;
            }
            return null;
        }

        [MenuItem("Tools/CTC/Dev/Bake Mouse Knees")]
        public static void BakeIntoPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MOUSE_PREFAB);
            try
            {
                if (Apply(root)) PrefabUtility.SaveAsPrefabAsset(root, MOUSE_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
