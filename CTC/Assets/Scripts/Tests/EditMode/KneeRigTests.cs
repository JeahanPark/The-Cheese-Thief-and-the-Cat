using System.Collections.Generic;
using CTC.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 쥐 뒷다리 무릎(2026-09-13). 전부 **에러 없이 조용히 깨지는** 자리를 붙든다:
    /// 정강이 트랜스폼이 빠지거나(클립 커브가 허공에 꽂힌다), 경로가 한 글자 어긋나거나(Generic은 경로 문자열로
    /// 뼈를 찾는다), 바인드포즈가 트랜스폼과 어긋나거나(쉬는 자세에서 다리가 튄다), 파생 메시를 새로 굽고
    /// 무릎을 안 다시 넣었거나(무릎이 안 굽는다).
    /// </summary>
    public class KneeRigTests
    {
        private const string MOUSE_PREFAB = KneeRigBaker.MOUSE_PREFAB;

        [Test]
        public void ShinShare_HalfAtKnee_FullBelow_NoneAbove()
        {
            Assert.AreEqual(0.5f, KneeWeightLogic.ShinShare(KneeWeightLogic.KNEE), 1e-4f);
            Assert.AreEqual(0f, KneeWeightLogic.ShinShare(0f), 1e-6f, "허벅지 머리는 정강이 몫이 없다");
            Assert.AreEqual(0f, KneeWeightLogic.ShinShare(KneeWeightLogic.KNEE - KneeWeightLogic.HALF), 1e-6f);
            Assert.AreEqual(1f, KneeWeightLogic.ShinShare(KneeWeightLogic.KNEE + KneeWeightLogic.HALF), 1e-6f);
            Assert.AreEqual(1f, KneeWeightLogic.ShinShare(1f), 1e-6f, "발목 끝은 정강이 전부");
        }

        [Test]
        public void ShinShare_IsMonotonic()
        {
            float prev = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float v = KneeWeightLogic.ShinShare(i / 100f);
                Assert.GreaterOrEqual(v, prev);
                prev = v;
            }
        }

        [Test]
        public void AxisParam_KneeIsKnee_ScaleFree()
        {
            var knee = new Vector3(0f, 0.0009f, 0f);   // 팩 리그처럼 1/100 단위
            Assert.AreEqual(KneeWeightLogic.KNEE, KneeWeightLogic.AxisParam(knee, knee), 1e-4f);
            Assert.AreEqual(0f, KneeWeightLogic.AxisParam(Vector3.zero, knee), 1e-6f);
            Assert.AreEqual(1f, KneeWeightLogic.AxisParam(knee / KneeWeightLogic.KNEE, knee), 1e-4f);
        }

        [Test]
        public void Split_MovesShareOfThighOnly_KeepsSum()
        {
            var w = new BoneWeight { boneIndex0 = 3, weight0 = 0.8f, boneIndex1 = 7, weight1 = 0.2f };
            Assert.IsTrue(KneeRigBaker.Split(ref w, 3, 12, 0.5f));
            var got = new Dictionary<int, float>();
            foreach (var (i, x) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                if (x > 0f) got[i] = x;
            Assert.AreEqual(0.4f, got[3], 1e-5f);
            Assert.AreEqual(0.4f, got[12], 1e-5f);
            Assert.AreEqual(0.2f, got[7], 1e-5f);
            Assert.GreaterOrEqual(w.weight0, w.weight1, "가중치는 큰 순서로 담는다");
        }

        [Test]
        public void Split_IgnoresVertexWithoutThigh()
        {
            var w = new BoneWeight { boneIndex0 = 1, weight0 = 1f };
            Assert.IsFalse(KneeRigBaker.Split(ref w, 3, 12, 1f));
            Assert.AreEqual(1f, w.weight0);
        }

        // ===== 구운 프리팹 =====

        private static GameObject LoadMouse()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MOUSE_PREFAB);
            if (prefab == null) Assert.Fail($"프리팹이 없다: {MOUSE_PREFAB}");
            return prefab;
        }

        private static Transform FindDeep(Transform _root, string _name)
        {
            if (_root.name == _name) return _root;
            foreach (Transform c in _root)
            {
                Transform f = FindDeep(c, _name);
                if (f != null) return f;
            }
            return null;
        }

        [Test]
        [NUnit.Framework.Category("Asset")]
        public void MousePrefab_HasShinUnderEachThigh()
        {
            GameObject prefab = LoadMouse();
            foreach (string side in KneeRigBaker.SIDES)
            {
                Transform thigh = FindDeep(prefab.transform, KneeRigBaker.ThighName(side));
                Assert.IsNotNull(thigh, KneeRigBaker.ThighName(side));
                Assert.IsNotNull(thigh.Find(KneeRigBaker.ShinName(side)),
                    $"'{KneeRigBaker.ShinName(side)}'가 허벅지 밑에 없다 — 클립의 무릎 커브가 허공에 꽂힌다. Tools/CTC/Dev/Bake Mouse Knees");
            }
        }

        /// <summary>
        /// **LOD 넷 전부** 정강이를 뼈로 갖고, 바인드포즈가 트랜스폼과 맞물려야 한다 —
        /// 쉬는 자세에서 정강이의 (월드←로컬 × 바인드)가 허벅지의 것과 같으면 무릎을 안 굽힌 클립은 정점이 그대로다.
        /// </summary>
        [Test]
        [NUnit.Framework.Category("Asset")]
        public void MousePrefab_EveryLod_SkinsShinConsistentlyWithThigh()
        {
            GameObject prefab = LoadMouse();
            int checkedRenderers = 0;
            foreach (SkinnedMeshRenderer skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] bones = skin.bones;
                int thighL = System.Array.FindIndex(bones, b => b != null && b.name == "leg.B.L");
                if (thighL < 0) continue;   // 총 등
                checkedRenderers++;

                Mesh mesh = skin.sharedMesh;
                Assert.AreEqual(bones.Length, mesh.bindposes.Length, $"'{skin.name}' 뼈 수와 바인드포즈 수가 다르다");

                foreach (string side in KneeRigBaker.SIDES)
                {
                    int thigh = System.Array.FindIndex(bones, b => b != null && b.name == KneeRigBaker.ThighName(side));
                    int shin = System.Array.FindIndex(bones, b => b != null && b.name == KneeRigBaker.ShinName(side));
                    Assert.GreaterOrEqual(shin, 0, $"'{skin.name}'(메시 {mesh.name})에 정강이 뼈가 없다 — 이 LOD에서 무릎이 안 굽는다");

                    Matrix4x4 a = bones[thigh].localToWorldMatrix * mesh.bindposes[thigh];
                    Matrix4x4 b = bones[shin].localToWorldMatrix * mesh.bindposes[shin];
                    for (int i = 0; i < 16; i++)
                        Assert.AreEqual(a[i], b[i], 1e-3f * Mathf.Max(1f, Mathf.Abs(a[i])),
                            $"'{skin.name}' {side} 정강이 바인드포즈가 트랜스폼과 어긋난다 — 쉬는 자세에서 다리가 튄다");
                }

                BoneWeight[] weights = mesh.boneWeights;
                int shinVerts = 0;
                int shinIndexL = System.Array.FindIndex(bones, b => b != null && b.name == "shin.B.L");
                foreach (BoneWeight w in weights)
                {
                    Assert.AreEqual(1f, w.weight0 + w.weight1 + w.weight2 + w.weight3, 1e-3f, "가중치 합이 1이 아니다");
                    if ((w.boneIndex0 == shinIndexL && w.weight0 > 0f) || (w.boneIndex1 == shinIndexL && w.weight1 > 0f)
                        || (w.boneIndex2 == shinIndexL && w.weight2 > 0f) || (w.boneIndex3 == shinIndexL && w.weight3 > 0f))
                        shinVerts++;
                }
                Assert.Greater(shinVerts, 0, $"'{skin.name}'에서 정강이에 가중치를 받은 정점이 0개다 — 파생 메시를 새로 굽고 무릎을 안 넣었다");
            }
            Assert.Greater(checkedRenderers, 0, "뒷다리를 가진 렌더러가 하나도 없다");
        }

        /// <summary>
        /// 클립의 무릎 커브 경로가 **모델의 실제 경로와 글자까지 같아야** 한다(Generic은 경로로 찾는다).
        /// </summary>
        [Test]
        [NUnit.Framework.Category("Asset")]
        public void KneeClips_CurvePathsMatchModelHierarchy()
        {
            GameObject prefab = LoadMouse();
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator);
            Transform shin = FindDeep(prefab.transform, "shin.B.L");
            Assert.IsNotNull(shin);
            string modelPath = AnimationUtility.CalculateTransformPath(shin, animator.transform);

            var paths = new HashSet<string>();
            AnimationClip shoot = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(KneeRigBaker.CLIPS_FBX))
            {
                if (o is AnimationClip c && c.name == "Shoot" && (c.hideFlags & HideFlags.HideInHierarchy) == 0) shoot = c;
            }
            Assert.IsNotNull(shoot, "CTC_Mouse_Clips.fbx에 Shoot이 없다");
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(shoot)) paths.Add(b.path);

            CollectionAssert.Contains(paths, modelPath,
                $"Shoot에 모델 경로 '{modelPath}'의 커브가 없다 — 무릎이 조용히 안 굽는다");
        }
    }
}
