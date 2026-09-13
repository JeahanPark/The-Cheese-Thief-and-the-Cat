using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 팩 캐릭터 메시에 **한쪽 눈만 움직이는 시선 셰이프키 넷**을 더한 파생 메시를 굽는다(2026-09-13).
    ///
    /// **왜 필요한가**: 팩의 <c>eyes.lookIn</c>/<c>eyes.lookOut</c>은 두 눈을 **거울 대칭으로 묶어**
    /// 움직인다(좌우 49정점씩, x 부호가 반대). 그래서 「왼눈은 앞, 오른눈은 뒤」 같은
    /// **옆 목표를 쫓는 시선**을 만들 수 없었다. 인게임에서 재 보니 조준 yaw +75도에서
    /// 눈마다 **49.3도씩** 어긋났고, 사람의 조준은 카메라 정면이라 이 각도는 흔하다.
    ///
    /// **새로 그리지 않고 쪼갠다.** 기존 키의 정점 변위를 좌/우로 갈라
    /// <c>eyes.lookIn.R</c>·<c>eyes.lookIn.L</c>·<c>eyes.lookOut.R</c>·<c>eyes.lookOut.L</c>를 만든다.
    /// 블렌더로 먼저 확인했다: 쪼갠 키가 제 쪽 눈만 44~46mm 움직이고 반대쪽은 **정확히 0**이다.
    ///
    /// **팩 FBX는 건드리지 않는다.** 파생 메시를 에셋으로 따로 두고 프리팹의 렌더러만 그쪽을 본다 —
    /// 팩을 다시 받아도 이 굽기만 다시 돌리면 된다.
    ///
    /// **어느 쪽이 오른눈인가는 추측하지 않는다.** FBX 임포트가 축을 바꾸므로 메시 공간 x 부호는
    /// 믿을 수 없다. 대신 <c>head</c> 본의 **바인드포즈**로 정점을 머리 본 로컬로 옮겨 x를 본다 —
    /// <see cref="CTC.Runtime.Player.EyeLook"/>이 오른눈을 머리 본 로컬 +x로 두는 것과 같은 틀이다.
    /// </summary>
    public static class EyeSplitMeshBaker
    {
        private const string OUTPUT_FOLDER = "Assets/Models/Characters";
        private const string HEAD_BONE = "head";

        /// <summary>
        /// 이 비율(메시 크기 대비)보다 **위치 변위가 작은** 정점은 「안 움직인다」로 보고 변위를 0으로 누른다.
        /// 눈 셰이프키의 실제 변위는 메시 크기의 2%대라(2026-09-13 실측 lookIn 2.23%) 10만분의 1은
        /// 여유가 넉넉하다 — 움직이는 정점은 절대 안 걸리고, 잡음만 걸린다.
        /// </summary>
        private const float NOISE_RATIO = 1e-5f;

        /// <summary>쪼갤 원본 키 — 수평 시선 둘. 위아래(lookUp/lookDown)는 이미 두 눈이 같은 쪽으로 가서 쪼갤 필요가 없다.</summary>
        private static readonly string[] SPLIT_SOURCES = { "eyes.lookIn", "eyes.lookOut" };

        /// <summary>쪼갠 키 이름 규약 — <see cref="CTC.Runtime.Player.EyeBlendShapes"/>가 같은 이름을 찾는다.</summary>
        public static string SplitName(string _source, bool _right) => _source + (_right ? ".R" : ".L");

        /// <summary>
        /// 모델 계층의 스킨드 메시를 전부 파생 메시로 갈아 끼운다. <c>PlayerPrefabFactory</c>가
        /// 모델을 세운 직후에 부른다 — 씬 재생성이 매번 프리팹을 새로 굽기 때문이다.
        /// </summary>
        public static void Apply(GameObject _model)
        {
            if (_model == null) return;

            // **LOD마다 메시가 따로다**(팩 프리팹에 LODGroup + Mesh_LOD0..3). 넷 다 굽는다 —
            // 하나만 구우면 멀어져서 LOD가 바뀌는 순간 **눈이 멈춘다**(2026-09-13 실측 전에 놓쳤던 자리).
            foreach (SkinnedMeshRenderer skin in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh original = OriginalMeshOf(skin);
                if (original == null) continue;
                if (original.GetBlendShapeIndex(SPLIT_SOURCES[0]) < 0) continue;   // 눈 키가 없는 메시(소품 등)

                Mesh split = BakeFor(skin, original);
                if (split != null) skin.sharedMesh = split;
            }
        }

        /// <summary>
        /// 이 렌더러의 **팩 원본 메시**. 이름으로 찾지 않는다 — 팩 FBX의 메시 이름이 LOD 넷 모두
        /// **그냥 "Mesh"**라 이름으로 찾으면 쥐·고양이·LOD가 한 파일로 겹친다(2026-09-13에 실제로 겹쳤다).
        /// 대신 이 렌더러가 온 **원본 프리팹의 같은 렌더러**를 따라가 그 메시를 쓴다 — 인스턴스가
        /// 이미 파생 메시를 물고 있어도 원본은 그대로라 쪼갠 키를 두 번 붙이지 않는다.
        /// </summary>
        private static Mesh OriginalMeshOf(SkinnedMeshRenderer _skin)
        {
            SkinnedMeshRenderer source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(_skin);
            if (source != null && source != _skin && source.sharedMesh != null) return source.sharedMesh;

            // 프리팹 인스턴스가 아니면 지금 메시를 원본으로 삼되, 이미 쪼갠 메시면 굽지 않는다.
            Mesh mesh = _skin.sharedMesh;
            if (mesh == null) return null;
            if (mesh.GetBlendShapeIndex(SplitName(SPLIT_SOURCES[0], true)) >= 0)
            {
                Debug.LogWarning($"[EyeSplitMeshBaker] '{_skin.name}'은 프리팹 인스턴스가 아니고 이미 쪼갠 메시를 물고 있다 — 건너뛴다");
                return null;
            }
            return mesh;
        }

        /// <summary>한 렌더러의 메시에서 파생 메시를 굽는다(있으면 내용만 갈아 끼워 GUID를 지킨다).</summary>
        private static Mesh BakeFor(SkinnedMeshRenderer _skin, Mesh original)
        {
            int head = HeadBoneIndex(_skin);
            if (head < 0)
            {
                Debug.LogError($"[EyeSplitMeshBaker] '{HEAD_BONE}' 본이 없다 — 좌우를 가를 수 없어 굽지 않는다: {original.name}");
                return null;
            }
            Matrix4x4 toHead = original.bindposes[head];

            // 이름은 메시가 아니라 **원본 FBX 파일**에서 딴다 — 메시 이름은 LOD 넷 다 "Mesh"다.
            string sourceName = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(original));
            if (string.IsNullOrEmpty(sourceName)) sourceName = original.name;
            string assetName = "Mesh_" + sourceName + "_EyeSplit";

            Mesh baked = Object.Instantiate(original);
            baked.name = assetName;   // 파일 이름과 같게 — 다르면 유니티가 「Main Object Name」 경고를 낸다

            Vector3[] rest = original.vertices;
            int vertexCount = rest.Length;
            var dv = new Vector3[vertexCount];
            var dn = new Vector3[vertexCount];
            var dt = new Vector3[vertexCount];
            int splitCount = 0;

            // **원본 셰이프키를 전부 다시 담되, 안 움직이는 정점은 0으로 눌러 저장에서 뺀다**(2026-09-13).
            // 그냥 복제하면 셰이프마다 정점이 거의 다 저장돼 파생 메시 8개가 **54MB**였다(레포 전체가 24MB).
            // 유니티는 변위가 정확히 0인 정점만 뺀다 — 임포트가 계산한 노멀·탄젠트 변위에 부동소수 잡음이
            // 섞여 거의 모든 정점이 「움직인 것」으로 남았다.
            //
            // **키를 버리지는 않는다.** 팩 컨트롤러의 Shapekey 레이어가 기본으로 `Eyes_Annoyed`를 틀어
            // `eyes.annoyed`를 매 프레임 100으로 쓴다 — 지금 게임 속 기본 얼굴이 그것이다.
            // 안 쓰는 줄 알고 표정 키를 지우면 **모든 캐릭터 얼굴이 조용히 바뀐다.**
            float eps = Mathf.Max(1e-9f, original.bounds.size.magnitude * NOISE_RATIO);
            baked.ClearBlendShapes();
            for (int shape = 0; shape < original.blendShapeCount; shape++)
            {
                string shapeName = original.GetBlendShapeName(shape);
                for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
                {
                    original.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt);
                    SnapNoise(dv, dn, dt, eps);
                    baked.AddBlendShapeFrame(shapeName, original.GetBlendShapeFrameWeight(shape, frame), dv, dn, dt);
                }
            }

            foreach (string name in SPLIT_SOURCES)
            {
                int index = original.GetBlendShapeIndex(name);
                if (index < 0)
                {
                    Debug.LogError($"[EyeSplitMeshBaker] 원본 키가 없다: {original.name} :: {name}");
                    Object.DestroyImmediate(baked);
                    return null;
                }

                // 팩 키는 프레임 하나다(가중치 100). 여러 프레임이면 첫 것만 쪼갠다 — 그런 팩이 오면 여기를 고친다.
                int frame = original.GetBlendShapeFrameCount(index) - 1;
                float weight = original.GetBlendShapeFrameWeight(index, frame);
                original.GetBlendShapeFrameVertices(index, frame, dv, dn, dt);
                SnapNoise(dv, dn, dt, eps);   // 원본을 담을 때와 같은 잣대 — 쪼갠 키만 부풀면 안 된다

                foreach (bool right in new[] { true, false })
                {
                    var sv = new Vector3[vertexCount];
                    var sn = new Vector3[vertexCount];
                    var st = new Vector3[vertexCount];
                    int moved = 0;
                    for (int i = 0; i < vertexCount; i++)
                    {
                        if (dv[i].sqrMagnitude <= 1e-14f) continue;   // 이 키가 안 움직이는 정점
                        float headX = toHead.MultiplyPoint3x4(rest[i]).x;
                        if ((headX > 0f) != right) continue;
                        sv[i] = dv[i];
                        sn[i] = dn[i];
                        st[i] = dt[i];
                        moved++;
                    }

                    if (moved == 0)
                    {
                        // 한쪽이 비면 좌우 판정이 틀린 것이다 — 조용히 빈 키를 굽지 않는다.
                        Debug.LogError($"[EyeSplitMeshBaker] {name} {(right ? "오른" : "왼")}쪽 정점이 0개다 — " +
                                       $"머리 본 로컬 x로 가르지 못했다: {original.name}");
                        Object.DestroyImmediate(baked);
                        return null;
                    }

                    baked.AddBlendShapeFrame(SplitName(name, right), weight, sv, sn, st);
                    splitCount++;
                }
            }

            Mesh saved = Save(baked, OUTPUT_FOLDER + "/" + assetName + ".asset");
            Debug.Log($"[EyeSplitMeshBaker] {sourceName} → {saved.name} (쪼갠 키 {splitCount}개, 총 {saved.blendShapeCount}개)");
            return saved;
        }

        /// <summary>
        /// 위치 변위가 잡음 수준인 정점은 위치·노멀·탄젠트 변위를 **전부 0으로** 누른다 — 그래야 유니티가
        /// 그 정점을 저장에서 뺀다. 판정은 **위치로만** 한다: 움직이지 않는 정점의 노멀 변위는 이웃이
        /// 움직여서 생긴 계산값이라, 그걸 기준으로 삼으면 거의 모든 정점이 다시 남는다.
        /// </summary>
        private static void SnapNoise(Vector3[] _v, Vector3[] _n, Vector3[] _t, float _eps)
        {
            float eps2 = _eps * _eps;
            for (int i = 0; i < _v.Length; i++)
            {
                if (_v[i].sqrMagnitude > eps2) continue;
                _v[i] = Vector3.zero;
                _n[i] = Vector3.zero;
                _t[i] = Vector3.zero;
            }
        }

        private static int HeadBoneIndex(SkinnedMeshRenderer _skin)
        {
            Transform[] bones = _skin.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && bones[i].name == HEAD_BONE) return i;
            }
            return -1;
        }

        /// <summary>
        /// 저장한다. 이미 있으면 **내용만 갈아 끼워 GUID를 지킨다** — 새로 만들어 덮으면 프리팹 참조가 끊긴다.
        /// <c>EditorUtility.CopySerialized</c>는 쓰지 않는다(CLAUDE.md 함정 4: 디스크는 새 것이 되는데
        /// **그리는 버퍼는 옛 것**이 남는다). 스킨드 메시라 뼈 가중치·바인드포즈·셰이프키까지 전부 옮긴다.
        /// </summary>
        private static Mesh Save(Mesh _baked, string _path)
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets/Models", "Characters");
            }

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(_path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(_baked, _path);
                return _baked;
            }

            CopyInto(_baked, existing);
            existing.name = _baked.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(_baked);
            return existing;
        }

        private static void CopyInto(Mesh _src, Mesh _dst)
        {
            _dst.Clear();
            _dst.indexFormat = _src.indexFormat;
            _dst.SetVertices(_src.vertices);
            _dst.SetNormals(_src.normals);
            _dst.SetTangents(_src.tangents);
            _dst.SetColors(_src.colors);
            var uv = new List<Vector4>();
            for (int channel = 0; channel < 8; channel++)
            {
                uv.Clear();
                _src.GetUVs(channel, uv);
                if (uv.Count > 0) _dst.SetUVs(channel, uv);
            }

            _dst.subMeshCount = _src.subMeshCount;
            for (int s = 0; s < _src.subMeshCount; s++)
            {
                _dst.SetIndices(_src.GetIndices(s), _src.GetTopology(s), s);
            }

            _dst.bindposes = _src.bindposes;
            _dst.boneWeights = _src.boneWeights;

            _dst.ClearBlendShapes();
            int n = _src.vertexCount;
            var v = new Vector3[n];
            var nr = new Vector3[n];
            var t = new Vector3[n];
            for (int shape = 0; shape < _src.blendShapeCount; shape++)
            {
                string name = _src.GetBlendShapeName(shape);
                for (int frame = 0; frame < _src.GetBlendShapeFrameCount(shape); frame++)
                {
                    _src.GetBlendShapeFrameVertices(shape, frame, v, nr, t);
                    _dst.AddBlendShapeFrame(name, _src.GetBlendShapeFrameWeight(shape, frame), v, nr, t);
                }
            }

            _dst.bounds = _src.bounds;
        }

        [MenuItem("Tools/CTC/Dev/Bake Eye Split Meshes")]
        public static void BakeFromPrefabs()
        {
            foreach (string path in new[] { "Assets/Prefabs/PlayerMouse.prefab", "Assets/Prefabs/PlayerCat.prefab" })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Apply(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[EyeSplitMeshBaker] 쥐·고양이 프리팹에 파생 메시를 물렸다");
        }
    }
}
