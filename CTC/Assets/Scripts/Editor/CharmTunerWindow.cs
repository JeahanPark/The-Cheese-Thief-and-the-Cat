using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CTC.Runtime;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using CTC.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 눈·귀 수치를 **플레이 중에 끌어 보는** 개발용 창(2026-09-10).
    ///
    /// **왜 필요한가.** 이 값들은 「맞는 수」가 없다 — 얼굴을 보고 정하는 것이다.
    /// 그런데 원본이 CSV라 한 번 고칠 때마다 「CSV 편집 → Convert → 재컴파일 → 플레이 →
    /// 판 세우기」를 다 돌아야 했다. 한 값을 다섯 번 만지면 그게 다섯 바퀴다.
    /// 그래서 창이 <see cref="BalanceInfo"/>를 **그 자리에서 덮어쓰고**, 씬에 서 있는 몸마다
    /// <c>DevReloadTuning()</c>을 불러 다음 프레임에 반영한다 — 내 몸·남의 몸·봇 전부.
    ///
    /// **덮어쓰기는 임시다.** 플레이를 끝내면 CSV가 다시 로드돼 원래 값으로 돌아간다.
    /// 마음에 드는 값을 남기려면 「CSV에 저장」을 눌러야 한다 — 그게 안전한 기본값이다.
    ///
    /// 런타임 쪽에 에디터 의존을 만들지 않는다. 창이 런타임의 공개 진입점을 부를 뿐이다.
    /// </summary>
    public class CharmTunerWindow : EditorWindow
    {
        private const string CSV_PATH = "Assets/Resources/CSVInfo/BalanceInfo.csv";
        private const string CONVERT_MENU = "Tools/CTC/Convert CSV → Info";

        /// <summary>슬라이더 하나 — CSV 컬럼 이름이 곧 <see cref="BalanceInfo"/>의 필드 이름이다.</summary>
        private readonly struct Knob
        {
            public readonly string Field;
            public readonly string Label;
            public readonly float Min;
            public readonly float Max;
            public readonly string Hint;

            public Knob(string _field, string _label, float _min, float _max, string _hint)
            {
                Field = _field;
                Label = _label;
                Min = _min;
                Max = _max;
                Hint = _hint;
            }
        }

        private static readonly Knob[] EYE_KNOBS =
        {
            new Knob("EyeMaxAngleDeg", "바깥·위아래 한계(°)", 0f, 90f,
                     "눈동자가 쉬는 자리에서 벗어날 수 있는 각. 크면 흰자 밖으로 나간다."),
            new Knob("EyeInwardPastCenterDeg", "안쪽 — 정면을 지나치는 각(°)", 0f, 90f,
                     "안쪽 눈이 정면을 지나 얼마나 더 가나. 쉬는 각도에 더해진다 — 쥐는 63° 바깥에 쉬므로 " +
                     "여기가 0이어도 안쪽 눈은 63°를 간다. 인중을 넘겨 보고 싶으면 올려라."),
            new Knob("EyeFollowSpeed", "따라가는 속도", 1f, 40f,
                     "지수 감쇠 계수. 크면 딱딱 붙고 작으면 흐물흐물 따라온다."),
            // 크기 손잡이 다섯(EyePupilRatio·EyePupilOffsetRatio·EyeFlatten·EyeScleraScale·
            // EyeInsetRatio)은 2026-09-13에 사라졌다 — 전부 **얹은 구가 얼굴에서 솟지 않게**
            // 누르던 값이라, 메시 자체를 미는 셰이프키 방식에서는 누를 것이 없다.
            // 눈 크기·모양은 이제 팩이 그린 그대로이고 코드가 정하지 않는다.
            new Knob("EyeBlinkSeconds", "깜빡임 길이(초)", 0f, 0.6f, "0이면 안 깜빡인다."),
            new Knob("EyeBlinkMinSeconds", "깜빡임 간격 최소(초)", 0f, 15f, ""),
            new Knob("EyeBlinkMaxSeconds", "깜빡임 간격 최대(초)", 0f, 20f, ""),
        };

        private static readonly Knob[] EAR_KNOBS =
        {
            new Knob("EarTurnMaxDeg", "귀가 소리 쪽으로 도는 최대(°)", 0f, 90f, ""),
            new Knob("EarPerkDeg", "쫑긋 세우는 각(°)", 0f, 90f, ""),
            new Knob("EarPerkDelaySeconds", "쫑긋 지연(초)", 0f, 0.5f,
                     "소리가 난 뒤 귀가 반응하기까지. 0이면 기계처럼 붙는다."),
            new Knob("EarPerkRiseSeconds", "세우는 시간(초)", 0.01f, 0.6f, ""),
            new Knob("EarPerkFallSeconds", "내리는 시간(초)", 0.01f, 1.5f, ""),
            new Knob("EarFollowSpeed", "귀가 따라가는 속도", 1f, 40f, ""),
        };

        /// <summary>미리보기 카메라 오브젝트 이름 — 이름으로 찾으므로 창을 닫았다 열어도 이어진다.</summary>
        private const string FACE_CAM = "CtcFaceCam";

        private Vector2 m_Scroll;
        private bool m_Dirty;          // CSV와 달라진 상태인가
        private float m_FaceDistance = 0.55f;
        private float m_FaceSize = 0.38f;

        [MenuItem("Tools/CTC/Dev/눈·귀 수치 조절기")]
        public static void Open()
        {
            CharmTunerWindow win = GetWindow<CharmTunerWindow>();
            win.titleContent = new GUIContent("눈·귀 조절기");
            win.minSize = new Vector2(470f, 640f);
            win.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        /// <summary>
        /// 플레이 중에는 계속 다시 그린다 — 한계 읽음(<see cref="DrawEyeLimitReadout"/>)이
        /// 슬라이더를 놓은 뒤에도 살아 있는 값을 보여야 한다. 창이 안 그려지면 죽은 숫자가 남는다.
        /// </summary>
        private void Update()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        /// <summary>플레이가 끝나면 덮어쓴 값은 사라진다 — 그 사실을 표시로 남긴다.</summary>
        private void OnPlayModeChanged(PlayModeStateChange _change)
        {
            if (_change == PlayModeStateChange.EnteredEditMode) m_Dirty = false;
            Repaint();
        }

        private void OnGUI()
        {
            // 이름이 길다(「안쪽 — 정면을 지나치는 각」) — 기본 폭이면 잘려서 무슨 값인지 안 보인다.
            EditorGUIUtility.labelWidth = 210f;

            bool playing = EditorApplication.isPlaying;
            BalanceInfo balance = playing ? LiveBalance() : null;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!playing)
                {
                    EditorGUILayout.HelpBox(
                        "플레이 중이 아니다 — 끌어도 반영할 몸이 없다.\n" +
                        "▶ 를 눌러 판을 세운 뒤 다시 오면 슬라이더가 살아난다.",
                        MessageType.Warning);
                }
                else if (balance == null)
                {
                    EditorGUILayout.HelpBox(
                        "InfoManager가 아직 안 섰다 — 판이 시작되면 살아난다.", MessageType.Warning);
                }
                else
                {
                    int eyes = LiveEyes().Length;
                    int ears = LiveEars().Length;
                    EditorGUILayout.LabelField($"씬의 몸: 눈 {eyes} · 귀 {ears}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        m_Dirty ? "CSV와 다름 — 저장 안 하면 플레이가 끝날 때 되돌아간다" : "CSV 값 그대로",
                        EditorStyles.miniLabel);
                }
            }

            using (new EditorGUI.DisabledScope(!playing))
            {
                DrawFaceCamRow();
            }

            using (new EditorGUI.DisabledScope(!playing || balance == null))
            {
                m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

                EditorGUILayout.LabelField("눈", EditorStyles.boldLabel);
                foreach (Knob knob in EYE_KNOBS) DrawKnob(balance, knob);

                if (playing && balance != null) DrawEyeLimitReadout();

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("귀", EditorStyles.boldLabel);
                foreach (Knob knob in EAR_KNOBS) DrawKnob(balance, knob);

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!m_Dirty))
                {
                    if (GUILayout.Button("CSV에 저장", GUILayout.Height(26f))) SaveToCsv();
                    if (GUILayout.Button("되돌리기(CSV 값으로)", GUILayout.Height(26f))) RevertFromCsv();
                }
            }
        }

        private void DrawKnob(BalanceInfo _balance, Knob _knob)
        {
            FieldInfo field = FieldOf(_knob.Field);
            if (field == null)
            {
                EditorGUILayout.LabelField(_knob.Label, "BalanceInfo에 없는 컬럼: " + _knob.Field);
                return;
            }

            float current = _balance != null ? Convert.ToSingle(field.GetValue(_balance)) : 0f;

            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.Slider(
                new GUIContent(_knob.Label, _knob.Hint), current, _knob.Min, _knob.Max);
            if (EditorGUI.EndChangeCheck() && _balance != null && !Mathf.Approximately(next, current))
            {
                field.SetValue(_balance, next);
                m_Dirty = true;
                PushToLiveBodies();
            }
        }

        /// <summary>안쪽 한계는 **쉬는 각도 + 지나치는 각**이라 종마다 다르다 — 결과를 그대로 보여 준다.</summary>
        private static void DrawEyeLimitReadout()
        {
            EyeLook[] eyes = LiveEyes();
            if (eyes.Length == 0) return;

            var seen = new HashSet<string>();
            foreach (EyeLook eye in eyes)
            {
                if (eye == null) continue;
                Vector3 limits = eye.DevLimitsDeg();
                string line = $"{eye.name}: 쉬는 각도 {limits.z:F1}° · 바깥 {limits.x:F1}° · 안쪽 {limits.y:F1}°";
                if (!seen.Add(line)) continue;
                EditorGUILayout.LabelField("   " + line, EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// **얼굴을 보면서 만지는 줄**(2026-09-10). 3인칭이라 제 캐릭터는 뒤통수만 보인다 —
        /// 이 창의 슬라이더는 눈을 보면서 끌어야 뜻이 있는데 게임 화면으로는 눈이 안 보인다.
        ///
        /// 그래서 그 몸의 머리 본에 카메라를 하나 붙이고 <c>depth</c>를 게임 카메라보다 높게 준다 —
        /// 게임 뷰 **위에 겹쳐** 그려지므로 플레이를 멈추지 않고 얼굴을 본다. 머리에 붙였으니
        /// 걸어도 고개를 돌려도 따라간다.
        /// </summary>
        private void DrawFaceCamRow()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("얼굴 보기 (게임 화면 위에 겹쳐 그린다)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("쥐 얼굴")) AttachFaceCam(RoleId.Mouse);
                    if (GUILayout.Button("고양이 얼굴")) AttachFaceCam(RoleId.Cat);
                    if (GUILayout.Button("내 몸")) AttachFaceCam(RoleId.None);
                    using (new EditorGUI.DisabledScope(FindFaceCam() == null))
                    {
                        if (GUILayout.Button("끄기")) RemoveFaceCam();
                    }
                }

                EditorGUI.BeginChangeCheck();
                m_FaceDistance = EditorGUILayout.Slider("거리(m)", m_FaceDistance, 0.15f, 3f);
                m_FaceSize = EditorGUILayout.Slider("창 크기(화면 비율)", m_FaceSize, 0.15f, 1f);
                if (EditorGUI.EndChangeCheck()) MoveFaceCam();
            }
        }

        // ── 창을 안 열고도 쓰는 손잡이 ──────────────────────────────────────
        // 창 버튼은 사람만 누를 수 있다. 같은 일을 메뉴로도 열어 두면 자동화(MCP)가 부를 수 있고,
        // 무엇보다 **CSV를 에디터 밖에서 고치고 플레이 중에 바로 보는** 길이 생긴다.

        [MenuItem("Tools/CTC/Dev/얼굴 보기 — 쥐")]
        public static void FaceCamMouse()
        {
            AttachFaceCamStatic(RoleId.Mouse, 0.55f);
        }

        [MenuItem("Tools/CTC/Dev/얼굴 보기 — 고양이")]
        public static void FaceCamCat()
        {
            AttachFaceCamStatic(RoleId.Cat, 0.55f);
        }

        [MenuItem("Tools/CTC/Dev/얼굴 보기 — 끄기")]
        public static void FaceCamOff()
        {
            RemoveFaceCam();
        }

        /// <summary>
        /// **CSV를 다시 읽어 씬의 모든 몸에 지금 먹인다.** 슬라이더가 미는 것과 **같은 경로**다 —
        /// 값을 파일로 고치고 플레이를 멈추지 않은 채 결과를 보고 싶을 때 쓴다.
        /// </summary>
        [MenuItem("Tools/CTC/Dev/눈·귀 수치 지금 다시 읽기 (CSV → 씬)")]
        public static void ReloadTuningNow()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CharmTuner] 플레이 중이 아니다 — 먹일 몸이 없다");
                return;
            }
            InfoManager.Reload();
            PushToLiveBodies();
            Debug.Log("[CharmTuner] CSV를 다시 읽어 씬의 몸에 먹였다");
        }

        private static GameObject FindFaceCam()
        {
            return GameObject.Find(FACE_CAM);
        }

        private static void RemoveFaceCam()
        {
            GameObject cam = FindFaceCam();
            if (cam != null) UnityEngine.Object.DestroyImmediate(cam);
        }

        /// <summary>그 역할의 몸을 하나 골라 얼굴 앞에 카메라를 붙인다. <c>None</c>이면 내 몸.</summary>
        private void AttachFaceCam(RoleId _role)
        {
            AttachFaceCamStatic(_role, m_FaceDistance, m_FaceSize);
        }

        private static void AttachFaceCamStatic(RoleId _role, float _distance, float _size = 0.38f)
        {
            EyeLook target = PickBody(_role);
            if (target == null)
            {
                Debug.LogWarning($"[CharmTuner] 씬에 {_role} 몸이 없다 — 판을 세우고 다시 눌러라");
                return;
            }

            RemoveFaceCam();

            var go = new GameObject(FACE_CAM);
            Camera cam = go.AddComponent<Camera>();
            cam.depth = 100f;                       // 게임 카메라 위에 겹친다
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 40f;

            // **얼굴에 등을 하나 달아 준다**(2026-09-11). 이 게임은 어두운 창고가 무대라
            // 그냥 카메라만 놓으면 눈동자가 실루엣에 묻혀 안 보인다 — 수치를 눈으로 정하려면
            // 얼굴이 밝아야 한다. 카메라 자식이라 게임 화면의 조명 연출은 안 건드린다.
            var lampGo = new GameObject("Lamp");
            lampGo.transform.SetParent(go.transform, false);
            Light lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 4f;
            lamp.intensity = 6f;
            lamp.color = new Color(1f, 0.97f, 0.92f);

            PlaceFaceCam(go.transform, target, _distance, _size);
        }

        private void MoveFaceCam()
        {
            GameObject cam = FindFaceCam();
            if (cam == null) return;
            EyeLook owner = cam.GetComponentInParent<EyeLook>();
            if (owner == null) return;
            PlaceFaceCam(cam.transform, owner, m_FaceDistance, m_FaceSize);
        }

        /// <summary>
        /// 두 흰자의 가운데를 그 몸 정면에서 바라본다 — 좌·우 눈이 같은 배율로 들어온다.
        ///
        /// **머리 본에 매단다**(2026-09-11). 몸에 매달면 애니메이터가 고개를 굴릴 때마다
        /// 얼굴이 화면 밖으로 흘러 나갔다 — 걷기만 해도 눈이 사라져 수치를 못 만졌다.
        /// 머리에 매달면 고개가 어떻게 돌아도 눈이 화면 한가운데에 남는다.
        /// </summary>
        private static void PlaceFaceCam(Transform _cam, EyeLook _target, float _distance, float _size)
        {
            Vector3 focus = (_target.DevEyeWorldRight() + _target.DevEyeWorldLeft()) * 0.5f;
            Transform body = _target.transform;
            Vector3 pos = focus + body.forward * _distance;

            Transform anchor = _target.DevHead();
            if (anchor == null) anchor = body;
            _cam.SetParent(anchor, true);
            _cam.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, body.up));

            var cam = _cam.GetComponent<Camera>();
            if (cam != null)
            {
                float size = Mathf.Clamp(_size, 0.15f, 1f);
                cam.rect = new Rect(1f - size, 1f - size, size, size);   // 오른쪽 위 구석
            }
        }

        private static EyeLook PickBody(RoleId _role)
        {
            EyeLook[] all = LiveEyes();
            EyeLook fallback = null;
            foreach (EyeLook eye in all)
            {
                if (eye == null) continue;
                if (fallback == null) fallback = eye;

                if (_role == RoleId.None)
                {
                    if (eye.DevMine()) return eye;
                    continue;
                }

                var tag = eye.GetComponent<CTC.Runtime.Combat.RoleTag>();
                if (tag != null && tag.Role == _role) return eye;
            }
            return _role == RoleId.None ? fallback : null;
        }

        private static FieldInfo FieldOf(string _name)
        {
            return typeof(BalanceInfo).GetField(_name, BindingFlags.Public | BindingFlags.Instance);
        }

        private static BalanceInfo LiveBalance()
        {
            InfoManager manager = InfoManager.Instance;
            return manager != null ? manager.Balance : null;
        }

        private static EyeLook[] LiveEyes()
        {
            return UnityEngine.Object.FindObjectsByType<EyeLook>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static EarPerk[] LiveEars()
        {
            return UnityEngine.Object.FindObjectsByType<EarPerk>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        /// <summary>씬에 서 있는 **모든 몸**에 지금 값을 먹인다 — 내 것·남의 것·봇을 안 가린다.</summary>
        private static void PushToLiveBodies()
        {
            foreach (EyeLook eye in LiveEyes())
            {
                if (eye != null) eye.DevReloadTuning();
            }
            foreach (EarPerk ear in LiveEars())
            {
                if (ear != null) ear.DevReloadTuning();
            }
        }

        /// <summary>지금 만진 값을 CSV의 해당 칸에 쓰고 생성기를 돌린다 — 여기가 원본을 바꾸는 유일한 곳이다.</summary>
        private void SaveToCsv()
        {
            BalanceInfo balance = LiveBalance();
            if (balance == null) return;

            string full = Path.GetFullPath(CSV_PATH);
            if (!File.Exists(full))
            {
                Debug.LogError("[CharmTuner] CSV가 없다: " + CSV_PATH);
                return;
            }

            string[] lines = File.ReadAllLines(full);
            if (lines.Length < 2)
            {
                Debug.LogError("[CharmTuner] CSV가 머리+한 줄이 아니다: " + CSV_PATH);
                return;
            }

            // 머리는 "필드명.타입" — 점 앞이 BalanceInfo의 필드 이름이다.
            string[] head = lines[0].TrimStart('﻿').Split(',');
            string[] row = lines[1].Split(',');
            if (head.Length != row.Length)
            {
                Debug.LogError($"[CharmTuner] CSV 칸 수가 안 맞는다: 머리 {head.Length} vs 값 {row.Length}");
                return;
            }

            int written = 0;
            foreach (Knob knob in AllKnobs())
            {
                int col = ColumnOf(head, knob.Field);
                FieldInfo field = FieldOf(knob.Field);
                if (col < 0 || field == null)
                {
                    Debug.LogWarning("[CharmTuner] CSV에 없는 칸 — 건너뛴다: " + knob.Field);
                    continue;
                }
                row[col] = Convert.ToSingle(field.GetValue(balance))
                    .ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                written++;
            }

            lines[1] = string.Join(",", row);
            File.WriteAllLines(full, lines);
            AssetDatabase.ImportAsset(CSV_PATH);

            if (!EditorApplication.ExecuteMenuItem(CONVERT_MENU))
            {
                Debug.LogWarning("[CharmTuner] 생성기를 못 돌렸다 — 손으로 " + CONVERT_MENU + " 를 실행해라");
            }

            m_Dirty = false;
            Debug.Log($"[CharmTuner] CSV에 {written}칸 저장하고 생성기를 돌렸다: {CSV_PATH}");
        }

        /// <summary>
        /// CSV를 다시 읽어 덮어쓴 값을 버린다. <see cref="InfoManager.Reload"/>가 싱글턴을 비우므로
        /// 다음 접근에서 표 전체가 새로 로드된다 — 그 뒤에 몸들에게 다시 먹여야 화면이 따라온다.
        /// </summary>
        private void RevertFromCsv()
        {
            InfoManager.Reload();
            PushToLiveBodies();
            m_Dirty = false;
            Repaint();
        }

        private static int ColumnOf(string[] _head, string _field)
        {
            for (int i = 0; i < _head.Length; i++)
            {
                string cell = _head[i].Trim();
                int dot = cell.IndexOf('.');
                if (dot > 0) cell = cell.Substring(0, dot);
                if (cell == _field) return i;
            }
            return -1;
        }

        private static IEnumerable<Knob> AllKnobs()
        {
            foreach (Knob knob in EYE_KNOBS) yield return knob;
            foreach (Knob knob in EAR_KNOBS) yield return knob;
        }
    }
}
