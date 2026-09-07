using System;
using System.Collections.Generic;
using System.Threading;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 끈끈이 장판(테크 v3 계획 2a). 바닥 쿼드 1장 — 모션·파티클 없이 색이 곧 연출이다.
    /// 서버가 매 틱 반경 안 고양이에게 감속 배율을 걸고, 나가면 1로 되돌린다. 지속이 끝나면 despawn.
    ///
    /// 트리거 콜라이더를 쓰지 않는다 — 얇은 바닥판에 CharacterController가 스치듯 지나가면
    /// Enter/Exit이 잘 안 뜬다. 0.1초마다 거리를 직접 재는 쪽이 눈에 보이는 원과 정확히 같다.
    /// </summary>
    public class StickyPatch : NetworkBehaviour
    {
        private const float TICK_SECONDS = 0.1f;
        private const float DEFAULT_RADIUS = 2.5f;

        /// <summary>바닥 쿼드 자식 이름 — 팩토리와 같은 값을 써야 한다.</summary>
        public const string QUAD_NAME = "Quad";

        private readonly NetworkVariable<float> m_Radius = new NetworkVariable<float>(DEFAULT_RADIUS);

        // 스폰 전에 받아 두는 값 — 스폰 전 NetworkVariable에 직접 쓰면 NGO가
        // "doesn't know its NetworkBehaviour yet" 경고를 장판마다 뱉는다(ThornWall과 같은 이유).
        private float m_PendingRadius = DEFAULT_RADIUS;
        private float m_SlowRatio;   // 서버 전용 — 배율은 서버가 계산해 PlayerMovement에 복제한다

        private readonly HashSet<Player.PlayerMovement> m_Slowed = new HashSet<Player.PlayerMovement>();

        /// <summary>서버 전용 — 스폰 **전에** 부른다(반경은 클라의 쿼드 크기이기도 하다).</summary>
        public void ServerSetup(float _radius, float _slowRatio)
        {
            if (_radius <= 0f)
            {
                Core.GameDebug.LogWarning(
                    $"[StickyPatch] 반경을 TechInfo에서 못 읽었다({_radius}) — 기본 {DEFAULT_RADIUS}m로 깐다.");
                _radius = DEFAULT_RADIUS;
            }
            m_PendingRadius = _radius;
            m_SlowRatio = _slowRatio;
            if (IsSpawned && IsServer) m_Radius.Value = _radius;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) m_Radius.Value = m_PendingRadius;
            m_Radius.OnValueChanged += OnRadiusChanged;
            ApplyRadiusToQuad(m_Radius.Value);
            if (IsServer) RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public override void OnNetworkDespawn()
        {
            m_Radius.OnValueChanged -= OnRadiusChanged;
        }

        private void OnRadiusChanged(float _old, float _new) => ApplyRadiusToQuad(_new);

        private void ApplyRadiusToQuad(float _radius)
        {
            Transform quad = transform.Find(QUAD_NAME);
            if (quad == null) return;
            quad.localScale = new Vector3(_radius * 2f, _radius * 2f, 1f);
        }

        /// <summary>
        /// 서버 루프 — 지속(BalanceInfo.StickySeconds) 동안 0.1초마다 반경 안을 훑는다.
        /// 플레이어 ≤10 · 장판 ≤8이라 <c>FindObjectsByType</c>이 무해하다.
        /// 겹친 장판 둘이 한 고양이를 다루면 **마지막 값이 이긴다** — 더 센 쪽이 이기게 만들려면
        /// 배율을 누가 걸었는지 추적해야 해서, 지금은 겹쳐 깔아도 이득이 없다는 규칙으로 둔다.
        /// </summary>
        private async UniTaskVoid RunAsync(CancellationToken _ct)
        {
            float life = Info.InfoManager.Instance.Balance.StickySeconds;
            double endAt = Time.timeAsDouble + life;
            float mul = StickyLogic.SpeedMultiplier(m_SlowRatio);

            while (Time.timeAsDouble < endAt && !_ct.IsCancellationRequested)
            {
                foreach (Player.PlayerMovement body in
                         FindObjectsByType<Player.PlayerMovement>(FindObjectsSortMode.None))
                {
                    RoleTag tag = body.GetComponent<RoleTag>();
                    if (tag == null || tag.Role != RoleId.Cat) continue;   // 쥐는 제가 깐 것에 안 붙는다

                    bool inside = StickyLogic.Inside(transform.position, body.transform.position, m_Radius.Value);
                    if (inside && m_Slowed.Add(body)) body.ServerSetExternalSpeedMul(mul);
                    else if (!inside && m_Slowed.Remove(body)) body.ServerSetExternalSpeedMul(1f);
                }
                await UniTask.Delay(TimeSpan.FromSeconds(TICK_SECONDS), cancellationToken: _ct);
            }

            // 끝나면 밟고 있던 고양이도 원래 속도로 — 여기서 안 풀면 영구 감속이다.
            foreach (Player.PlayerMovement body in m_Slowed)
            {
                if (body != null) body.ServerSetExternalSpeedMul(1f);
            }
            m_Slowed.Clear();
            if (IsSpawned) NetworkObject.Despawn(true);
        }
    }
}
