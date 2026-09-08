using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 귀(`ear.L`·`ear.R`)가 **어느 방향으로 뻗어 있는지**를 실제 메시에서 잰다.
    ///
    /// 왜 필요한가(2026-09-08): 귀를 「몸의 정면 축 둘레로」 돌렸더니 반대쪽 귀가 머리를 가로질러
    /// **접혀서** 부러진 것처럼 보였다. 귀는 접히는 게 아니라 **제 밑동에서 좌우로 도는(swivel)**
    /// 것이라, 돌릴 축을 눈대중이 아니라 지오메트리에서 뽑아야 한다.
    ///
    /// 재는 것: 귀 본에 매인 정점들의 밑동(본 원점)→끝(가장 먼 정점) 방향을
    /// **몸 로컬**과 **귀 본 로컬** 양쪽으로 찍는다. 본 로컬 축(right/up/forward)도 같이 낸다.
    /// Tools > CTC > Dev > Probe Ear Geometry
    /// </summary>
    public static class EarGeometryProbe
    {
        private static readonly string[] MODELS =
        {
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Models/Mouse_LOD0.fbx",
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Models/Cat_LOD0.fbx",
        };

        private static readonly string[] EARS = { "ear.R", "ear.L" };

        private static string ReportPath => Path.Combine(Path.GetTempPath(), "ctc-ear-probe.txt");

        [MenuItem("Tools/CTC/Dev/Probe Ear Geometry")]
        public static void Probe()
        {
            var sb = new StringBuilder();
            foreach (string path in MODELS) ProbeOne(path, sb);

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[EarGeometryProbe] 완료 → {ReportPath}\n{sb}");
        }

        private static void ProbeOne(string _modelPath, StringBuilder _sb)
        {
            _sb.AppendLine($"=== {Path.GetFileName(_modelPath)} ===");

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(_modelPath);
            SkinnedMeshRenderer smr = go != null ? go.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            Mesh mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null) { _sb.AppendLine("  메시 없음"); return; }

            Transform root = go.transform;
            Transform[] bones = smr.bones;
            Vector3[] verts = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;

            foreach (string earName in EARS)
            {
                int idx = -1;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null && bones[i].name == earName) { idx = i; break; }
                }
                if (idx < 0) { _sb.AppendLine($"  {earName}: 본 없음"); continue; }

                Transform ear = bones[idx];
                var owned = new List<Vector3>();
                for (int v = 0; v < verts.Length; v++)
                {
                    BoneWeight w = weights[v];
                    if ((w.boneIndex0 == idx && w.weight0 > 0.5f) ||
                        (w.boneIndex1 == idx && w.weight1 > 0.5f)) owned.Add(verts[v]);
                }
                if (owned.Count == 0) { _sb.AppendLine($"  {earName}: 매인 정점 0"); continue; }

                // 본 원점(밑동)을 메시 공간으로 — 바인드포즈의 역이 본→메시다.
                Matrix4x4 boneToMesh = mesh.bindposes[idx].inverse;
                Vector3 baseMesh = boneToMesh.MultiplyPoint3x4(Vector3.zero);

                Vector3 centroid = Vector3.zero;
                Vector3 tip = owned[0];
                float far = 0f;
                foreach (Vector3 p in owned)
                {
                    centroid += p;
                    float d = (p - baseMesh).sqrMagnitude;
                    if (d > far) { far = d; tip = p; }
                }
                centroid /= owned.Count;

                Vector3 outward = (tip - baseMesh).normalized;         // 밑동 → 끝 (메시 = 모델 로컬)
                Vector3 rootBase = root.InverseTransformPoint(smr.transform.TransformPoint(baseMesh));

                _sb.AppendLine($"  {earName}: 정점 {owned.Count}");
                _sb.AppendLine($"    밑동(모델 로컬) = {F(rootBase)}   길이 = {Mathf.Sqrt(far):F4}");
                _sb.AppendLine($"    밑동→끝(모델 로컬) = {F(outward)}");
                _sb.AppendLine($"    중심-밑동(모델 로컬) = {F((centroid - baseMesh).normalized)}");
                _sb.AppendLine($"    본 로컬축 → 모델 로컬: " +
                               $"right={F(root.InverseTransformDirection(ear.right))} " +
                               $"up={F(root.InverseTransformDirection(ear.up))} " +
                               $"fwd={F(root.InverseTransformDirection(ear.forward))}");
                // 밑동→끝을 본 로컬로 — 어느 로컬 축이 「귀가 뻗은 축」인지 한 줄로 보인다.
                Vector3 localOut = ear.InverseTransformDirection(
                    root.TransformDirection(outward)).normalized;
                _sb.AppendLine($"    밑동→끝(본 로컬) = {F(localOut)}");
            }
        }

        private static string F(Vector3 _v) => $"({_v.x:F3}, {_v.y:F3}, {_v.z:F3})";
    }
}
