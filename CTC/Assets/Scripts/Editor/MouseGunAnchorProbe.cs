using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 쥐의 **앞다리(`leg.F.R`)가 어느 방향으로 뻗어 있는지**를 실제 메시에서 잰다 —
    /// 총을 그 발에 물릴 때 쓸 로컬 위치·회전을 눈대중이 아니라 계산으로 정하기 위해서다.
    /// Tools > CTC > Dev > Probe Mouse Gun Anchor
    ///
    /// 왜 필요한가(2026-09-12): 총은 본의 **자식**이라 로컬 좌표 하나로 붙는다. 그 값이 틀리면
    /// 에러 없이 「발에 꽂힌 총」이 된다 — 화면으로만 알 수 있다. <see cref="EarGeometryProbe"/>가
    /// 귀에 대해 한 것과 같은 방식으로, 본에 매인 정점에서 밑동→발끝 방향을 뽑아
    /// **본 로컬**로 찍는다. 총의 규약(원점=손잡이, +Z=총구, +Y=위)에 맞춰
    ///   +Z ← 밑동→발끝(팔이 뻗은 축)
    ///   +Y ← 몸 기준 위쪽에서 팔 축 성분을 뺀 것
    /// 을 고르면 로컬 회전이 <c>Quaternion.LookRotation(fwd, up)</c> 한 줄로 나온다.
    ///
    /// 리그를 갈아 끼우면 이걸 다시 돌려 <c>MouseGunBaker</c>의 상수를 바꾼다.
    /// </summary>
    public static class MouseGunAnchorProbe
    {
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";

        private const string MOUSE_LOD0 =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Models/Mouse_LOD0.fbx";

        private const string CLIPS_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";

        private const string GUN_BONE = "leg.F.R";

        /// <summary>발(파지 지점) 뭉치로 볼 정점의 기준 — 밑동에서 이 비율보다 먼 것들.</summary>
        private const float PAW_CLUSTER_RATIO = 0.8f;

        /// <summary>
        /// 실측한 발 뭉치 중심(본 로컬). <see cref="ProbeSkin"/>이 찍는 값을 그대로 옮겨 적었다 —
        /// 포즈 구간에서 「발이 지금 어디 있나」를 같이 찍으려면 상수가 하나 필요하다.
        /// </summary>
        private static readonly Vector3 PawLocal = new Vector3(0f, 0.0018f, 0f);

        private static string ReportPath => Path.Combine(Path.GetTempPath(), "ctc-mouse-gun-anchor.txt");

        [MenuItem("Tools/CTC/Dev/Probe Mouse Gun Anchor")]
        public static void Probe()
        {
            var sb = new StringBuilder();
            ProbeHierarchy(sb);
            ProbeSkin(sb);
            ProbePose(sb);

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[MouseGunAnchorProbe] 완료 → {ReportPath}\n{sb}");
        }

        /// <summary>프리팹에서 본의 **경로**를 확인한다 — 추측한 경로로 배선하면 조용히 빈다.</summary>
        private static void ProbeHierarchy(StringBuilder _sb)
        {
            _sb.AppendLine($"=== 본 경로 — {MOUSE_PREFAB} ===");

            GameObject root = PrefabUtility.LoadPrefabContents(MOUSE_PREFAB);
            if (root == null) { _sb.AppendLine("  프리팹 없음"); return; }

            try
            {
                Transform bone = FindDeep(root.transform, GUN_BONE);
                if (bone == null) { _sb.AppendLine($"  {GUN_BONE} 없음"); return; }

                _sb.AppendLine($"  경로 = {AnimationUtility.CalculateTransformPath(bone, root.transform)}");
                _sb.AppendLine($"  본 로컬 스케일 누적(루트→본) = {F(LossyOf(root.transform, bone))}");

                // 본 로컬축이 **몸 기준**으로 어디를 보는지 — 총의 +Y(위)를 고를 때 쓴다.
                _sb.AppendLine($"  본 로컬축 → 몸 로컬: " +
                               $"right={F(root.transform.InverseTransformDirection(bone.right))} " +
                               $"up={F(root.transform.InverseTransformDirection(bone.up))} " +
                               $"fwd={F(root.transform.InverseTransformDirection(bone.forward))}");
                _sb.AppendLine($"  몸의 up  → 본 로컬 = {F(bone.InverseTransformDirection(root.transform.up))}");
                _sb.AppendLine($"  몸의 fwd → 본 로컬 = {F(bone.InverseTransformDirection(root.transform.forward))}");
                _sb.AppendLine($"  본 원점(몸 로컬) = {F(root.transform.InverseTransformPoint(bone.position))}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 스킨에서 앞다리에 매인 정점을 모아 밑동→발끝을 잰다. 바인드포즈 역행렬이 본→메시라
        /// <see cref="EarGeometryProbe"/>와 같은 방식으로 **본 로컬**까지 되돌릴 수 있다.
        /// </summary>
        private static void ProbeSkin(StringBuilder _sb)
        {
            _sb.AppendLine();
            _sb.AppendLine($"=== 스킨 실측 — {Path.GetFileName(MOUSE_LOD0)} :: {GUN_BONE} ===");

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(MOUSE_LOD0);
            SkinnedMeshRenderer smr = go != null ? go.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            Mesh mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null) { _sb.AppendLine("  메시 없음"); return; }

            Transform[] bones = smr.bones;
            int idx = -1;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && bones[i].name == GUN_BONE) { idx = i; break; }
            }
            if (idx < 0) { _sb.AppendLine("  본 없음"); return; }

            Vector3[] verts = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            var owned = new List<Vector3>();
            for (int v = 0; v < verts.Length; v++)
            {
                BoneWeight w = weights[v];
                if ((w.boneIndex0 == idx && w.weight0 > 0.5f) ||
                    (w.boneIndex1 == idx && w.weight1 > 0.5f)) owned.Add(verts[v]);
            }
            if (owned.Count == 0) { _sb.AppendLine("  매인 정점 0"); return; }

            Matrix4x4 boneToMesh = mesh.bindposes[idx].inverse;
            Vector3 baseMesh = boneToMesh.MultiplyPoint3x4(Vector3.zero);

            Vector3 tip = owned[0];
            float far = 0f;
            foreach (Vector3 p in owned)
            {
                float d = (p - baseMesh).sqrMagnitude;
                if (d > far) { far = d; tip = p; }
            }
            float length = Mathf.Sqrt(far);

            // 메시 공간 방향을 본 로컬로 — 바인드포즈(메시→본)가 그대로 그 변환이다.
            Matrix4x4 meshToBone = mesh.bindposes[idx];
            Vector3 outLocal = meshToBone.MultiplyVector(tip - baseMesh).normalized;
            Vector3 tipLocal = meshToBone.MultiplyPoint3x4(tip);

            _sb.AppendLine($"  매인 정점 {owned.Count}");
            _sb.AppendLine($"  밑동→발끝 길이(메시 단위) = {length:F5}");
            _sb.AppendLine($"  밑동→발끝(본 로컬 방향) = {F(outLocal)}");
            _sb.AppendLine($"  발끝(본 로컬 위치) = {F(tipLocal)}   |발끝| = {tipLocal.magnitude:F5}");

            // 발끝 한 점이 아니라 **발 뭉치의 중심**이 파지 지점이다 — 끝점에 쥐면 총이 발 밖에
            // 떠 있는 것으로 보인다. 밑동에서 먼 쪽 20%를 발로 보고 그 중심을 쓴다.
            Vector3 pawSum = Vector3.zero;
            int pawCount = 0;
            foreach (Vector3 p in owned)
            {
                if ((p - baseMesh).magnitude < length * PAW_CLUSTER_RATIO) continue;
                pawSum += meshToBone.MultiplyPoint3x4(p);
                pawCount++;
            }
            if (pawCount > 0)
            {
                Vector3 paw = pawSum / pawCount;
                _sb.AppendLine($"  발 뭉치 중심(본 로컬, 정점 {pawCount}) = {F(paw)}   " +
                               $"월드 = {(paw.magnitude * 100f):F4}m");
            }

            // 팩 프리팹의 실제 월드 스케일(= 본 로컬 1단위가 몇 미터인가)을 같이 낸다.
            // 총은 미터로 구워졌으므로 이 배율을 모르면 위치를 미터로 못 적는다.
            GameObject prefab = PrefabUtility.LoadPrefabContents(MOUSE_PREFAB);
            if (prefab != null)
            {
                try
                {
                    Transform bone = FindDeep(prefab.transform, GUN_BONE);
                    if (bone != null)
                    {
                        Vector3 lossy = bone.lossyScale;
                        _sb.AppendLine($"  본 월드 스케일(쥐 프리팹) = {F(lossy)}");
                        _sb.AppendLine($"  → 발끝까지 월드 거리(m) ≈ " +
                                       $"{(tipLocal.magnitude * lossy.x):F4}");
                        _sb.AppendLine($"  → 총 전장 0.22m를 본 로컬로 환산 = {(0.22f / lossy.x):F4}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
        }

        /// <summary>
        /// 사격 클립을 돌려 **쏘는 순간 앞다리가 어디를 보는지** 잰다.
        ///
        /// 이게 있어야 총의 로컬 회전이 계산이 된다. 총은 본의 자식이라
        /// <c>본회전 × 총로컬회전 = 총월드회전</c>인데, 「쏘는 순간 총구(+Z)가 몸 정면을 봐야 한다」는
        /// 것이 우리가 아는 유일한 요구다 — 그러면 <c>총로컬회전 = 본회전⁻¹</c>(몸 기준)이 바로 답이다.
        /// 눈대중으로 오일러를 넣어 보는 대신 **원하는 결과에서 거꾸로 푼다.**
        ///
        /// 본 회전은 코드로 돌려도 정확히 바뀐다(스킨드 메시만 안 따라온다 — 여기선 상관없다).
        /// </summary>
        private static void ProbePose(StringBuilder _sb)
        {
            _sb.AppendLine();
            _sb.AppendLine("=== 사격 포즈 — 본 회전(몸 기준) ===");

            GameObject root = PrefabUtility.LoadPrefabContents(MOUSE_PREFAB);
            if (root == null) { _sb.AppendLine("  프리팹 없음"); return; }

            try
            {
                Transform bone = FindDeep(root.transform, GUN_BONE);
                if (bone == null) { _sb.AppendLine($"  {GUN_BONE} 없음"); return; }

                // **샘플 기준은 프리팹 루트가 아니라 Animator가 달린 오브젝트다.** 클립의 커브
                // 경로(`Rig/root/body/leg.F.R`)는 FBX 루트 기준이라, PlayerMouse 루트에 대고
                // 샘플하면 경로가 하나도 안 맞아 **에러 없이 아무 일도 안 일어난다**
                // (2026-09-12에 실제로 그렇게 나왔다 — 11프레임이 전부 같은 값이었다).
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) { _sb.AppendLine("  Animator 없음"); return; }
                GameObject sampleRoot = animator.gameObject;
                Transform body = sampleRoot.transform;
                _sb.AppendLine($"  샘플 기준 = {AnimationUtility.CalculateTransformPath(body, root.transform)}");

                foreach (string clipName in new[] { "Shoot", "ShootDraw" })
                {
                    AnimationClip clip = ClipParityProbe.FindClip(CLIPS_FBX, clipName);
                    if (clip == null) { _sb.AppendLine($"  {clipName}: 클립 없음"); continue; }

                    _sb.AppendLine($"  -- {clipName} (길이 {clip.length:F3}s) --");
                    for (int f = 0; f <= 10; f++)
                    {
                        clip.SampleAnimation(sampleRoot, clip.length * f / 10f);

                        // 본 → 몸 회전. 총 로컬회전은 이것의 역이 된다(총구가 몸 정면을 보게).
                        Quaternion boneInBody = Quaternion.Inverse(body.rotation) * bone.rotation;
                        Vector3 wanted = Quaternion.Inverse(boneInBody).eulerAngles;

                        _sb.AppendLine(
                            $"    bf{f + 1:D2}  본회전(몸기준)={FE(boneInBody.eulerAngles)}  " +
                            $"발끝(몸기준)={F(body.InverseTransformPoint(bone.TransformPoint(PawLocal)))}  " +
                            $"→ 총로컬회전={FE(wanted)}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Vector3 LossyOf(Transform _root, Transform _t)
        {
            Vector3 s = Vector3.one;
            Transform cur = _t;
            while (cur != null && cur != _root.parent)
            {
                s = new Vector3(s.x * cur.localScale.x, s.y * cur.localScale.y, s.z * cur.localScale.z);
                cur = cur.parent;
            }
            return s;
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

        private static string F(Vector3 _v) => $"({_v.x:F4}, {_v.y:F4}, {_v.z:F4})";

        /// <summary>오일러는 소수 둘이면 충분하다 — 그대로 상수로 옮겨 적을 값이라 짧아야 읽힌다.</summary>
        private static string FE(Vector3 _v) => $"({_v.x:F2}, {_v.y:F2}, {_v.z:F2})";
    }
}
