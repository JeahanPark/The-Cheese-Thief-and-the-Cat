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

        // 서버 전용 — 스폰 순간에 한 번 훑어 둔 고양이 목록.
        // 매 틱 FindObjectsByType을 돌면 장판 8장 × 초당 10틱 = 씬 전수 조사 80회/초에
        // 플레이어마다 GetComponent<RoleTag>까지 붙는다. 명단은 한 번만 만든다.
        // **절충: 판 도중 새로 들어온 고양이는 이미 깔린 장판에 안 걸린다.** 장판 수명이 10초라
        // 그 사이 난입은 무시할 만하고, 다음에 까는 장판부터는 정상이다.
        private readonly List<Player.PlayerMovement> m_Cats = new List<Player.PlayerMovement>();

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
            // 끈끈이 튀김(2026-09-14) — 장판이 서는 순간 한 번. 크기는 반경을 따른다(1 m 기준 프리팹).
            Fx.FxPlayer.PlayAt(Fx.FxId.StickySplat, transform.position, Quaternion.identity, Mathf.Max(0.5f, m_Radius.Value));
            if (IsServer)
            {
                ServerBuildCatRoster();
                RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
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

        // 스폰 순간의 고양이 명단(서버). 쥐는 제가 깐 것에 안 붙으므로 여기서 걸러 둔다.
        private void ServerBuildCatRoster()
        {
            m_Cats.Clear();
            foreach (Player.PlayerMovement body in
                     FindObjectsByType<Player.PlayerMovement>(FindObjectsSortMode.None))
            {
                RoleTag tag = body.GetComponent<RoleTag>();
                if (tag != null && tag.Role == RoleId.Cat) m_Cats.Add(body);
            }
        }

        /// <summary>
        /// 서버 루프 — 지속(BalanceInfo.StickySeconds) 동안 0.1초마다 명단을 훑는다.
        /// 겹친 장판 둘이 한 고양이를 다루면 **더 센 쪽이 이긴다** — 감속은 건 장판마다
        /// 한 칸씩 <see cref="Player.PlayerMovement"/>에 쌓이고, 거기서 최솟값을 고른다.
        /// </summary>
        private async UniTaskVoid RunAsync(CancellationToken _ct)
        {
            float life = Info.InfoManager.Instance.Balance.StickySeconds;
            double endAt = Time.timeAsDouble + life;
            float mul = StickyLogic.SpeedMultiplier(m_SlowRatio);
            ulong sourceId = NetworkObjectId;   // 파괴 뒤엔 못 읽는다 — 풀어 줄 열쇠를 미리 쥔다
            bool cancelled = false;

            try
            {
                while (Time.timeAsDouble < endAt && !_ct.IsCancellationRequested)
                {
                    foreach (Player.PlayerMovement body in m_Cats)
                    {
                        if (body == null) continue;   // 판 도중 despawn된 고양이

                        bool inside = StickyLogic.Inside(
                            transform.position, body.transform.position, m_Radius.Value);
                        if (inside && m_Slowed.Add(body)) body.ServerAddSlowSource(sourceId, mul);
                        else if (!inside && m_Slowed.Remove(body)) body.ServerRemoveSlowSource(sourceId);
                    }
                    await UniTask.Delay(TimeSpan.FromSeconds(TICK_SECONDS), cancellationToken: _ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 판이 끝나거나 씬이 바뀌면 Delay가 예외로 빠져나온다 — 예전엔 그대로 죽어서
                // 밟고 있던 고양이가 영구 감속으로 남았다. despawn만 건너뛴다(이미 사라지는 중).
                cancelled = true;
            }
            finally
            {
                // 어떻게 끝나든 밟고 있던 고양이는 원래 속도로 되돌린다.
                foreach (Player.PlayerMovement body in m_Slowed)
                {
                    if (body != null) body.ServerRemoveSlowSource(sourceId);
                }
                m_Slowed.Clear();
            }

            if (!cancelled && IsSpawned) NetworkObject.Despawn(true);
        }
    }
}
