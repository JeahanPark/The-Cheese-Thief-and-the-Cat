using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 고양이 살금살금(LCtrl 홀드). 발소리를 완전히 죽이는 대신 이동 속도를 절반 이하로 낮춘다.
    ///
    /// 고양이의 접근은 지금까지 발소리(15m)가 항상 먼저 알렸다 — 쥐는 소리만 듣고 도망치면 됐고
    /// 고양이는 기습이라는 선택지가 없었다(2026-07-28 플레이 피드백). 이제 고양이는
    /// "빠르지만 들리는 추격"과 "느리지만 안 들리는 접근" 중에 고른다.
    ///
    /// 잠행 여부는 원격 클라이언트의 발소리 재생에도 쓰이므로 복제한다(소유자 기록).
    /// </summary>
    public class CatSneak : NetworkBehaviour
    {
        /// <summary>
        /// 잠행 중 이속 배율 — 6.7 → 3.0. 걷는 쥐(6)보다 느려야 추격 대체재가 되지 않는다.
        /// 수치 원본은 CSV(PlayerInfo.csv의 Cat 행) — 2026-07-31 데이터 분리.
        /// </summary>
        public static float SPEED_MULTIPLIER =>
            Info.InfoManager.Instance.PlayerOf(Lobby.RoleId.Cat).SneakSpeedMultiplier;

        private readonly NetworkVariable<bool> m_Sneaking = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool Sneaking => m_Sneaking.Value;

        private bool m_DevForced; // QA 강제 — 키를 누르고 있는 것과 같게 취급한다

        /// <summary>QA 전용 — 키 입력 없이 잠행을 켜고 끈다(Update가 매 프레임 되돌리지 않도록 래치).</summary>
        public void DevSetSneak(bool _sneaking)
        {
            m_DevForced = _sneaking;
            if (IsOwner && IsSpawned) m_Sneaking.Value = _sneaking;
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;

            if (Core.UiFocus.BlocksGameplay) return; // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // 커서가 풀린 동안(로비·인게임 콘솔)은 조작으로 치지 않는다 — 다른 창에서 Ctrl 조합을 누를 때
            bool held = m_DevForced
                        || (Cursor.lockState == CursorLockMode.Locked && kb.leftCtrlKey.isPressed);
            if (m_Sneaking.Value != held) m_Sneaking.Value = held;
        }
    }
}
