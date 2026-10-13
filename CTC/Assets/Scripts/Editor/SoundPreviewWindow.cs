using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CTC.EditorTools
{
    /// <summary>
    /// 사운드 청취 창(2026-09-27 사용자 요청). 게임 음원 전부를 한글 이름과 함께 늘어놓고 ▶ 한 번에 듣는다.
    /// 윈도우 기본 플레이어는 0.5초 이하 효과음을 켜지는 사이에 놓쳐서 「소리가 안 난다」로 들렸다.
    /// 재생은 에디터 내부 미리듣기(AudioUtil)를 리플렉션으로 부른다 — 못 찾으면 그 클립을 선택해 인스펙터 ▶로 넘긴다.
    /// </summary>
    public class SoundPreviewWindow : EditorWindow
    {
        private const string ROOT = "Assets/Audio";

        // 파일 이름 → (한글 이름, 언제 나는지). 없는 파일은 이름만 보인다.
        private static readonly Dictionary<string, (string label, string where)> INFO = new Dictionary<string, (string, string)>
        {
            { "bgm_lobby", ("대기실·메뉴 곡", "메인 ~ 대기실") },
            { "bgm_ingame", ("인게임 곡", "게임 씬 내내") },
            { "bgm_chase", ("추격곡", "쥐 · 고양이 20m 안") },
            { "amb_kitchen", ("앰비언스", "판 중 인게임 곡 밑에 작게") },
            { "sfx_mouse_step1", ("쥐 발소리 1", "걸음마다 셋을 번갈아") },
            { "sfx_mouse_step2", ("쥐 발소리 2", "") },
            { "sfx_mouse_step3", ("쥐 발소리 3", "") },
            { "sfx_cat_step1", ("고양이 발소리 1", "걸음마다 셋을 번갈아") },
            { "sfx_cat_step2", ("고양이 발소리 2", "") },
            { "sfx_cat_step3", ("고양이 발소리 3", "") },
            { "sfx_mouse_dash", ("쥐 대시", "Space") },
            { "sfx_popgun_fire", ("딱총", "쥐 기본 총") },
            { "sfx_slingshot_fire", ("새총", "총 1단계") },
            { "sfx_machinegun_fire", ("기관총 한 발", "총 2단계 · 0.1초마다 반복") },
            { "sfx_pounce_swing", ("휘두르기", "덮치기 · 몽둥이 · 칼") },
            { "sfx_mouse_hurt", ("쥐 피격", "") },
            { "sfx_cat_hurt", ("고양이 피격", "") },
            { "sfx_mouse_death", ("쥐 탈락", "") },
            { "sfx_cat_death", ("고양이 쓰러짐", "") },
            { "sfx_cat_skill", ("고양이 전직 스킬", "Q") },
            { "sfx_cat_frenzy", ("분노 발톱", "고양이 Space") },
            { "sfx_cheese_tick", ("치즈 채굴", "벌 때마다") },
            { "sfx_cheese_depleted", ("치즈 지점 고갈", "") },
            { "sfx_shop_buy", ("구매 성공", "") },
            { "sfx_shop_deny", ("구매 거절", "") },
            { "sfx_shop_shutter", ("상점 철문", "고양이 접근") },
            { "sfx_job_pickup", ("직업권 줍기", "") },
            { "sfx_fortress_place", ("요새 설치", "") },
            { "sfx_fortress_hit", ("요새 맞음", "") },
            { "sfx_fortress_break", ("요새 부서짐", "") },
            { "sfx_decoy_place", ("창고 설치", "") },
            { "sfx_warehouse_tick", ("창고 생산", "") },
            { "sfx_heal_tick", ("회복", "") },
            { "sfx_alcove_hit", ("벽감 맞음", "") },
            { "sfx_alcove_break", ("벽감 부서짐", "멀리 들림") },
            { "sfx_box_hit", ("상자 맞음", "") },
            { "sfx_box_break", ("상자 부서짐", "조용히 — 찾은 사람만") },
            { "sfx_ui_click", ("버튼 클릭", "모든 버튼") },
            { "sfx_match_start", ("판 시작", "") },
            { "sfx_timer_warn", ("시간 경고", "") },
            { "sfx_cat_advance", ("고양이 전직", "") },
            { "sfx_gang_rise", ("깡패 등장", "") },
            { "jingle_mouse_win", ("쥐 승리", "결과 화면") },
            { "jingle_cat_win", ("고양이 승리", "결과 화면") },
            { "sfx_dog_bark", ("강아지 짖음", "") },
            { "sfx_dog_bite", ("강아지 물기", "") },
            { "sfx_dog_hurt", ("강아지 피격", "") },
            { "sfx_dog_death", ("강아지 쓰러짐", "") },
            { "sfx_dog_alcove_idle", ("벽 속 강아지", "반복 재생") },
        };

        private static readonly string[] FOLDER_ORDER = { "BGM", "Movement", "Combat", "Economy", "UI", "Ai", "Resources" };
        private static readonly Dictionary<string, string> FOLDER_LABEL = new Dictionary<string, string>
        {
            { "BGM", "배경음" }, { "Movement", "이동" }, { "Combat", "전투" }, { "Economy", "경제·구조물" },
            { "UI", "UI·판 진행" }, { "Ai", "강아지" }, { "Resources", "UI (Resources)" },
        };

        private List<(string folder, AudioClip clip)> m_Clips = new List<(string, AudioClip)>();
        private Vector2 m_Scroll;
        private AudioClip m_Playing;
        private bool m_Loop;

        [MenuItem("Tools/CTC/Audio/사운드 청취 창")]
        public static void Open()
        {
            var w = GetWindow<SoundPreviewWindow>("사운드 청취");
            w.minSize = new Vector2(420f, 360f);
            w.Reload();
        }

        private void OnEnable() => Reload();
        private void OnDisable() => Stop();

        private void Reload()
        {
            m_Clips.Clear();
            var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { ROOT, "Assets/Resources/Audio" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct();
            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string folder = path.StartsWith("Assets/Resources/") ? "Resources" : path.Split('/')[2];
                m_Clips.Add((folder, clip));
            }
            m_Clips = m_Clips.OrderBy(c => FolderRank(c.folder)).ThenBy(c => c.clip.name).ToList();
        }

        private static int FolderRank(string _folder)
        {
            int i = Array.IndexOf(FOLDER_ORDER, _folder);
            return i < 0 ? 99 : i;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("■ 정지", EditorStyles.toolbarButton, GUILayout.Width(60f))) Stop();
                m_Loop = GUILayout.Toggle(m_Loop, "반복", EditorStyles.toolbarButton, GUILayout.Width(50f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70f))) Reload();
            }

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            string folder = null;
            foreach (var (f, clip) in m_Clips)
            {
                if (f != folder)
                {
                    folder = f;
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField(FOLDER_LABEL.TryGetValue(f, out string fl) ? fl : f, EditorStyles.boldLabel);
                }
                INFO.TryGetValue(clip.name, out var info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool playing = m_Playing == clip;
                    if (GUILayout.Button(playing ? "■" : "▶", GUILayout.Width(28f)))
                    {
                        if (playing) Stop(); else Play(clip);
                    }
                    GUILayout.Label(string.IsNullOrEmpty(info.label) ? clip.name : info.label, GUILayout.Width(120f));
                    GUILayout.Label($"{clip.length:0.00}s", EditorStyles.miniLabel, GUILayout.Width(44f));
                    GUILayout.Label(string.IsNullOrEmpty(info.where) ? clip.name : $"{info.where}  ·  {clip.name}", EditorStyles.miniLabel);
                    if (GUILayout.Button("선택", EditorStyles.miniButton, GUILayout.Width(40f)))
                    {
                        Selection.activeObject = clip;
                        EditorGUIUtility.PingObject(clip);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Play(AudioClip _clip)
        {
            Stop();
            if (Invoke("PlayPreviewClip", _clip, 0, m_Loop)) { m_Playing = _clip; return; }
            // 내부 API가 바뀌었으면 선택만 해 준다 — 인스펙터 미리보기의 ▶로 듣는다
            Selection.activeObject = _clip;
            EditorGUIUtility.PingObject(_clip);
            Debug.LogWarning("[사운드 청취] 에디터 미리듣기 API를 못 찾았다 — 인스펙터 아래 ▶로 들어 주세요");
        }

        private void Stop()
        {
            Invoke("StopAllPreviewClips");
            m_Playing = null;
        }

        // UnityEditor.AudioUtil — 공개 API가 아니라 이름·인자 개수로 찾는다
        private static bool Invoke(string _method, params object[] _args)
        {
            Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            MethodInfo m = util?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(x => x.Name == _method && x.GetParameters().Length == _args.Length);
            if (m == null) return false;
            m.Invoke(null, _args);
            return true;
        }
    }
}
