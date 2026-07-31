using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 고양이 덮치기. 소유자 좌클릭 → ServerRpc → 서버가 전방 부채꼴 내
    /// 살아있는 쥐 전원에게 데미지. 실데미지만큼 장난기(Fun) 적립 + 플레이어 킬 보너스(스펙 v3.2).
    /// </summary>
    public class CatPounce : NetworkBehaviour
    {
        // 네 값 모두 PlayerInfo.csv가 원본 — 생성기가 프리팹에 굽는다(씬 재생성 필요).
        [SerializeField] private float m_Range = 2.2f;
        [SerializeField] private float m_HalfAngleDeg = 60f;
        [SerializeField] private int m_Damage = 60;
        [SerializeField] private float m_CooldownSeconds = 2.5f;

        // 공격 강화 스택(상점). 서버 기록.
        private readonly NetworkVariable<int> m_DamageStacks = new NetworkVariable<int>();

        private double m_LastUseTime = -999.0;

        public int DamageStacks => m_DamageStacks.Value;

        /// <summary>서버 전용(ShopController). 캡은 ShopLogic.MAX_UPGRADE_STACKS.</summary>
        public void ServerAddDamageStack()
        {
            if (!IsServer || !ShopLogic.CanAddStack(m_DamageStacks.Value)) return;
            m_DamageStacks.Value++;
        }

        private void Update()
        {
            if (!IsOwner || Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            PounceRpc();
        }

        /// <summary>QA 자동화 진입점 (TestManager/wesqa).</summary>
        public void DevPounce() => PounceRpc();

        [Rpc(SendTo.Server, RequireOwnership = true)] // 타인 고양이 쿨다운 강제 소모 차단 — 리뷰 I2
        private void PounceRpc()
        {
            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, m_CooldownSeconds)) return;
            m_LastUseTime = now;

            AttackAnimRpc();

            CheeseWallet myWallet = GetComponent<CheeseWallet>();

            foreach (Health target in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (target.IsDead) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                if (!CombatLogic.IsInPounceArc(
                        transform.position, transform.forward, target.transform.position,
                        m_Range, m_HalfAngleDeg)) continue;

                // v3 진입형 요새: 벽 너머 타격 금지 — 요새 안 쥐는 요새(외벽)부터 부숴야 한다
                if (IsOccluded(target)) continue;

                // 보상 대상 여부는 데미지 전에 캡처 — 사망 시 PlayerDeath가 Died 이벤트에서
                // 동기 despawn하므로 이후에는 IsPlayerObject가 이미 false다
                bool isPlayerTarget = target.NetworkObject.IsPlayerObject;

                int dealt = target.TakeDamage(ShopLogic.PounceDamage(m_Damage, m_DamageStacks.Value));
                // 장난기 적립(v3.6): 플레이어 실피해 ×0.5 + 킬 보너스, 구조물은 ×0.25.
                // 요새 1채 300pt는 벽 때리기를 최고 효율 수입원으로 만들었다.
                if (myWallet != null)
                {
                    myWallet.AddCheese(FunLogic.AccruedFun(
                        dealt, target.IsDead && isPlayerTarget, !isPlayerTarget));
                }
            }
        }

        // 덮치기 모션 지속(이동 애니메이션 잠금 시간). 쿨다운 통과 시에만 전 클라 재생.
        private const float ATTACK_ANIM_SECONDS = 0.6f;

        [Rpc(SendTo.Everyone)]
        private void AttackAnimRpc()
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.PounceSwing, transform.position);

            Player.PlayerAnimationDriver driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null)
            {
                driver.PlayOneShot(Player.AnimationStateLogic.Attack, ATTACK_ANIM_SECONDS);
            }
        }

        // 차폐 레이 높이 — 요새 입구(0.75)보다 높고 요새 벽(1.2)보다 낮게:
        // 열린 문으로는 절대 안 새고, 외벽이 서 있는 한 내부가 보호된다(원작 벙커).
        private const float OCCLUSION_RAY_HEIGHT = 1.0f;

        // 시전자→대상 차폐 레이 — 둘 자신의 콜라이더는 차단으로 안 친다
        // (요새 자체를 때릴 때 그 요새의 외벽에 막히면 안 됨). 트리거 무시.
        // 플레이어 계층(CC 루트 — Quirky 모델 자식 콜라이더 포함)도 차폐물이 아님:
        // 요새 안 쥐가 몸으로 요새 레이를 가려 요새가 무적이 되는 것 방지. 차폐는 구조물/지형만.
        private bool IsOccluded(Health _target)
        {
            Vector3 from = transform.position + Vector3.up * OCCLUSION_RAY_HEIGHT;
            Vector3 to = _target.transform.position + Vector3.up * OCCLUSION_RAY_HEIGHT;
            Vector3 dir = to - from;

            foreach (RaycastHit hit in Physics.RaycastAll(
                         from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform hitRoot = hit.transform.root;
                if (hitRoot == transform.root || hitRoot == _target.transform.root) continue;
                if (hitRoot.GetComponent<CharacterController>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
