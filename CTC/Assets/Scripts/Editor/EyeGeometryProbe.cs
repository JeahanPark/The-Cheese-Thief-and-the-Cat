using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 쥐·고양이 메시에서 "눈"이 어떤 지오메트리인지 캔다.
    /// 팩 텍스처는 8x8 팔레트라 색이 UV로 결정된다 — 팔레트 색별로 정점을 묶고,
    /// 좌우 대칭 덩어리를 찾아 머리 본 로컬 좌표로 찍는다.
    /// Tools > CTC > Dev > Probe Eye Geometry
    /// </summary>
    public static class EyeGeometryProbe
    {
        private static readonly string[] MODELS =
        {
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Models/Mouse_LOD0.fbx",
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Models/Cat_LOD0.fbx",
        };

        private static readonly string[] TEXTURES =
        {
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Textures/T_Mouse.png",
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Textures/T_Cat.png",
        };

        private static string ReportPath => Path.Combine(Path.GetTempPath(), "ctc-eye-probe.txt");

        [MenuItem("Tools/CTC/Dev/Probe Eye Geometry")]
        public static void Probe()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < MODELS.Length; i++)
            {
                ProbeOne(MODELS[i], TEXTURES[i], sb);
            }

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[EyeGeometryProbe] 완료 → {ReportPath}");
        }

        private static void ProbeOne(string _modelPath, string _texPath, StringBuilder _sb)
        {
            _sb.AppendLine($"=== {Path.GetFileName(_modelPath)} ===");

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(_modelPath);
            if (go == null) { _sb.AppendLine("  모델 없음"); return; }

            SkinnedMeshRenderer smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null) { _sb.AppendLine("  SkinnedMeshRenderer 없음"); return; }

            Mesh mesh = smr.sharedMesh;
            if (mesh == null) { _sb.AppendLine("  메시 없음"); return; }

            _sb.AppendLine($"  submesh={mesh.subMeshCount} verts={mesh.vertexCount} bones={mesh.bindposes.Length}");

            Transform[] bones = smr.bones;
            int headIdx = -1;
            for (int i = 0; i < bones.Length; i++)
            {
                _sb.AppendLine($"  bone[{i}] {(bones[i] != null ? bones[i].name : "null")}");
                if (bones[i] != null && bones[i].name == "head") headIdx = i;
            }
            _sb.AppendLine($"  headIdx={headIdx}");

            // 팔레트 색 읽기 (읽기 허용을 임시로 켠다)
            var texImporter = AssetImporter.GetAtPath(_texPath) as TextureImporter;
            bool restore = false;
            if (texImporter != null && !texImporter.isReadable)
            {
                texImporter.isReadable = true;
                texImporter.SaveAndReimport();
                restore = true;
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(_texPath);

            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            Vector2[] uvs = mesh.uv;
            BoneWeight[] bw = mesh.boneWeights;
            Matrix4x4[] bind = mesh.bindposes;

            var groups = new Dictionary<string, List<int>>();
            for (int v = 0; v < verts.Length; v++)
            {
                Color c = tex != null && uvs.Length > v
                    ? tex.GetPixelBilinear(uvs[v].x, uvs[v].y)
                    : Color.magenta;
                string key = ColorUtility.ToHtmlStringRGB(c);
                if (!groups.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>();
                    groups[key] = list;
                }
                list.Add(v);
            }

            foreach (KeyValuePair<string, List<int>> kv in groups)
            {
                _sb.AppendLine($"  색 #{kv.Key}: 정점 {kv.Value.Count}개");
                DumpClusters(kv.Value, verts, norms, bw, bind, bones, headIdx, _sb);
            }

            if (restore && texImporter != null)
            {
                texImporter.isReadable = false;
                texImporter.SaveAndReimport();
            }
            _sb.AppendLine();
        }

        /// <summary>x 부호로 좌/우를 가른 뒤, 머리 본 로컬 좌표의 중심·반경을 찍는다.</summary>
        private static void DumpClusters(List<int> _idx, Vector3[] _verts, Vector3[] _norms, BoneWeight[] _bw,
            Matrix4x4[] _bind, Transform[] _bones, int _headIdx, StringBuilder _sb)
        {
            var byBone = new Dictionary<int, List<int>>();
            for (int i = 0; i < _idx.Count; i++)
            {
                int v = _idx[i];
                int b = _bw.Length > v ? _bw[v].boneIndex0 : 0;
                if (!byBone.TryGetValue(b, out List<int> list))
                {
                    list = new List<int>();
                    byBone[b] = list;
                }
                list.Add(v);
            }

            foreach (KeyValuePair<int, List<int>> kv in byBone)
            {
                string boneName = kv.Key < _bones.Length && _bones[kv.Key] != null ? _bones[kv.Key].name : "?";
                Matrix4x4 toBone = kv.Key < _bind.Length ? _bind[kv.Key] : Matrix4x4.identity;

                // 좌/우/가운데
                for (int side = -1; side <= 1; side++)
                {
                    Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity, sum = Vector3.zero;
                    Vector3 nsum = Vector3.zero;
                    int n = 0;
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        Vector3 p = toBone.MultiplyPoint3x4(_verts[kv.Value[i]]);
                        int s = p.x > 1e-5f ? 1 : (p.x < -1e-5f ? -1 : 0);
                        if (s != side) continue;
                        min = Vector3.Min(min, p); max = Vector3.Max(max, p); sum += p; n++;
                        if (_norms.Length > kv.Value[i]) nsum += toBone.MultiplyVector(_norms[kv.Value[i]]).normalized;
                    }
                    if (n == 0) continue;
                    Vector3 c = sum / n;
                    Vector3 ext = (max - min) * 0.5f;
                    _sb.AppendLine($"    bone={boneName} side={side} n={n} " +
                                   $"center=({c.x:F5},{c.y:F5},{c.z:F5}) half=({ext.x:F5},{ext.y:F5},{ext.z:F5}) " +
                                   $"nrm=({nsum.normalized.x:F3},{nsum.normalized.y:F3},{nsum.normalized.z:F3})");
                }
            }
        }
    }
}
