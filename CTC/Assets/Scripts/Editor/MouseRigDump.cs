using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 쥐 리그의 본 경로와 기존 클립이 건드리는 본을 콘솔에 덤프한다.
    /// 인트로 "치즈 치켜들기" 포즈를 짜려면 Avatar Mask와 .anim 커브가 둘 다 본 경로를 요구한다 —
    /// 그 경로를 추측하지 않으려고 만든 도구(2026-08-26).
    /// Tools > CTC > Dev > Dump Mouse Rig
    /// </summary>
    public static class MouseRigDump
    {
        private const string PREFAB_PATH = "Assets/Prefabs/PlayerMouse.prefab";

        private const string ANIM_FBX_PATH =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/Mouse_Animations.fbx";

        private static readonly string[] CLIPS_OF_INTEREST = { "Idle_B", "Eat", "Bounce" };

        /// <summary>콘솔은 긴 글을 잘라 먹는다 — 프로파일러와 같은 방식으로 파일에 쓴다.</summary>
        private static string ReportPath =>
            Path.Combine(Path.GetTempPath(), "ctc-mouse-rig.txt");

        [MenuItem("Tools/CTC/Dev/Dump Mouse Rig")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            DumpBones(sb);
            for (int i = 0; i < CLIPS_OF_INTEREST.Length; i++)
            {
                DumpClipPaths(CLIPS_OF_INTEREST[i], sb);
            }

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[MouseRigDump] 덤프 완료 → {ReportPath}");
        }

        private static void DumpBones(StringBuilder _sb)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[MouseRigDump] 프리팹 없음: {PREFAB_PATH}");
                return;
            }

            _sb.AppendLine($"=== 본 계층 — {PREFAB_PATH} ===");
            Walk(prefab.transform, prefab.transform, _sb);
            _sb.AppendLine();
        }

        private static void Walk(Transform _root, Transform _t, StringBuilder _sb)
        {
            string path = _t == _root ? "(root)" : AnimationUtility.CalculateTransformPath(_t, _root);
            _sb.AppendLine(path);
            for (int i = 0; i < _t.childCount; i++)
            {
                Walk(_root, _t.GetChild(i), _sb);
            }
        }

        /// <summary>클립이 실제로 건드리는 본 경로만 모은다 — 속성별로 다 찍으면 콘솔이 못 읽을 만큼 길어진다.</summary>
        private static void DumpClipPaths(string _clipName, StringBuilder _sb)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ANIM_FBX_PATH);
            for (int i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (clip == null || clip.name != _clipName) continue;

                var paths = new List<string>();
                var seen = new HashSet<string>();
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (seen.Add(binding.path)) paths.Add(binding.path);
                }

                _sb.AppendLine($"=== 클립 '{clip.name}' — 길이 {clip.length:0.00}s · 루프 {clip.isLooping} · 건드리는 본 {paths.Count}개 ===");
                for (int p = 0; p < paths.Count; p++)
                {
                    _sb.AppendLine($"  {paths[p]}");
                }
                _sb.AppendLine();
                return;
            }

            Debug.LogWarning($"[MouseRigDump] 클립 못 찾음: {_clipName} ({ANIM_FBX_PATH})");
        }
    }
}
