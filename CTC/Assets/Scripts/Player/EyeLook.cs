using CTC.Runtime.Info;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 눈동자가 보는 곳을 따라간다(R.E.P.O. 참고) — 몸이 안 움직여도 캐릭터가 살아 보이게.
    ///
    /// **왜 얹는가**: 팩 모델(Quirky Pets)의 눈은 본도 아니고 텍스처도 아니다.
    /// 8×8 팔레트를 UV로 찍은 **스킨드 메시의 한 덩어리**라 돌릴 트랜스폼이 없다(2026-09-08 실측).
    /// 그래서 흰자 구·눈동자 구·눈꺼풀 구를 **머리 본 아래에 새로 얹고** 로컬로만 굴린다.
    /// 셋 다 모델과 **같은 머티리얼**을 쓰고 팔레트 텍셀만 골라 찍으므로 새 머티리얼·셰이더가 없다
    /// (색이 M_Cat_URP·M_Mouse_URP의 틴트를 그대로 따라온다).
    ///
    /// **네트워크 0**: 전부 각 클라가 자기 화면에서 계산한다. 내 몸은 내 카메라가 보는 곳을,
    /// 남의 몸은 그 몸의 정면을 본다. 보내는 것도 받는 것도 없다.
    ///
    /// 수치(최대 각도·따라가는 속도·깜빡임)는 BalanceInfo.csv가 원본.
    /// 눈의 자리·크기는 모델 지오메트리라 CSV가 아니라 프리팹에 굽는다(PlayerPrefabFactory).
    /// </summary>
    public class EyeLook : MonoBehaviour
    {
        // 팔레트 텍셀 중심(8×8, 2×2 블록) — 두 종이 같은 자리를 쓴다.
        private static readonly Vector2 UV_WHITE = new Vector2(0.5625f, 0.375f);
        private static readonly Vector2 UV_DARK = new Vector2(0.8125f, 0.375f);
        private static readonly Vector2 UV_SKIN = new Vector2(0.3125f, 0.75f);

        private const float BLINK_SHRINK = 0.95f;    // 감았을 때 흰자가 줄어드는 비율
        private const float LID_FLATTEN = 0.5f;      // 눈꺼풀은 눈 축으로 납작하게 — 구면 혹처럼 튀어나오면 안 된다

        [SerializeField] private string m_HeadBoneName = "head";

        [Tooltip("오른눈 중심 — 머리 본 로컬. 왼눈은 x를 뒤집는다.")]
        [SerializeField] private Vector3 m_EyeCenterLocal = new Vector3(0.00132f, 0.00100f, 0.00034f);

        [Tooltip("오른눈이 쉴 때 보는 방향 — 머리 본 로컬.")]
        [SerializeField] private Vector3 m_EyeAxisLocal = new Vector3(0.892f, 0.396f, 0.216f);

        [Tooltip("얹는 흰자 반지름(기준) — 팩 눈동자를 덮되 흰 테두리 안에 든다. CSV 배율이 곱해진다.")]
        [SerializeField] private float m_ScleraRadiusLocal = 0.00055f;

        [Tooltip("눈꺼풀 반지름 — 감았을 때 흰 테두리까지 덮는다.")]
        [SerializeField] private float m_LidRadiusLocal = 0.00090f;

        private static Mesh s_ScleraMesh;
        private static Mesh s_PupilMesh;
        private static Mesh s_LidMesh;

        private Transform m_Head;
        private NetworkObject m_NetObject;
        private Camera m_Camera;

        private Transform m_EyeR, m_EyeL, m_PupilR, m_PupilL, m_LidR, m_LidL;
        private Renderer m_LidRendererR, m_LidRendererL;
        private Renderer m_EyeRendererR, m_EyeRendererL, m_PupilRendererR, m_PupilRendererL;

        private Vector3 m_RestR, m_RestL;     // 머리 본 로컬
        private Vector3 m_GazeR, m_GazeL;

        private float m_ScleraRadius;         // 기준 반경 × CSV 배율
        private float m_LidRadius;
        private float m_PupilRatio;           // 흰자 반지름 대비 눈동자 구 반지름
        private float m_PupilDistance;        // 흰자 중심에서 눈동자 중심까지(흰자 단위)
        private float m_Flatten;              // 눈 축 방향 눌림(1이면 완전한 구)
        private float m_Inset;                // 눈 축 안쪽으로 미는 양(흰자 반지름 단위)
        private Quaternion m_EyeRotR, m_EyeRotL;      // 로컬 +z = 눈 축
        private Quaternion m_EyeRotInvR, m_EyeRotInvL;
        private float m_MaxAngleDeg;
        private float m_FollowSpeed;
        private float m_BlinkMin;
        private float m_BlinkMax;
        private float m_BlinkSeconds;
        private float m_BlinkTimer;           // 다음 깜빡임까지 남은 시간
        private float m_BlinkElapsed = -1f;   // 음수면 안 깜빡이는 중
        private bool m_LidVisible;
        private bool m_Ready;

        private void Awake()
        {
            m_NetObject = GetComponent<NetworkObject>();

            BalanceInfo balance = InfoManager.Instance.Balance;
            m_MaxAngleDeg = balance.EyeMaxAngleDeg;
            m_FollowSpeed = balance.EyeFollowSpeed;
            m_BlinkMin = balance.EyeBlinkMinSeconds;
            m_BlinkMax = balance.EyeBlinkMaxSeconds;
            m_BlinkSeconds = balance.EyeBlinkSeconds;

            // 크기는 **인상을 바꾸는 손잡이**다(2026-09-08). 팩 캐릭터의 매력이 큰 검은 눈이라
            // 눈동자를 너무 작게 잡으면 얼굴이 달라진다 — 플레이 거리에서 보고 CSV로 맞춘다.
            m_ScleraRadius = m_ScleraRadiusLocal * Mathf.Max(0.05f, balance.EyeScleraScale);
            m_LidRadius = Mathf.Max(m_LidRadiusLocal, m_ScleraRadius * 1.2f);
            m_PupilRatio = Mathf.Clamp(balance.EyePupilRatio, 0.05f, 0.95f);
            m_PupilDistance = Mathf.Clamp01(balance.EyePupilOffsetRatio);
            // 팩 눈은 **납작한 원판**이라 완전한 구를 얹으면 얼굴 실루엣 밖으로 공처럼 솟는다
            // (2026-09-08 사용자 피드백). 옆에서 봐야 보이는 종류의 문제다.
            // 눈 축으로만 눌러 돔으로 만들면 정면 실루엣(=덮어야 하는 크기)은 그대로라
            // 팩 눈동자가 밑에서 비치지 않으면서 옆에서도 안 솟는다.
            m_Flatten = Mathf.Clamp(balance.EyeFlatten, 0.1f, 1f);
            m_Inset = Mathf.Clamp(balance.EyeInsetRatio, 0f, 0.6f);
            m_BlinkTimer = EyeLookLogic.NextBlinkDelay(m_BlinkMin, m_BlinkMax, Random.value);

            Build();
        }

        private void Build()
        {
            m_Head = FindDeep(transform, m_HeadBoneName);
            if (m_Head == null)
            {
                Debug.LogWarning("[EyeLook] 머리 본을 못 찾았다 — 눈을 얹지 않는다: " + m_HeadBoneName);
                return;
            }

            SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null || skin.sharedMaterial == null)
            {
                Debug.LogWarning("[EyeLook] 모델 머티리얼을 못 찾았다 — 눈을 얹지 않는다");
                return;
            }

            Material mat = skin.sharedMaterial;
            GameObject sample = skin.gameObject;

            EnsureMeshes();

            m_RestR = m_EyeAxisLocal.normalized;
            m_RestL = EyeLookLogic.Mirror(m_RestR);
            m_GazeR = m_RestR;
            m_GazeL = m_RestL;

            m_EyeRotR = Quaternion.LookRotation(m_RestR);
            m_EyeRotL = Quaternion.LookRotation(m_RestL);
            m_EyeRotInvR = Quaternion.Inverse(m_EyeRotR);
            m_EyeRotInvL = Quaternion.Inverse(m_EyeRotL);

            // 눈 축 안쪽으로 밀어 더 파묻는다 — 덮는 넓이(가로)는 그대로다.
            Vector3 centerR = m_EyeCenterLocal - m_RestR * (m_ScleraRadius * m_Inset);
            Vector3 centerL = EyeLookLogic.Mirror(centerR);

            m_EyeR = MakePart("EyeR", s_ScleraMesh, mat, sample, centerR, m_ScleraRadius, out m_EyeRendererR);
            m_EyeL = MakePart("EyeL", s_ScleraMesh, mat, sample, centerL, m_ScleraRadius, out m_EyeRendererL);
            m_EyeR.localRotation = m_EyeRotR;
            m_EyeL.localRotation = m_EyeRotL;

            m_PupilR = MakeChild("PupilR", s_PupilMesh, mat, sample, m_EyeR, out m_PupilRendererR);
            m_PupilL = MakeChild("PupilL", s_PupilMesh, mat, sample, m_EyeL, out m_PupilRendererL);

            m_LidR = MakePart("LidR", s_LidMesh, mat, sample, centerR, m_LidRadius, out m_LidRendererR);
            m_LidL = MakePart("LidL", s_LidMesh, mat, sample, centerL, m_LidRadius, out m_LidRendererL);
            // 눈 축을 로컬 +z로 두면 z만 눌러 렌즈 모양이 된다 — 감았을 때 눈두덩처럼 보인다.
            m_LidR.localRotation = Quaternion.LookRotation(m_RestR);
            m_LidL.localRotation = Quaternion.LookRotation(m_RestL);
            m_LidRendererR.enabled = false;
            m_LidRendererL.enabled = false;

            Apply();
            m_Ready = true;
        }

        private Transform MakePart(string _name, Mesh _mesh, Material _mat, GameObject _sample,
            Vector3 _localPos, float _radius, out Renderer _renderer)
        {
            var go = new GameObject(_name);
            go.layer = _sample.layer;
            go.tag = _sample.tag;
            go.transform.SetParent(m_Head, false);
            go.transform.localPosition = _localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * _radius;

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer = mr;
            return go.transform;
        }

        private Transform MakeChild(string _name, Mesh _mesh, Material _mat, GameObject _sample,
            Transform _parent, out Renderer _renderer)
        {
            var go = new GameObject(_name);
            go.layer = _sample.layer;
            go.tag = _sample.tag;
            go.transform.SetParent(_parent, false);
            go.transform.localScale = Vector3.one * m_PupilRatio;

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer = mr;
            return go.transform;
        }

        /// <summary>플레이 종료 시 정적 메시가 파괴되므로 매번 널을 본다(규칙 9).</summary>
        private static void EnsureMeshes()
        {
            if (s_ScleraMesh == null) s_ScleraMesh = BuildSphere("EyeSclera", UV_WHITE);
            if (s_PupilMesh == null) s_PupilMesh = BuildSphere("EyePupil", UV_DARK);
            if (s_LidMesh == null) s_LidMesh = BuildSphere("EyeLid", UV_SKIN);
        }

        /// <summary>반지름 1 구. UV는 팔레트 텍셀 하나로 고정한다 — 색을 그 텍셀이 정한다.</summary>
        private static Mesh BuildSphere(string _name, Vector2 _uv)
        {
            const int STACKS = 10;
            const int SLICES = 14;

            int vertCount = (STACKS + 1) * (SLICES + 1);
            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            for (int y = 0; y <= STACKS; y++)
            {
                float v = (float)y / STACKS;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= SLICES; x++)
                {
                    float u = (float)x / SLICES;
                    float theta = u * Mathf.PI * 2f;
                    var p = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    int i = y * (SLICES + 1) + x;
                    verts[i] = p;
                    norms[i] = p;
                    uvs[i] = _uv;
                }
            }

            var tris = new int[STACKS * SLICES * 6];
            int t = 0;
            for (int y = 0; y < STACKS; y++)
            {
                for (int x = 0; x < SLICES; x++)
                {
                    int a = y * (SLICES + 1) + x;
                    int b = a + SLICES + 1;
                    // 감는 방향을 뒤집으면 구가 안팎이 뒤집혀 **에러 없이** 사라진다
                    // (2026-09-08 실측: 흰자는 머리에 가려 안 보이고 눈동자만 새까맣게 나왔다).
                    tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                    tris[t++] = a + 1; tris[t++] = b + 1; tris[t++] = b;
                }
            }

            var mesh = new Mesh { name = _name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 애니메이터가 머리 본을 굴린 뒤에 얹어야 한 프레임 늦지 않는다.
        private void LateUpdate()
        {
            if (!m_Ready) return;

            float dt = Time.deltaTime;
            Vector3 aimLocal = m_Head.InverseTransformDirection(AimDirection());

            m_GazeR = EyeLookLogic.Follow(
                m_GazeR, EyeLookLogic.ClampGaze(m_RestR, aimLocal, m_MaxAngleDeg), m_FollowSpeed, dt);
            m_GazeL = EyeLookLogic.Follow(
                m_GazeL, EyeLookLogic.ClampGaze(m_RestL, aimLocal, m_MaxAngleDeg), m_FollowSpeed, dt);

            StepBlink(dt);
            Apply();
        }

        private void StepBlink(float _dt)
        {
            if (m_BlinkSeconds <= 0f) return;

            if (m_BlinkElapsed >= 0f)
            {
                m_BlinkElapsed += _dt;
                if (m_BlinkElapsed >= m_BlinkSeconds)
                {
                    m_BlinkElapsed = -1f;
                    m_BlinkTimer = EyeLookLogic.NextBlinkDelay(m_BlinkMin, m_BlinkMax, Random.value);
                }
                return;
            }

            m_BlinkTimer -= _dt;
            if (m_BlinkTimer <= 0f) m_BlinkElapsed = 0f;
        }

        private void Apply()
        {
            float cover = m_BlinkElapsed >= 0f
                ? EyeLookLogic.BlinkCover(m_BlinkElapsed, m_BlinkSeconds)
                : 0f;

            // 가로(x·y)는 그대로 두고 눈 축(z)만 눌러 돔으로 만든다 — 부모의 비균등 스케일이
            // 눈동자까지 같이 눌러 주므로 눈동자도 표면을 따라 붙는다.
            float eye = m_ScleraRadius * (1f - BLINK_SHRINK * cover);
            var eyeScale = new Vector3(eye, eye, eye * m_Flatten);
            m_EyeR.localScale = eyeScale;
            m_EyeL.localScale = eyeScale;

            // 시선은 머리 로컬이고 흰자는 눈 축으로 돌려 놨다 — 눈 로컬로 옮겨야 자리가 맞는다.
            m_PupilR.localPosition = (m_EyeRotInvR * m_GazeR) * m_PupilDistance;
            m_PupilL.localPosition = (m_EyeRotInvL * m_GazeL) * m_PupilDistance;

            // 눈꺼풀은 흰자보다 덜 눌려야 감았을 때 흰자가 안 비친다.
            float lid = m_LidRadius * cover;
            var lidScale = new Vector3(lid, lid, lid * Mathf.Max(LID_FLATTEN, m_Flatten));
            m_LidR.localScale = lidScale;
            m_LidL.localScale = lidScale;

            bool visible = cover > 0.01f;
            if (visible != m_LidVisible)
            {
                m_LidVisible = visible;
                m_LidRendererR.enabled = visible;
                m_LidRendererL.enabled = visible;
            }
        }

        /// <summary>내 몸이면 내가 보는 곳, 남의 몸이면 그 몸의 정면 — 어느 쪽도 통신하지 않는다.</summary>
        private Vector3 AimDirection()
        {
            if (m_NetObject != null &&
                LocalControlLogic.Mine(m_NetObject.IsOwner, m_NetObject.IsPlayerObject))
            {
                if (m_Camera == null) m_Camera = Camera.main;
                if (m_Camera != null) return m_Camera.transform.forward;
            }
            return transform.forward;
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

        /// <summary>QA 전용 — 오른눈 눈동자의 월드 위치(실제로 움직이는지 재는 유일한 근거).</summary>
        public Vector3 DevPupilWorldRight()
        {
            return m_PupilR != null ? m_PupilR.position : Vector3.zero;
        }

        /// <summary>
        /// QA 전용 — 얹은 눈을 껐다 켠다. **"전/후"를 같은 거리·같은 각도로 나란히 찍는 유일한 방법**이다
        /// (컴포넌트만 끄면 이미 만들어 둔 구가 그대로 남는다).
        /// </summary>
        public void DevSetVisible(bool _on)
        {
            if (m_EyeRendererR != null) m_EyeRendererR.enabled = _on;
            if (m_EyeRendererL != null) m_EyeRendererL.enabled = _on;
            if (m_PupilRendererR != null) m_PupilRendererR.enabled = _on;
            if (m_PupilRendererL != null) m_PupilRendererL.enabled = _on;
            if (!_on)
            {
                if (m_LidRendererR != null) m_LidRendererR.enabled = false;
                if (m_LidRendererL != null) m_LidRendererL.enabled = false;
                m_LidVisible = false;
            }
            enabled = _on;   // 꺼진 동안엔 눈꺼풀이 다시 켜지지 않게 LateUpdate도 멈춘다
        }

        /// <summary>QA 전용 — 지금 깜빡이게 한다(랜덤 주기를 기다리지 않고 그 프레임을 잡는다).</summary>
        public void DevBlinkNow()
        {
            m_BlinkElapsed = m_BlinkSeconds * 0.5f;   // 가장 많이 감은 순간
        }

        /// <summary>QA 전용 — 조준을 어디서 가져왔나("cam" | "body"). 배선 누락은 이 한 글자로 갈린다.</summary>
        public string DevAimSource()
        {
            bool mine = m_NetObject != null &&
                        LocalControlLogic.Mine(m_NetObject.IsOwner, m_NetObject.IsPlayerObject);
            if (!mine) return "body(notMine)";
            return m_Camera != null ? "cam" : "body(noCamera)";
        }

        /// <summary>QA 전용 — 오른눈 흰자의 월드 위치(고정 시점 근접 촬영의 초점).</summary>
        public Vector3 DevEyeWorldRight()
        {
            return m_EyeR != null ? m_EyeR.position : transform.position;
        }

        /// <summary>QA 전용 — 머리 본 로컬 시선(오른눈).</summary>
        public Vector3 DevGazeRight()
        {
            return m_GazeR;
        }
    }
}
