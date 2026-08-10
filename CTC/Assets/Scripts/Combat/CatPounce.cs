using CTC.Runtime.Core;
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

        /// <summary>강화 중첩 스트림 — 전직 창이 "다 올렸나"를 폴링 없이 안다(규칙 3).</summary>
        public R3.Observable<int> DamageStacksChanged =>
            m_DamageStacks.AsObservableWithCurrent();

        /// <summary>서버 전용(ShopController). 캡은 ShopLogic.MAX_UPGRADE_STACKS.</summary>
        public void ServerAddDamageStack()
        {
            if (!IsServer || !ShopLogic.CanAddStack(m_DamageStacks.Value)) return;
            m_DamageStacks.Value++;
        }

        private void Update()
        {
            if (!IsOwner || Cursor.lockState != CursorLockMode.Locked) return;

            if (Core.UiFocus.BlocksGameplay) return; // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
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

                // 쥐·구조물 + 제3세력(강아지). 제 진영만 아니면 때린다 — v3.8 5번에서 규칙을 하나로 모았다.
                RoleTag tag = target.GetComponent<RoleTag>();
                bool targetIsPlayer = target.NetworkObject != null && target.NetworkObject.IsPlayerObject;
                if (tag == null || !CombatLogic.CanAttack(RoleId.Cat, tag.Role, targetIsPlayer)) continue;

                if (!CombatLogic.IsInPounceArc(
                        transform.position, transform.forward, target.transform.position,
                        m_Range, m_HalfAngleDeg)) continue;

                // v3 진입형 요새: 벽 너머 타격 금지 — 요새 안 쥐는 요새(외벽)부터 부숴야 한다
                if (IsOccluded(target)) continue;

                // 보상 대상 여부는 데미지 전에 캡처 — 사망 시 PlayerDeath가 Died 이벤트에서
                // 동기 despawn하므로 이후에는 IsPlayerObject가 이미 false다
                bool isPlayerTarget = targetIsPlayer;

                // 맵 지형물(벽감)은 쥐가 쓴 돈이 아니라 값을 치르지 않는다 — 안 그러면
                // '벽 때리기가 최고 효율'이던 v3.5의 실패가 되살아난다(2026-08-09)
                bool exempt = target.GetComponent<BountyExempt>() != null;

                int damage = ShopLogic.PounceDamage(m_Damage, m_DamageStacks.Value);
                // 불(v3.9) — 스킬이 켜져 있는 동안만 곱한다. 판정을 서버가 하므로 클라가 못 속인다.
                damage = Mathf.RoundToInt(damage * FireMultiplier());
                int dealt = target.TakeDamage(damage);
                // 장난기 적립(v3.6): 플레이어 실피해 ×0.5 + 킬 보너스, 구조물은 ×0.25.
                // 요새 1채 300pt는 벽 때리기를 최고 효율 수입원으로 만들었다.
                if (myWallet != null && !exempt)
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

        // 불 전직 + 스킬 활성일 때만 1이 아니다. 컴포넌트가 없으면(구 프리팹) 항상 1이라
        // 전직을 모르는 씬에서도 그대로 돈다.
        private float FireMultiplier()
        {
            var cls = GetComponent<Player.CatClass>();
            var skill = GetComponent<Player.CatSkill>();
            if (cls == null || skill == null) return 1f;
            if (cls.Value != Player.CatClassId.Fire || !skill.Active) return 1f;

            return Player.CatClassLogic.FireDamageMultiplier;
        }

        // 차폐 판정은 SightLine이 단독으로 갖는다 — 강아지(v3.8 5번)가 같은 규칙을 써야 해서
        // 여기서 꺼냈다. 규칙이 갈라지면 요새 안이 덮치기에만, 또는 강아지에게만 안전해진다.
        private bool IsOccluded(Health _target) => SightLine.IsBlocked(transform, _target.transform);
    }
}
