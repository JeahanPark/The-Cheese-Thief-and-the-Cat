using System.Threading;
using CTC.Runtime.Info;
using Cysharp.Threading.Tasks;
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
    /// **조준 방향은 복제된다**(2026-09-10). 예전엔 통신 0이었는데 — 남의 몸은 그 몸의 정면을
    /// 보게 해 놨더니 눈이 쉬는 방향도 결국 머리 정면이라 <c>ClampGaze(정면, 정면) = 정면</c>,
    /// **남의 눈동자가 늘 가운데 붙어 아무것도 안 했다.** 3인칭이라 내 눈은 내가 거의 못 보므로
    /// 이 연출의 값어치는 **남이 볼 때** 나온다. 그래서 <see cref="GazeAim"/>이 조준 방향을
    /// 몸 로컬 각도 **2바이트**로 접어 초당 12회 상한·변화 시에만 복제한다.
    /// 이 클래스는 그 값을 **읽기만** 한다 — 여전히 NetworkBehaviour가 아니고, 상태를 안 가진다.
    ///
    /// 눈꺼풀(깜빡임)은 그대로 통신 0이다 — 각자 제 시계로 랜덤하게 깜빡인다.
    /// **쏠 때 질끈 감는 것도 여기서 돈다**(2026-09-12, <see cref="BlinkForShot"/>) —
    /// 발사 RPC가 이미 전원에게 가므로 이것도 통신이 0이다.
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

        [Tooltip("오른눈이 쉴 때 보는 방향 — 머리 본 로컬. 왼눈은 x를 뒤집는다.")]
        [SerializeField] private Vector3 m_EyeAxisLocal = new Vector3(0.892f, 0.396f, 0.216f);

        [Tooltip("머리 본 로컬 **정면** — 여기를 조준하면 두 눈동자가 흰자 한가운데 앉는다. " +
                 "리그가 종마다 달라 굽는다(고양이 +z · 쥐 +y). 0이면 두 눈의 대칭축으로 물러선다.")]
        [SerializeField] private Vector3 m_HeadForwardLocal = new Vector3(0f, 0f, 1f);

        [Tooltip("얹는 흰자 반지름(기준) — 팩 눈동자를 덮되 흰 테두리 안에 든다. CSV 배율이 곱해진다.")]
        [SerializeField] private float m_ScleraRadiusLocal = 0.00055f;

        [Tooltip("눈꺼풀 반지름 — 감았을 때 흰 테두리까지 덮는다.")]
        [SerializeField] private float m_LidRadiusLocal = 0.00090f;

        private static Mesh s_ScleraMesh;
        private static Mesh s_PupilMesh;
        private static Mesh s_LidMesh;

        private Transform m_Head;
        private NetworkObject m_NetObject;
        private GazeAim m_Aim;

        private Transform m_EyeR, m_EyeL, m_PupilR, m_PupilL, m_LidR, m_LidL;
        private Renderer m_LidRendererR, m_LidRendererL;
        private Renderer m_EyeRendererR, m_EyeRendererL, m_PupilRendererR, m_PupilRendererL;

        private Vector3 m_RestR, m_RestL;     // 머리 본 로컬
        private Vector3 m_GazePivot;          // 두 눈이 함께 도는 기준축(머리 본 로컬)
        private Vector3 m_GazeR, m_GazeL;

        private float m_ScleraRadius;         // 기준 반경 × CSV 배율
        private float m_LidRadius;
        private float m_PupilRatio;           // 흰자 반지름 대비 눈동자 구 반지름
        private float m_PupilDistance;        // 흰자 중심에서 눈동자 중심까지(흰자 단위)
        private float m_Flatten;              // 눈 축 방향 눌림(1이면 완전한 구)
        private float m_Inset;                // 눈 축 안쪽으로 미는 양(흰자 반지름 단위)
        private float m_PastCenterDeg;        // 안쪽 눈이 정면을 지나칠 수 있는 각(CSV)
        private Quaternion m_EyeRotR, m_EyeRotL;      // 로컬 +z = 눈 축
        private Quaternion m_EyeRotInvR, m_EyeRotInvL;
        private float m_MaxAngleDeg;      // 바깥·위아래 한계(쉬는 방향 기준)
        private float m_InwardMaxDeg;     // 안쪽 한계 — 쉬는 각도 + CSV의 «정면을 지나치는 각»
        private float m_SwingMaxDeg;      // 공유 회전의 한계 = 둘 중 큰 쪽
        private float m_FollowSpeed;
        private float m_BlinkMin;
        private float m_BlinkMax;
        private float m_BlinkSeconds;
        private float m_BlinkTimer;           // 다음 깜빡임까지 남은 시간
        private float m_BlinkElapsed = -1f;   // 음수면 안 깜빡이는 중
        private bool m_LidVisible;
        private bool m_Ready;

        /// <summary>
        /// 쏠 때 감는 시간표의 위상(<see cref="ShotBlinkLogic"/>) — **발사 순간이 0**이라
        /// 지연 구간은 음수다. <c>+무한</c>이면 안 감는 중이다(<c>Cover</c>가 0을 준다).
        /// </summary>
        private float m_ShotPhase = float.PositiveInfinity;

        /// <summary>순수 C# 객체라 `?.`를 써도 된다(규칙 9 예외).</summary>
        private CancellationTokenSource m_ShotBlinkCts;

        private void Awake()
        {
            m_NetObject = GetComponent<NetworkObject>();
            m_Aim = GetComponent<GazeAim>();

            ReadTuning();
            m_BlinkTimer = EyeLookLogic.NextBlinkDelay(m_BlinkMin, m_BlinkMax, Random.value);

            Build();
        }

        /// <summary>
        /// CSV 수치를 캐시로 옮긴다. **여기 하나만 다시 부르면 값이 갈아 끼워진다** —
        /// 개발용 조절기 창(<c>Tools/CTC/Dev/눈·귀 수치 조절기</c>)이 그 경로를 쓴다.
        /// 쉬는 방향·기준축이 필요한 것(안쪽 한계)은 <see cref="Build"/> 뒤에 다시 잡는다.
        /// </summary>
        private void ReadTuning()
        {
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
            m_PastCenterDeg = Mathf.Max(0f, balance.EyeInwardPastCenterDeg);
        }

        /// <summary>쉬는 방향이 정해진 뒤에야 알 수 있는 한계 — 안쪽은 종마다 다르다.</summary>
        private void ReadLimits()
        {
            m_InwardMaxDeg = EyeLookLogic.InwardLimitDeg(m_RestR, m_GazePivot, m_PastCenterDeg);
            m_SwingMaxDeg = Mathf.Max(m_MaxAngleDeg, m_InwardMaxDeg);
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
            m_GazePivot = EyeLookLogic.GazePivot(m_HeadForwardLocal, m_RestR, m_RestL);
            ReadLimits();
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

            // **한 회전을 두 눈에 똑같이 먹인다**(2026-09-10) — 눈마다 따로 클램프하면 회전축이
            // 갈려 오른쪽을 보라 해도 한쪽만 가로로 돌았다(EyeLookLogic.GazeSwing 주석에 실측값).
            // 각도는 눈마다 다를 수 있다 — 안쪽으로 가는 눈이 더 간다(옆눈은 안 그러면 인중에 못 닿는다).
            Quaternion swing = EyeLookLogic.GazeSwing(m_GazePivot, aimLocal, m_SwingMaxDeg);
            Vector3 targetR = EyeLookLogic.EyeGaze(m_RestR, m_GazePivot, swing, m_MaxAngleDeg, m_InwardMaxDeg);
            Vector3 targetL = EyeLookLogic.EyeGaze(m_RestL, m_GazePivot, swing, m_MaxAngleDeg, m_InwardMaxDeg);
            m_GazeR = EyeLookLogic.Follow(m_GazeR, targetR, m_FollowSpeed, dt);
            m_GazeL = EyeLookLogic.Follow(m_GazeL, targetL, m_FollowSpeed, dt);

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

        /// <summary>
        /// **한 발 쐈다 — 그 순간 눈을 질끈 감는다.**
        ///
        /// 부르는 쪽은 <c>MousePopgun.TracerRpc</c> 하나다(<c>SendTo.Everyone</c>이라 **남의 쥐가
        /// 쏘는 것도 똑같이 보인다** — 여기에 소유자 검사를 넣으면 안 된다).
        ///
        /// 왜 눈꺼풀을 가진 이 클래스가 받는가: 눈꺼풀 스케일을 쓰는 곳이 <see cref="Apply"/> 하나뿐이라야
        /// 「평상 깜빡임」과 「쏠 때 감기」가 서로를 덮어쓸 수가 없다. 바깥에서 내부를 만지는 대신
        /// **부탁만 한다** — 부르는 쪽은 눈꺼풀이 몇 개인지도 모른다.
        ///
        /// 시간 계산은 <see cref="ShotBlinkLogic"/>이 전부 갖고 있다(EditMode가 거기를 덮는다).
        /// 여기서 하는 일은 그 위상을 프레임마다 밀어 주는 것뿐이다.
        /// </summary>
        /// <param name="_alreadyOut">
        /// 쏘기 직전에 총이 나와 있었나 — 클립을 고르는 값과 **같은 값**이다
        /// (<see cref="MouseGunVisualLogic.ClipForShot"/>). 클립마다 터지는 프레임이 달라
        /// 감는 시각도 같이 갈린다.
        /// </param>
        public void BlinkForShot(bool _alreadyOut)
        {
            if (!m_Ready) return;

            // **취소보다 먼저 읽는다** — 지금 돌고 있는 위상이 이어 감기의 근거다.
            float phase = ShotBlinkLogic.RetriggerPhase(m_ShotPhase, ShotBlinkLogic.DelayFor(_alreadyOut));

            CancelShotBlink();
            m_ShotBlinkCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ShotBlinkAsync(phase, m_ShotBlinkCts.Token).Forget();
        }

        /// <summary>
        /// 위상을 프레임마다 민다. 코루틴이 아니라 UniTask다(CTC 규칙 1).
        /// <see cref="Apply"/>가 <c>LateUpdate</c>에서 이 위상을 읽으므로 여기서는 스케일을 안 만진다 —
        /// 눈꺼풀을 쓰는 곳이 둘이 되면 마지막에 쓴 쪽이 조용히 이긴다.
        /// </summary>
        private async UniTaskVoid ShotBlinkAsync(float _phase, CancellationToken _token)
        {
            m_ShotPhase = _phase;
            try
            {
                while (!ShotBlinkLogic.IsComplete(m_ShotPhase))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _token);
                    m_ShotPhase += Time.deltaTime;
                }
            }
            // `using System;`을 안 쓴다 — 이 파일은 UnityEngine.Random을 쓰는데
            // System.Random과 이름이 겹쳐 컴파일이 깨진다(CS0104).
            catch (System.OperationCanceledException)
            {
                // 다음 발이 이어받았거나(그 쪽이 위상을 이미 정했다) 꺼지는 중이다.
                // 여기서 되돌리면 연사 중에 눈이 한 번 뜬다.
                return;
            }

            m_ShotPhase = float.PositiveInfinity;
        }

        private void CancelShotBlink()
        {
            m_ShotBlinkCts?.Cancel();
            m_ShotBlinkCts?.Dispose();
            m_ShotBlinkCts = null;
        }

        /// <summary>
        /// 꺼질 때는 **반드시 눈을 뜬 상태로 되돌린다.** 감는 도중에 꺼지면
        /// <c>LateUpdate</c>가 멈춰 눈꺼풀이 그 스케일 그대로 굳는다 — 다시 켜도 감긴 채다.
        /// </summary>
        private void OnDisable()
        {
            CancelShotBlink();
            m_ShotPhase = float.PositiveInfinity;

            // 파괴 중이면 얹어 둔 구가 이미 사라졌을 수 있다 — Unity 객체는 `!= null`로만 본다(규칙 9).
            if (m_Ready && m_EyeR != null && m_LidR != null) Apply();
        }

        /// <summary>
        /// 지금 눈꺼풀이 덮는 양(0 = 활짝, 1 = 완전히 감음). **평상 깜빡임과 쏠 때 감기는
        /// 서로를 모른다** — 더 많이 감은 쪽이 이긴다. 둘을 더하거나 순서로 덮으면
        /// 겹친 순간에 눈이 튄다.
        ///
        /// QA(<see cref="DevLidCover"/>)도 이 함수를 본다 — 둘로 나누면 「보이는 값」과
        /// 「재는 값」이 갈려 QA가 거짓말을 한다.
        /// </summary>
        private float CurrentCover()
        {
            float idle = m_BlinkElapsed >= 0f
                ? EyeLookLogic.BlinkCover(m_BlinkElapsed, m_BlinkSeconds)
                : 0f;
            return Mathf.Max(idle, ShotBlinkLogic.Cover(m_ShotPhase));
        }

        private void Apply()
        {
            float cover = CurrentCover();

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

        /// <summary>
        /// **내 몸이든 남의 몸이든 같은 곳에서 읽는다** — 복제되는 <see cref="GazeAim"/> 하나다.
        /// 소유자도 제 카메라를 직접 안 본다: 내가 보는 내 눈과 남이 보는 내 눈이 갈리면
        /// "저 고양이가 날 봤나"가 사람마다 다른 답을 준다.
        /// </summary>
        private Vector3 AimDirection()
        {
            if (m_Aim != null) return m_Aim.Direction;
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

        /// <summary>
        /// QA 전용 — 지금 눈이 얼마나 감겼나(0 = 활짝, 1 = 완전히 감음).
        /// **눈꺼풀 스케일이 아니라 그걸 정하는 값**이라, 「감았다고 말은 하는데 안 감겼다」가 안 생긴다.
        /// 총 흔적 기록(<c>TestManager.Gun</c>)이 프레임마다 이걸 적어 감는 시각을 **재게** 한다.
        /// </summary>
        public float DevLidCover()
        {
            return CurrentCover();
        }

        /// <summary>QA 전용 — 쏠 때 감기의 위상(초, 발사 순간이 0). 지연 구간이 음수로 보인다.</summary>
        public float DevShotBlinkPhase()
        {
            return m_ShotPhase;
        }

        /// <summary>QA 전용 — 지금 깜빡이게 한다(랜덤 주기를 기다리지 않고 그 프레임을 잡는다).</summary>
        public void DevBlinkNow()
        {
            m_BlinkElapsed = m_BlinkSeconds * 0.5f;   // 가장 많이 감은 순간
        }

        /// <summary>
        /// QA 전용 — 조준을 어디서 가져왔나. 배선 누락은 이 한 글자로 갈린다.
        /// `gaze:*`면 복제값을 읽고 있는 것이고, `body(noGazeAim)`면 **프리팹에 컴포넌트가 안 붙었다**
        /// (씬 재생성이 매번 프리팹을 새로 굽는다 — 빠지면 남의 눈이 다시 죽는다).
        /// </summary>
        public string DevAimSource()
        {
            if (m_Aim == null) return "body(noGazeAim)";
            return "gaze:" + m_Aim.DevSource();
        }

        /// <summary>QA 전용 — 내가 조종하는 몸인가(남의 눈을 보고 있는지 가르는 값).</summary>
        public bool DevMine()
        {
            return m_NetObject != null &&
                   LocalControlLogic.Mine(m_NetObject.IsOwner, m_NetObject.IsPlayerObject);
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

        /// <summary>
        /// QA 전용 — **두 눈동자가 같은 쪽을 보는지는 둘을 같이 재야 안다**(2026-09-10).
        /// 오른눈만 재던 훅으로는 좌우가 갈리는 것을 못 잡았다.
        /// </summary>
        public Vector3 DevPupilWorldLeft()
        {
            return m_PupilL != null ? m_PupilL.position : Vector3.zero;
        }

        /// <summary>QA 전용 — 왼눈 흰자의 월드 위치.</summary>
        public Vector3 DevEyeWorldLeft()
        {
            return m_EyeL != null ? m_EyeL.position : transform.position;
        }

        /// <summary>QA 전용 — 머리 본 로컬 시선(왼눈).</summary>
        public Vector3 DevGazeLeft()
        {
            return m_GazeL;
        }

        /// <summary>
        /// QA 전용 — 이 몸의 **머리 본**. 조절기 창이 얼굴 카메라를 여기에 매단다:
        /// 몸에 매달면 애니메이터가 머리를 굴릴 때 얼굴이 화면 밖으로 흘러 나간다.
        /// </summary>
        public Transform DevHead()
        {
            return m_Head;
        }

        /// <summary>QA 전용 — 두 눈이 함께 도는 기준축(머리 본 로컬). 굽는 값이 맞는지 재는 근거.</summary>
        public Vector3 DevGazePivot()
        {
            return m_GazePivot;
        }

        /// <summary>QA 전용 — "바깥한계/안쪽한계/쉬는각도"(도). 종마다 갈리는 값이라 눈으로 확인한다.</summary>
        public Vector3 DevLimitsDeg()
        {
            return new Vector3(m_MaxAngleDeg, m_InwardMaxDeg, Vector3.Angle(m_RestR, m_GazePivot));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// **CSV 수치를 지금 다시 읽어 그 자리에서 반영한다**(2026-09-10).
        ///
        /// 개발용 조절기 창이 부르는 단 하나의 진입점이다. 창은 <c>BalanceInfo</c>를 덮어쓰고
        /// 씬에 서 있는 몸마다 이걸 부른다 — 다시 플레이하거나 씬을 굽지 않아도 된다.
        /// 매 프레임 쓰는 값뿐 아니라 **얹을 때 한 번만 쓰던 것**(눈 파묻힘·눈동자 크기)도 같이 고친다.
        /// </summary>
        public void DevReloadTuning()
        {
            if (!m_Ready) return;

            ReadTuning();
            ReadLimits();

            // 얹을 때 한 번만 반영하던 것들 — 다시 안 잡으면 슬라이더가 안 먹는 것처럼 보인다.
            Vector3 centerR = m_EyeCenterLocal - m_RestR * (m_ScleraRadius * m_Inset);
            Vector3 centerL = EyeLookLogic.Mirror(centerR);
            m_EyeR.localPosition = centerR;
            m_EyeL.localPosition = centerL;
            m_LidR.localPosition = centerR;
            m_LidL.localPosition = centerL;
            m_PupilR.localScale = Vector3.one * m_PupilRatio;
            m_PupilL.localScale = Vector3.one * m_PupilRatio;

            Apply();
        }
#endif
    }
}
