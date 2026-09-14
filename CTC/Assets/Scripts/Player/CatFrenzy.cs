using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 광분(고양이 모면 테크, 테크 v3 계획 2b) — 몇 초 동안 덮치기 피해가 곱해지고 쿨이 돈다.
    ///
    /// <see cref="CatSkill"/>의 「액티브 지속 + 쿨」을 그대로 본떴지만 **전직과는 무관하다**.
    /// 다른 점 셋:
    /// ① 입력을 읽지 않는다 — 켜는 것은 핫바 숫자키이고 그 길은 <c>ShopController.Use</c>가 갖는다.
    ///    여기서 키를 또 읽으면 같은 프레임에 두 번 켜진다.
    /// ② 지속·쿨·배율은 전부 TechInfo.csv의 그 레벨 행(P1 배율 · P2 지속 · P3 쿨)이다.
    /// ③ 여기는 "켜졌다/꺼졌다"만 복제한다 — 피해 배율은 <c>CatPounce</c>가 이 값을 보고 곱한다.
    ///
    /// **켤 때 소리가 난다.** 큰 효과에는 늘 소리가 붙는다 — 쥐가 "지금 뭔가 켜졌다"를 알아야
    /// 흩어질 수 있고, 그게 액티브로 만든 이유다(CatSkill과 같은 규칙).
    /// </summary>
    public class CatFrenzy : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> m_Active = new NetworkVariable<bool>();
        // 서버 시각(NetworkManager.ServerTime) 기준. 소유자 HUD(능력 바·핫바)가 남은 초를 스스로
        // 계산한다 — 서버 float를 복제하면 클라에서 쿨다운이 늘 0으로 보인다(2026-08-17 CatSkill).
        private readonly NetworkVariable<double> m_ReadyAt = new NetworkVariable<double>();
        private readonly NetworkVariable<double> m_ActiveUntil = new NetworkVariable<double>();

        // 광분 발동 동작(자작 Frenzy, 2026-09-13 — 팩 Spin은 발레 회전이었다). 지속(10초)이 아니라
        // "켜졌다"를 알리는 한 번짜리다 — 10초 내내 자세를 유지하면 「아직 광분 중」이 없던 정보가 된다.
        private const float FRENZY_ANIM_SECONDS = 0.6f;

        private TechSlots m_Slots;
        private PlayerAnimationDriver m_AnimationDriver; // 켤 때마다 찾지 않는다

        /// <summary>지금 켜져 있는가(전원 읽기 — CatPounce·HUD가 본다).</summary>
        public bool Active => m_Active.Value;

        public Observable<bool> ActiveChanged => m_Active.AsObservableWithCurrent();

        /// <summary>남은 쿨다운(초) — 전 클라에서 같은 값(서버 시각 기준).</summary>
        public float CooldownRemaining => RemainingUntil(m_ReadyAt.Value);

        /// <summary>지금 레벨의 쿨다운 전체(초). 미보유면 0 — 능력 바가 빈 칸으로 그린다.</summary>
        public float CooldownTotal => ParamOf(3);

        /// <summary>남은 지속(초). 꺼져 있으면 0.</summary>
        public float ActiveRemaining => m_Active.Value ? RemainingUntil(m_ActiveUntil.Value) : 0f;

        public float ActiveTotal => ParamOf(2);

        /// <summary>지금 곱해지는 덮치기 배율 — 꺼져 있으면 1. CatPounce가 이걸 읽는다.</summary>
        public float PounceMultiplier => CatFrenzyLogic.PounceMultiplier(m_Active.Value, ParamOf(1));

        private void Awake()
        {
            m_Slots = GetComponent<TechSlots>();
            m_AnimationDriver = GetComponentInChildren<PlayerAnimationDriver>();
        }

        private void Update()
        {
            if (IsServer) TickServer();
        }

        private void TickServer()
        {
            if (m_Active.Value && NetworkManager.ServerTime.Time >= m_ActiveUntil.Value)
            {
                m_Active.Value = false;
            }
        }

        /// <summary>
        /// 서버 전용 — 핫바가 누른 결과(ShopController.UseRpc)가 여기로 온다.
        /// 보유·쿨·중복 판정은 순수 로직이 갖는다.
        /// </summary>
        public void ServerTryUse()
        {
            if (!IsServer) return;

            int level = Level;
            double now = NetworkManager.ServerTime.Time;
            if (!CatFrenzyLogic.CanUse(level, m_ReadyAt.Value, now, m_Active.Value)) return;

            float seconds = TechLogic.ParamOf(TechId.CatFrenzy, level, 2);
            float cooldown = TechLogic.ParamOf(TechId.CatFrenzy, level, 3);
            if (seconds <= 0f)
            {
                // 표를 못 읽었다 — 지속 0으로 켜면 같은 프레임에 꺼지고 쿨만 먹는다.
                GameDebug.LogWarning($"[CatFrenzy] 지속을 TechInfo에서 못 읽었다(레벨 {level}) — 켜지 않는다.");
                return;
            }

            m_Active.Value = true;
            m_ActiveUntil.Value = now + seconds;
            m_ReadyAt.Value = now + cooldown;

            FrenzyFxRpc();
        }

        /// <summary>남은 쿨다운을 서버에 묻지 않고 알아야 하는 곳(핫바 판정)이 쓴다.</summary>
        public bool Ready => CatFrenzyLogic.CanUse(Level, m_ReadyAt.Value, ServerNow, m_Active.Value);

        private int Level => m_Slots != null ? m_Slots.LevelOf(TechId.CatFrenzy) : 0;

        private double ServerNow => IsSpawned ? NetworkManager.ServerTime.Time : 0.0;

        private float ParamOf(int _index) => TechLogic.ParamOf(TechId.CatFrenzy, Level, _index);

        private float RemainingUntil(double _serverTime)
        {
            if (!IsSpawned) return 0f;
            return Mathf.Max(0f, (float)(_serverTime - NetworkManager.ServerTime.Time));
        }

        [Rpc(SendTo.Everyone)]
        private void FrenzyFxRpc()
        {
            // 위치가 실린 3D 소리다 — "어디서 켜졌나"까지 알려 줘야 도망칠 방향이 생긴다.
            Audio.SfxPlayer.PlayAt(Audio.SfxId.CatSkill, transform.position, 1f,
                Info.InfoManager.Instance.Balance.CatSkillSoundMeters);

            // 소리와 같은 이유로 **모션도 전 클라**다 — 쥐가 "지금 뭔가 켜졌다"를 눈으로도
            // 알아야 흩어질 수 있다. 애니메이터 상태는 복제되지 않으므로 이 Rpc가 유일한 길이다.
            if (m_AnimationDriver != null)
            {
                m_AnimationDriver.PlayOneShot(AnimationStateLogic.Frenzy, FRENZY_ANIM_SECONDS);
            }
            Fx.FxPlayer.PlayAt(Fx.FxId.FrenzyShock, transform.position);   // 한 번만 — 오라는 안 둔다(위와 같은 이유)
        }
    }
}
