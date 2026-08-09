using CTC.Runtime.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 살금살금(LCtrl 홀드). 발소리를 완전히 죽이는 대신 이동 속도를 절반 이하로 낮춘다.
    /// **고양이·쥐 둘 다 쓴다** (2026-08-10에 쥐로 확장, 그전까지는 고양이 전용 `CatSneak`이었다).
    ///
    /// 고양이 쪽 근거: 접근을 발소리(15m)가 항상 먼저 알려서 기습이라는 선택지가 없었다
    /// (2026-07-28 플레이 피드백). 이제 "빠르지만 들리는 추격"과 "느리지만 안 들리는 접근"을 고른다.
    ///
    /// 쥐 쪽 근거: 쥐에게도 같은 거래를 준다 — 6.0 → 2.7. **귀 사냥은 안 죽는다.**
    /// 파밍 소리(20m)가 발소리(8m)보다 훨씬 크고 잠행과 무관해서, 잠행은 "이동 중에만" 쓰는
    /// 수단이 된다. 그리고 들키면 고양이(6.7)에게 그 자리에서 잡힌다.
    ///
    /// 배율은 역할마다 다를 수 있어 **자기 `RoleTag`를 보고** CSV에서 읽는다 —
    /// 역할을 박아 두면 한쪽 수치를 고칠 때 다른 쪽이 조용히 따라온다.
    ///
    /// 잠행 여부는 원격 클라이언트의 발소리 재생에도 쓰이므로 복제한다(소유자 기록).
    /// </summary>
    public class Sneak : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> m_Sneaking = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool Sneaking => m_Sneaking.Value;

        private bool m_DevForced; // QA 강제 — 키를 누르고 있는 것과 같게 취급한다

        /// <summary>잠행 중 이속 배율. 수치 원본은 CSV(`PlayerInfo.csv`의 자기 역할 행).</summary>
        public float SpeedMultiplier
        {
            get
            {
                RoleTag tag = GetComponent<RoleTag>();
                Lobby.RoleId role = tag != null ? tag.Role : Lobby.RoleId.Cat;
                return Info.InfoManager.Instance.PlayerOf(role).SneakSpeedMultiplier;
            }
        }

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
