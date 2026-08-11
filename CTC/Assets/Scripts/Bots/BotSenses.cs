#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Audio;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 봇 하나가 지금 **알 수 있는 것**만 걸러 낸다 (2026-08-11).
    ///
    /// 서버는 전지적이다. 이 관문이 없으면 고양이 봇이 모든 쥐의 좌표를 알고,
    /// 그 판의 승률은 "소리 밸런스가 좋다"가 아니라 "봇이 반칙했다"를 뜻하게 된다.
    ///
    /// 세 가지만 통과시킨다.
    /// ① 눈 — 카메라와 같은 각도 + 레이캐스트 가림 검사
    /// ② 지속음 — 상대가 지금 소리를 내고 있고 그 반경 안인가(발소리)
    /// ③ 단발음 — <see cref="SoundLog"/>에 남은 사건 중 반경 안인 것
    ///
    /// 소리로 얻은 자리는 **흐린다**. 사람은 3D 오디오로 "대략 저쪽"을 알 뿐이다.
    /// </summary>
    public class BotSenses
    {
        /// <summary>1m당 이만큼 어긋난다. 0이면 고양이가 신이 되고, 크면 소리가 무의미해진다.</summary>
        public const float HEAR_ERROR_PER_METER = 0.15f;

        private readonly Transform m_Self;
        private readonly RoleId m_Role;
        private readonly float m_FovDeg;

        private readonly List<Transform> m_Visible = new List<Transform>();

        public IReadOnlyList<Transform> Visible => m_Visible;

        /// <summary>가장 값진 소리 단서(흐린 자리). 없으면 <see cref="HasLead"/>가 false.</summary>
        public Vector3 Lead { get; private set; }
        public bool HasLead { get; private set; }

        public BotSenses(Transform _self, RoleId _role, float _fovDeg)
        {
            m_Self = _self;
            m_Role = _role;
            m_FovDeg = _fovDeg;
        }

        /// <summary>매 판단마다 한 번. 무거우니 매 프레임 부르지 않는다.</summary>
        public void Observe(SoundLog _sound, float _now)
        {
            m_Visible.Clear();
            HasLead = false;

            RoleId prey = m_Role == RoleId.Cat ? RoleId.Mouse : RoleId.Cat;
            float bestScore = 0f;
            Vector3 bestLead = Vector3.zero;

            foreach (RoleTag tag in Object.FindObjectsByType<RoleTag>(FindObjectsSortMode.None))
            {
                if (tag.Role != prey) continue;
                if (tag.GetComponent<Player.PlayerMovement>() == null) continue;   // 몸만 — 설치물 제외

                var jailed = tag.GetComponent<JailedTag>();
                if (jailed != null && jailed.Jailed) continue;                     // 갇힌 쥐는 사냥감이 아니다

                Transform other = tag.transform;
                if (CanSee(other)) { m_Visible.Add(other); continue; }

                // 발소리 — 지금 실제로 나고 있는 소리만. 반경도 실제 AudioSource에서 읽는다.
                float range = ActiveFootstepRange(other);
                if (range <= 0f) continue;
                if (!BotSenseLogic.CanHear(m_Self.position, other.position, range)) continue;

                float distance = Vector3.Distance(m_Self.position, other.position);
                float score = CatBotLogic.LeadScore(distance, 0f);
                if (score <= bestScore) continue;

                bestScore = score;
                bestLead = Blur(other.position);
            }

            // 단발음 — 벽 파괴·파밍·스킬. 사건이라 시간이 지나면 값이 떨어진다.
            if (_sound != null)
            {
                foreach (SoundLog.Heard heard in _sound.Recent)
                {
                    if (!BotSenseLogic.CanHear(m_Self.position, heard.Position, heard.MaxDistance)) continue;

                    float distance = Vector3.Distance(m_Self.position, heard.Position);
                    float score = CatBotLogic.LeadScore(distance, _now - heard.Time);
                    if (score <= bestScore) continue;

                    bestScore = score;
                    bestLead = Blur(heard.Position);
                }
            }

            if (bestScore <= 0f) return;
            Lead = bestLead;
            HasLead = true;
        }

        /// <summary>가장 가까운, 지금 보이는 상대. 없으면 null.</summary>
        public Transform NearestVisible()
        {
            Transform best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < m_Visible.Count; i++)
            {
                float sqr = (m_Visible[i].position - m_Self.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = m_Visible[i];
            }
            return best;
        }

        // 카메라와 같은 각도 + 실제 레이캐스트. 봇이 못 보는 건 사람도 못 보는 것이어야 한다.
        private bool CanSee(Transform _other)
        {
            if (!BotSenseLogic.IsInView(m_Self.position, m_Self.forward, _other.position, m_FovDeg)) return false;
            return !SightLine.IsBlocked(m_Self, _other);
        }

        // 지금 나고 있는 발소리의 반경. 안 움직이거나 멈췄으면 0 — 잠행이면 좁은 값이 그대로 온다.
        private static float ActiveFootstepRange(Transform _other)
        {
            foreach (AudioSource source in _other.GetComponentsInChildren<AudioSource>())
            {
                if (!source.loop || !source.isPlaying) continue;
                return source.maxDistance;
            }
            return 0f;
        }

        private Vector3 Blur(Vector3 _heard)
        {
            // 각도는 자리에서 결정적으로 뽑는다 — 매 틱 흔들리면 봇이 제자리에서 떤다.
            float angle = Mathf.Repeat(_heard.x * 12.9898f + _heard.z * 78.233f, Mathf.PI * 2f);
            return BotSenseLogic.Blur(_heard, m_Self.position, HEAR_ERROR_PER_METER, angle);
        }
    }
}
#endif
