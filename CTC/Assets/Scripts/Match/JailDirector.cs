using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 감옥을 돌리는 Worker (v3.8 4번) — 수감 · 체류 카운트다운 · 구출 게이지 · 석방.
    ///
    /// 감옥을 `NetworkObject`로 만들지 않은 이유: 감옥은 맞지도 부서지지도 않는 붙박이다.
    /// 복제할 상태는 "누가 갇혔나"뿐이고 그건 이미 `JailedTag`가 플레이어에 들고 있다.
    /// 생김새는 `MapBuilder`가 레이아웃에서 각자 짓는다(치즈 지점과 같은 취급).
    ///
    /// 판정은 전부 서버다. 구출 게이지도 서버가 세므로 클라가 "다 찼다"고 주장할 수 없다.
    /// </summary>
    public class JailDirector : NetworkBehaviour
    {
        [SerializeField] private LobbyController m_Lobby;

        // 감옥별 구출 진행도(초). 인덱스는 레이아웃 순서.
        private readonly List<float> m_RescueProgress = new List<float>();
        private readonly List<Vector3> m_JailPositions = new List<Vector3>();

        /// <summary>QA·HUD용 — 지금 갇힌 쥐 수.</summary>
        public int JailedCount { get; private set; }

        /// <summary>맵이 바뀌면 감옥 자리도 바뀐다. `MapDirector`가 알린다.</summary>
        public void Rebind(IReadOnlyList<Vector3> _jailPositions)
        {
            m_JailPositions.Clear();
            m_RescueProgress.Clear();
            if (_jailPositions == null) return;

            m_JailPositions.AddRange(_jailPositions);
            for (int i = 0; i < m_JailPositions.Count; i++) m_RescueProgress.Add(0f);
        }

        /// <summary>
        /// 서버 전용 — 잡힌 쥐를 가장 가까운 감옥에 넣는다. 감옥이 없으면 false(호출부가 기존 탈락 처리).
        /// </summary>
        public bool ServerJail(NetworkObject _mouse)
        {
            if (!IsServer || _mouse == null) return false;

            int index = JailLogic.NearestJailIndex(_mouse.transform.position, m_JailPositions);
            if (index < 0) return false;

            var tag = _mouse.GetComponent<JailedTag>();
            var health = _mouse.GetComponent<Health>();
            if (tag == null || health == null) return false;

            // 갇힌 동안 계속 맞아 죽으면 체류 타이머가 무의미해진다 — HP를 채워 넣는다.
            // Heal이 아니라 ServerSetHp인 이유: 이 시점에 HP가 0이라 Heal은 '죽은 대상'으로 보고 튕긴다.
            health.ServerSetHp(health.MaxHp);
            tag.ServerJail(index, JailLogic.HoldSeconds);

            // CharacterController는 transform 대입을 무시하므로 잠깐 끄고 옮긴다(CTC 제약)
            Teleport(_mouse.transform, m_JailPositions[index]);
            GameDebug.Log($"[JailDirector] 수감 client={_mouse.OwnerClientId} → 감옥 {index}");
            return true;
        }

        private void Update()
        {
            if (!IsServer || m_Lobby == null || !m_Lobby.GameStarted) return;

            float dt = Time.deltaTime;
            JailedCount = 0;

            // 감옥마다: 갇힌 쥐를 세고 체류를 깎고, 구조자가 있는지 본다
            for (int jail = 0; jail < m_JailPositions.Count; jail++)
            {
                TickJail(jail, dt);
            }
        }

        private void TickJail(int _jail, float _deltaSeconds)
        {
            var prisoners = new List<JailedTag>();
            bool rescuerInside = false;

            foreach (JailedTag tag in FindObjectsByType<JailedTag>(FindObjectsSortMode.None))
            {
                if (tag.Jailed)
                {
                    if (tag.JailIndex != _jail) continue;
                    prisoners.Add(tag);
                    JailedCount++;
                    continue;
                }

                // 자유로운 쥐만 구조자다 — 고양이가 서 있어도 게이지는 안 찬다
                if (!IsFreeMouse(tag)) continue;
                Vector3 d = tag.transform.position - m_JailPositions[_jail];
                d.y = 0f;
                if (d.magnitude <= JailLogic.RescueRadius) rescuerInside = true;
            }

            if (prisoners.Count == 0)
            {
                m_RescueProgress[_jail] = 0f;
                return;
            }

            // 체류 — 다 되면 그때야 진짜 탈락이다
            for (int i = prisoners.Count - 1; i >= 0; i--)
            {
                float left = JailLogic.AdvanceHold(prisoners[i].RemainingSeconds, _deltaSeconds);
                prisoners[i].ServerSetRemaining(left);
                if (JailLogic.IsHoldExpired(left)) Eliminate(prisoners[i]);
            }

            // 구출 — 성공하면 이 감옥이 통째로 열린다
            m_RescueProgress[_jail] = JailLogic.AdvanceRescue(m_RescueProgress[_jail], _deltaSeconds, rescuerInside);
            if (!JailLogic.IsRescueComplete(m_RescueProgress[_jail], JailLogic.RescueSeconds)) return;

            m_RescueProgress[_jail] = 0f;
            foreach (JailedTag prisoner in prisoners)
            {
                if (prisoner != null && prisoner.Jailed) Release(prisoner);
            }
        }

        private static bool IsFreeMouse(JailedTag _tag)
        {
            RoleTag role = _tag.GetComponent<RoleTag>();
            return role != null && role.Role == RoleId.Mouse
                   && _tag.NetworkObject != null && _tag.NetworkObject.IsPlayerObject;
        }

        private void Release(JailedTag _prisoner)
        {
            var health = _prisoner.GetComponent<Health>();
            if (health != null)
            {
                // 온전히 돌려주면 고양이가 방금 들인 노력이 0이 된다 — 절반만 준다
                int target = JailLogic.ReleaseHp(health.MaxHp, JailLogic.ReleaseHpRatio);
                health.ServerSetHp(target);
            }

            int jail = _prisoner.JailIndex;
            _prisoner.ServerRelease();
            if (jail >= 0 && jail < m_JailPositions.Count)
            {
                // 감옥 안에 그대로 두면 다음 수감자와 겹친다 — 한 발 앞으로 내보낸다
                Teleport(_prisoner.transform, m_JailPositions[jail] + Vector3.forward * 1.5f);
            }
            GameDebug.Log($"[JailDirector] 석방 client={_prisoner.OwnerClientId}");
        }

        private void Eliminate(JailedTag _prisoner)
        {
            GameDebug.Log($"[JailDirector] 체류 만료 — 영구 탈락 client={_prisoner.OwnerClientId}");
            _prisoner.ServerRelease();   // 세는 쪽이 헷갈리지 않게 먼저 내린다
            _prisoner.NetworkObject.Despawn(true);
        }

        // CharacterController가 붙어 있으면 transform 대입이 무시된다(CTC 제약) — 잠깐 끈다
        private static void Teleport(Transform _target, Vector3 _position)
        {
            var cc = _target.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _target.position = _position;
            if (cc != null) cc.enabled = true;
        }
    }
}
