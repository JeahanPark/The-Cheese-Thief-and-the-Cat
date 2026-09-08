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

        private const float PUPIL_SCALE = 0.5f;      // 흰자 반지름 대비
        private const float PUPIL_DISTANCE = 0.75f;  // 흰자 중심에서 눈동자 중심까지
        private const float BLINK_SHRINK = 0.95f;    // 감았을 때 흰자가 줄어드는 비율
        private const float LID_FLATTEN = 0.5f;      // 눈꺼풀은 눈 축으로 납작하게 — 구면 혹처럼 튀어나오면 안 된다

        [SerializeField] private string m_HeadBoneName = "head";

        [Tooltip("오른눈 중심 — 머리 본 로컬. 왼눈은 x를 뒤집는다.")]
        [SerializeField] private Vector3 m_EyeCenterLocal = new Vector3(0.00132f, 0.00100f, 0.00034f);

        [Tooltip("오른눈이 쉴 때 보는 방향 — 머리 본 로컬.")]
        [SerializeField] private Vector3 m_EyeAxisLocal = new Vector3(0.892f, 0.396f, 0.216f);

        [Tooltip("얹는 흰자 반지름 — 팩 눈동자를 덮되 흰 테두리 안에 든다.")]
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

        private Vector3 m_RestR, m_RestL;     // 머리 본 로컬
        private Vector3 m_GazeR, m_GazeL;

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

            Vector3 centerR = m_EyeCenterLocal;
            Vector3 centerL = EyeLookLogic.Mirror(centerR);

            m_EyeR = MakePart("EyeR", s_ScleraMesh, mat, sample, centerR, m_ScleraRadiusLocal, out _);
            m_EyeL = MakePart("EyeL", s_ScleraMesh, mat, sample, centerL, m_ScleraRadiusLocal, out _);

            m_PupilR = MakeChild("PupilR", s_PupilMesh, mat, sample, m_EyeR);
            m_PupilL = MakeChild("PupilL", s_PupilMesh, mat, sample, m_EyeL);

            m_LidR = MakePart("LidR", s_LidMesh, mat, sample, centerR, m_LidRadiusLocal, out m_LidRendererR);
            m_LidL = MakePart("LidL", s_LidMesh, mat, sample, centerL, m_LidRadiusLocal, out m_LidRendererL);
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

        private Transform MakeChild(string _name, Mesh _mesh, Material _mat, GameObject _sample, Transform _parent)
        {
            var go = new GameObject(_name);
            go.layer = _sample.layer;
            go.tag = _sample.tag;
            go.transform.SetParent(_parent, false);
            go.transform.localScale = Vector3.one * PUPIL_SCALE;

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

            float eye = m_ScleraRadiusLocal * (1f - BLINK_SHRINK * cover);
            m_EyeR.localScale = new Vector3(eye, eye, eye);
            m_EyeL.localScale = new Vector3(eye, eye, eye);

            m_PupilR.localPosition = m_GazeR * PUPIL_DISTANCE;
            m_PupilL.localPosition = m_GazeL * PUPIL_DISTANCE;

            float lid = m_LidRadiusLocal * cover;
            var lidScale = new Vector3(lid, lid, lid * LID_FLATTEN);
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
