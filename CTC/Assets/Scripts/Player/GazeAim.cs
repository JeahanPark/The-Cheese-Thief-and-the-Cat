using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 이 몸이 **지금 어디를 조준하는가**를 들고 복제한다(2026-09-10). 값만 가진다 —
    /// 그리는 것은 <see cref="EyeLook"/>이고, 여기는 아무것도 안 그린다(CTC 규칙 5·7).
    ///
    /// **왜 만들었나.** 눈동자는 원래 통신 0이었다. 내 몸은 내 카메라를, 남의 몸은 **그 몸의 정면**을
    /// 보게 해 놨는데, 눈이 쉬는 방향(rest)도 결국 머리 정면이라 <c>ClampGaze(정면, 정면) = 정면</c>이
    /// 됐다 — **남의 눈동자는 늘 가운데 붙어 아무것도 안 했다.** 3인칭이라 내 눈은 내가 거의 못 보므로
    /// 이 기능의 값어치는 전부 **남이 볼 때** 나온다. 그래서 조준 방향을 보내기로 했다(2026-09-10 지시).
    ///
    /// **얼마나 보내나.** 방향 하나를 <c>short</c> **2바이트**로 접고(<see cref="GazeAimLogic"/>),
    /// 초당 <see cref="SEND_HZ"/>회를 넘기지 않고, **값이 실제로 바뀐 칸일 때만** 쓴다.
    /// 가만히 서 있으면 0바이트다. 눈은 연출이라 이 이상 쓸 이유가 없다.
    ///
    /// **왜 몸 로컬인가**(머리 본이 아니라). 몸의 회전은 `ClientNetworkTransform`이 이미 복제해
    /// 보내는 쪽과 받는 쪽이 **같은 기준**을 갖는다. 머리 본은 애니메이션이 굴리는데 그 재생 위상은
    /// 클라마다 제 시계로 도는 로컬 값이라 기준이 어긋난다. 게다가 몸 로컬로 보내면 머리가 까딱일 때
    /// 눈이 **반대로 돌아 바라보던 곳을 붙든다**(사람 눈의 시선 안정화) — 머리 로컬이면 눈이 머리를
    /// 따라 같이 흔들린다.
    ///
    /// **소유자만 쓰고 모두가 읽는다.** 봇은 붙일 클라이언트가 없어 서버가 소유하므로 서버가 쓴다.
    /// </summary>
    public class GazeAim : NetworkBehaviour
    {
        /// <summary>
        /// 초당 최대 쓰기 횟수. 연출 상수라 CSV로 안 내린다 — 눈은 <c>EyeFollowSpeed</c>(10)로
        /// 지수 감쇠하며 따라가므로 83 ms 계단은 보간에 그대로 묻힌다.
        /// </summary>
        private const float SEND_HZ = 12f;

        private const float SEND_INTERVAL = 1f / SEND_HZ;

        /// <summary>봇이 "가고 있다"고 볼 최소 속도(m/s)의 제곱 — 이하면 몸 정면을 본다.</summary>
        private const float MOVING_SQR = 0.04f;

        private readonly NetworkVariable<short> m_Packed = new NetworkVariable<short>(
            GazeAimLogic.FORWARD, NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private Camera m_Camera;
        private CharacterController m_Controller;
        private bool m_Forced;          // QA — 조준을 못박았나
        private short m_ForcedPacked;
        private float m_NextSendAt;
        private int m_Writes;        // QA — 실제로 쓴 횟수(대역폭의 유일한 근거)
        private float m_WritesSince; // 그 셈을 시작한 시각

        /// <summary>
        /// 이 몸이 조준하는 **월드 방향**. 소유자도 이 값을 읽는다 — 내가 보는 내 눈과
        /// 남이 보는 내 눈이 **같은 값**에서 나와야 "저 고양이가 날 봤나"가 거짓말을 안 한다.
        /// </summary>
        public Vector3 Direction => transform.TransformDirection(GazeAimLogic.Unpack(m_Packed.Value));

        /// <summary>QA 전용 — 복제되는 원본 2바이트.</summary>
        public short DevPacked => m_Packed.Value;

        /// <summary>QA 전용 — 몸 로컬 각도 "yaw,pitch"(도).</summary>
        public Vector2 DevAngles
        {
            get
            {
                GazeAimLogic.UnpackAngles(m_Packed.Value, out float yaw, out float pitch);
                return new Vector2(yaw, pitch);
            }
        }

        /// <summary>QA 전용 — 이 몸이 실제로 네트워크에 쓴 횟수. 대역폭은 이 수 × 프레임 크기다.</summary>
        public int DevWrites => m_Writes;

        /// <summary>QA 전용 — 위 셈이 돌기 시작한 뒤 흐른 시간(초).</summary>
        public float DevWriteSeconds => Mathf.Max(0f, Time.time - m_WritesSince);

        /// <summary>QA 전용 — 쓰기 셈을 0부터 다시 센다.</summary>
        public void DevResetWrites()
        {
            m_Writes = 0;
            m_WritesSince = Time.time;
        }

        /// <summary>
        /// QA 전용 — 조준을 이 몸 로컬 각도에 **못박는다**(2026-09-10).
        /// 전/후 비교 시트는 좌·중·우를 **같은 값**에서 찍어야 비교가 되는데,
        /// 카메라를 손으로 돌려 맞추면 매번 다른 각도가 나온다.
        /// </summary>
        public void DevForce(float _yawDeg, float _pitchDeg)
        {
            m_Forced = true;
            m_ForcedPacked = GazeAimLogic.PackAngles(_yawDeg, _pitchDeg);
            if (IsSpawned && IsOwner) m_Packed.Value = m_ForcedPacked;
        }

        /// <summary>QA 전용 — 못박은 조준을 푼다.</summary>
        public void DevClearForce()
        {
            m_Forced = false;
        }

        /// <summary>QA 전용 — 조준을 어디서 가져오나("cam" | "bot" | "body").</summary>
        public string DevSource()
        {
            if (!IsSpawned) return "unspawned";
            if (!IsOwner) return "remote";                       // 복제만 받는다 — 안 쓴다
            if (!Mine()) return "bot";                           // 서버가 미는 몸
            return m_Camera != null || Camera.main != null ? "cam" : "body(noCamera)";
        }

        private void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_WritesSince = Time.time;
        }

        // 카메라가 그 프레임의 최종 회전을 가진 뒤에 읽어야 한 프레임 늦지 않는다
        // (시네머신은 LateUpdate에서 카메라를 놓는다) — EyeLook보다 먼저 돌 필요는 없다.
        // 한 프레임 늦어도 EyeLook이 지수 감쇠로 따라가므로 눈에 안 보인다.
        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner) return;

            float now = Time.time;
            if (now < m_NextSendAt) return;

            short packed = m_Forced
                ? m_ForcedPacked
                : GazeAimLogic.Pack(transform.InverseTransformDirection(SourceDirection()));
            if (packed == m_Packed.Value) return;   // 안 바뀌면 0바이트

            m_Packed.Value = packed;
            m_NextSendAt = now + SEND_INTERVAL;
            m_Writes++;
        }

        private bool Mine()
        {
            return NetworkObject != null &&
                   LocalControlLogic.Mine(IsOwner, NetworkObject.IsPlayerObject);
        }

        /// <summary>
        /// 사람의 몸은 **카메라 정면**(= 커서가 가리키는 곳)이다.
        ///
        /// 봇은 카메라가 없다 — 서버가 소유하고 몸만 있다. 그래서 **실제로 가고 있는 방향**
        /// (CharacterController의 수평 속도)을 쓴다. 몸은 <see cref="PlayerMovement"/>가
        /// `TurnSpeedDegPerSec`로 **천천히** 돌리므로 코너를 돌 때 가는 방향이 몸 정면보다 앞서고,
        /// 그 차이만큼 눈이 **가려는 쪽을 먼저 본다** — 사람이 모퉁이를 돌 때 하는 그 동작이다.
        /// 벽을 스치면 속도가 벽을 따라 꺾이므로 눈도 미끄러지는 쪽을 본다.
        ///
        /// **봇에게 "적을 본다"를 주지 않은 이유**: 봇의 시야 판정(<c>BotSenses</c>)은
        /// 개발 빌드 전용(<c>BotAgent</c>가 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`)이라 런타임
        /// 컴포넌트가 기댈 수 없고, 무엇보다 **사람에게는 없는 채널**이 된다 — 사람의 눈은
        /// 카메라를 따라갈 뿐 "적을 조준"하지 않는다. 봇만 적을 정확히 노려보면 봇 티가 나고,
        /// 쥐 입장에서는 「없던 정보」다.
        /// </summary>
        private Vector3 SourceDirection()
        {
            if (Mine())
            {
                if (m_Camera == null) m_Camera = Camera.main;
                if (m_Camera != null) return m_Camera.transform.forward;
                return transform.forward;
            }

            if (m_Controller != null)
            {
                Vector3 velocity = m_Controller.velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > MOVING_SQR) return velocity.normalized;
            }
            return transform.forward;
        }
    }
}
