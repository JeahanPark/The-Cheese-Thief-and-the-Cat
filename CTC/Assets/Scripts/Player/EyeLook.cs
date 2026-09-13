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
    /// **팩 메시의 셰이프키를 민다**(2026-09-13). 2026-09-08부터 09-13까지는 흰자·눈동자·눈꺼풀
    /// **구 세 개를 머리 본에 얹는** 방식이었다. 그 전제는 「팩 메시는 눈동자를 못 움직인다」였는데
    /// **틀렸다** — 팩 메시에 눈 셰이프키가 30개 있고 `Mouse_LOD0~3`·`Cat_LOD0` 전부 갖고 있다.
    /// 얹은 구는 옆에서 보면 머리에서 떨어져 솟았고, CSV 손잡이 다섯이 그걸 누르려고 있었다.
    /// A/B 검수(2026-09-13)에서 셰이프키가 선택됐고 구 경로는 이 커밋에서 지웠다.
    ///
    /// **못 하게 된 것이 하나 있다**: 팩 셰이프키는 두 눈을 거울 대칭으로 묶어 움직여서
    /// 「왼눈은 앞, 오른눈은 뒤」 같은 **비대칭 시선**이 안 된다. 그래서 두 눈의 편차를 평균해
    /// 표현 가능한 성분만 쓴다(<see cref="EyeBlendShapeLogic"/>). 계산은 여전히 눈마다 따로 한다 —
    /// 눈마다 다른 한계(안쪽이 더 간다)를 먹인 뒤에 평균해야 옳다.
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
        [SerializeField] private string m_HeadBoneName = "head";

        [Tooltip("오른눈 중심 — 머리 본 로컬. 왼눈은 x를 뒤집는다. 이제 그리는 데는 안 쓰고 " +
                 "**근접 촬영의 초점**으로만 쓴다(DevEyeWorldRight).")]
        [SerializeField] private Vector3 m_EyeCenterLocal = new Vector3(0.00132f, 0.00100f, 0.00034f);

        [Tooltip("오른눈이 쉴 때 보는 방향 — 머리 본 로컬. 왼눈은 x를 뒤집는다.")]
        [SerializeField] private Vector3 m_EyeAxisLocal = new Vector3(0.892f, 0.396f, 0.216f);

        [Tooltip("머리 본 로컬 **정면** — 여기를 조준하면 두 눈동자가 흰자 한가운데 앉는다. " +
                 "리그가 종마다 달라 굽는다(고양이 +z · 쥐 +y). 0이면 두 눈의 대칭축으로 물러선다.")]
        [SerializeField] private Vector3 m_HeadForwardLocal = new Vector3(0f, 0f, 1f);

        private Transform m_Head;
        private NetworkObject m_NetObject;
        private GazeAim m_Aim;

        private Vector3 m_RestR, m_RestL;     // 머리 본 로컬
        private Vector3 m_GazePivot;          // 두 눈이 함께 도는 기준축(머리 본 로컬)
        private Vector3 m_GazeR, m_GazeL;

        private float m_PastCenterDeg;        // 안쪽 눈이 정면을 지나칠 수 있는 각(CSV)
        private float m_MaxAngleDeg;      // 바깥·위아래 한계(쉬는 방향 기준)
        private float m_InwardMaxDeg;     // 안쪽 한계 — 쉬는 각도 + CSV의 «정면을 지나치는 각»
        private float m_SwingMaxDeg;      // 공유 회전의 한계 = 둘 중 큰 쪽
        private float m_FollowSpeed;
        private float m_BlinkMin;
        private float m_BlinkMax;
        private float m_BlinkSeconds;
        private float m_BlinkTimer;           // 다음 깜빡임까지 남은 시간
        private float m_BlinkElapsed = -1f;   // 음수면 안 깜빡이는 중
        private bool m_Ready;

        /// <summary>가중치를 렌더러에 넣는 곳 — 못 붙으면 null이고 이 컴포넌트는 아무것도 안 한다.</summary>
        private EyeBlendShapes m_Shapes;

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

            // **크기 손잡이 다섯은 2026-09-13에 사라졌다**(EyeScleraScale · EyePupilRatio ·
            // EyePupilOffsetRatio · EyeFlatten · EyeInsetRatio). 전부 얹은 구가 얼굴에서
            // 솟지 않게 누르던 값이라, 메시 자체를 미는 방식에서는 누를 것이 없다.
            m_PastCenterDeg = Mathf.Max(0f, balance.EyeInwardPastCenterDeg);
        }

        /// <summary>쉬는 방향이 정해진 뒤에야 알 수 있는 한계 — 안쪽은 종마다 다르다.</summary>
        private void ReadLimits()
        {
            m_InwardMaxDeg = EyeLookLogic.InwardLimitDeg(m_RestR, m_GazePivot, m_PastCenterDeg);
            m_SwingMaxDeg = Mathf.Max(m_MaxAngleDeg, m_InwardMaxDeg);
        }

        /// <summary>머리 본·쉬는 방향·회전 한계 — 시선 계산이 시작되기 전에 있어야 하는 것들.</summary>
        private bool SetupDirections()
        {
            m_Head = FindDeep(transform, m_HeadBoneName);
            if (m_Head == null)
            {
                Debug.LogWarning("[EyeLook] 머리 본을 못 찾았다 — 눈을 굴리지 않는다: " + m_HeadBoneName);
                return false;
            }

            m_RestR = m_EyeAxisLocal.normalized;
            m_RestL = EyeLookLogic.Mirror(m_RestR);
            m_GazePivot = EyeLookLogic.GazePivot(m_HeadForwardLocal, m_RestR, m_RestL);
            ReadLimits();
            m_GazeR = m_RestR;
            m_GazeL = m_RestL;
            return true;
        }

        /// <summary>
        /// **새 오브젝트를 하나도 안 만든다.** 팩 메시가 이미 가진
        /// eyes.look* / eyes.blink를 밀 뿐이다.
        /// </summary>
        private void Build()
        {
            if (!SetupDirections()) return;

            m_Shapes = GetComponent<EyeBlendShapes>();
            // AddComponent는 그 자리에서 Awake를 돌리므로 Ready가 바로 정해진다.
            if (m_Shapes == null) m_Shapes = gameObject.AddComponent<EyeBlendShapes>();
            if (!m_Shapes.Ready)
            {
                // 컴포넌트가 이미 경고를 찍었다 — 여기서 또 찍지 않는다.
                m_Shapes = null;
                return;
            }

            Apply();
            m_Ready = true;
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
        /// <c>LateUpdate</c>가 멈춰 blink 가중치가 그 값 그대로 굳는다 — 다시 켜도 감긴 채다.
        /// </summary>
        private void OnDisable()
        {
            CancelShotBlink();
            m_ShotPhase = float.PositiveInfinity;

            if (m_Ready) Apply();
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
            return Mathf.Max(IdleCover(), ShotBlinkLogic.Cover(m_ShotPhase));
        }

        /// <summary>평상 깜빡임만 — <c>eyes.blink</c>로 간다.</summary>
        private float IdleCover()
        {
            return m_BlinkElapsed >= 0f
                ? EyeLookLogic.BlinkCover(m_BlinkElapsed, m_BlinkSeconds)
                : 0f;
        }

        private void Apply()
        {
            if (m_Shapes == null) return;

            // 감는 양은 둘로 갈린다(2026-09-13): 평상 깜빡임은 blink, 쏠 때 감기는 **질끈(squint, > <)**.
            // 「더 감은 쪽이 이긴다」는 그대로다 — 쐐기가 들어오는 만큼 blink가 물러난다(EyeBlendShapeLogic).
            // QA가 읽는 CurrentCover()는 여전히 둘의 max라 「얼마나 감았나」의 뜻이 안 바뀐다.
            m_Shapes.Apply(EyeBlendShapeLogic.Weights(
                m_RestR, m_RestL, m_GazeR, m_GazeL, m_GazePivot, m_MaxAngleDeg,
                IdleCover(), ShotBlinkLogic.Cover(m_ShotPhase)));
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

        /// <summary>
        /// QA 전용 — 오른눈의 월드 위치(고정 시점 근접 촬영의 **초점**).
        /// 얹은 구가 사라졌으므로(2026-09-13) 프리팹에 구운 머리 로컬 좌표로 직접 푼다 —
        /// 그리는 데는 안 쓰지만 「눈을 화면 가운데 두는」 데는 여전히 이 값이 원본이다.
        /// </summary>
        public Vector3 DevEyeWorldRight()
        {
            return m_Head != null ? m_Head.TransformPoint(m_EyeCenterLocal) : transform.position;
        }

        /// <summary>QA 전용 — 머리 본 로컬 시선(오른눈).</summary>
        public Vector3 DevGazeRight()
        {
            return m_GazeR;
        }

        /// <summary>QA 전용 — 왼눈의 월드 위치(<see cref="DevEyeWorldRight"/>의 거울).</summary>
        public Vector3 DevEyeWorldLeft()
        {
            return m_Head != null
                ? m_Head.TransformPoint(EyeLookLogic.Mirror(m_EyeCenterLocal))
                : transform.position;
        }

        /// <summary>
        /// QA 전용 — **실제로 렌더러에 들어간** 셰이프키 가중치. 눈이 진짜 움직이는지 재는
        /// 유일한 근거다(예전엔 눈동자 구의 월드 위치가 그 자리였다).
        /// 계산값이 아니라 되읽은 값이라, 배선이 끊기면 여기서 0으로 드러난다.
        /// </summary>
        public string DevEyeWeights()
        {
            return m_Shapes != null ? m_Shapes.DevWeights().ToString() : "(셰이프키 없음)";
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
        ///
        /// **「얹을 때 한 번만 쓰던 것」을 다시 잡는 구간이 2026-09-13에 사라졌다** —
        /// 구를 안 만드니 한 번만 쓰는 값 자체가 없다. 이제 전부 매 프레임 값이다.
        /// </summary>
        public void DevReloadTuning()
        {
            if (!m_Ready) return;

            ReadTuning();
            ReadLimits();
            Apply();
        }
#endif
    }
}
