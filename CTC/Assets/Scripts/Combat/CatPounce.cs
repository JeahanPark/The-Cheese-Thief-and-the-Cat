using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 고양이 덮치기. 소유자 좌클릭 → ServerRpc → 서버가 전방 부채꼴 내
    /// 살아있는 쥐 전원에게 데미지. 처치 시 고양이 지갑에 보상.
    /// </summary>
    public class CatPounce : NetworkBehaviour
    {
        [SerializeField] private float m_Range = 2.2f;
        [SerializeField] private float m_HalfAngleDeg = 60f;
        [SerializeField] private int m_Damage = 60;
        [SerializeField] private float m_CooldownSeconds = 2.5f;
        [SerializeField] private int m_KillReward = 50;

        private double m_LastUseTime = -999.0;

        private void Update()
        {
            if (!IsOwner || Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            PounceRpc();
        }

        /// <summary>QA 자동화 진입점 (TestManager/wesqa).</summary>
        public void DevPounce() => PounceRpc();

        [Rpc(SendTo.Server)]
        private void PounceRpc()
        {
            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, m_CooldownSeconds)) return;
            m_LastUseTime = now;

            CheeseWallet myWallet = GetComponent<CheeseWallet>();

            foreach (Health target in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (target.IsDead) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                if (!CombatLogic.IsInPounceArc(
                        transform.position, transform.forward, target.transform.position,
                        m_Range, m_HalfAngleDeg)) continue;

                target.TakeDamage(m_Damage);
                if (target.IsDead && myWallet != null)
                {
                    myWallet.AddCheese(m_KillReward);
                }
            }
        }
    }
}
