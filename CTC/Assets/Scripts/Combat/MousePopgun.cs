using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 쥐 기본 무기 딱총(원작 BB탄 딱총 티어). 소유자 좌클릭 → 카메라 전방으로
    /// ServerRpc → 서버 Raycast → 고양이만 데미지. 상점 티어 확장은 Plan 6.
    /// </summary>
    public class MousePopgun : NetworkBehaviour
    {
        private const float MUZZLE_HEIGHT = 0.3f;

        // 무기 티어(0 딱총/1 새총/2 기관총급). 서버 기록(상점 구매), 전원 읽기(HUD·QA).
        private readonly NetworkVariable<int> m_Tier = new NetworkVariable<int>();

        private double m_LastUseTime = -999.0;

        public int Tier => m_Tier.Value;

        /// <summary>서버 전용(ShopController). 단방향 상승만.</summary>
        public void ServerSetTier(int _tier)
        {
            if (!IsServer || _tier <= m_Tier.Value) return;
            m_Tier.Value = _tier;
        }

        private void Update()
        {
            if (!IsOwner || Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Vector3 dir = Camera.main != null ? Camera.main.transform.forward : transform.forward;
            dir.y = 0f;
            FireRpc(dir.normalized);
        }

        /// <summary>QA: 가장 가까운 고양이를 향해 발사.</summary>
        public void DevShootAtNearestCat()
        {
            Health nearest = null;
            float best = float.MaxValue;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                RoleTag tag = h.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat || h.IsDead) continue;

                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = h; }
            }
            if (nearest == null) return;

            Vector3 dir = nearest.transform.position - transform.position;
            dir.y = 0f;
            FireRpc(dir.normalized);
        }

        [Rpc(SendTo.Server)]
        private void FireRpc(Vector3 _direction)
        {
            WeaponStats stats = ShopLogic.WeaponStatsForTier(m_Tier.Value);

            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, stats.CooldownSeconds)) return;
            m_LastUseTime = now;

            // 원점을 전진시키면 밀착한 고양이 콜라이더 '내부'에서 시작해 빗나간다(리뷰 I-1).
            // 자기 위치에서 RaycastAll 후 자기 자신만 스킵하고 최근접 유효 히트를 취한다.
            Vector3 origin = transform.position + Vector3.up * MUZZLE_HEIGHT;
            Vector3 end = origin + _direction * stats.Range;
            RaycastHit[] hits = Physics.RaycastAll(origin, _direction, stats.Range);
            System.Array.Sort(hits, (_a, _b) => _a.distance.CompareTo(_b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.root == transform.root) continue; // 자기 몸 스킵

                Health target = hit.collider.GetComponentInParent<Health>();
                if (target == null)
                {
                    end = hit.point; // 벽/가구가 먼저 막음
                    break;
                }
                if (target.IsDead) continue;

                end = hit.point;

                RoleTag tag = target.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat) break; // 쥐 등 비대상이 막음

                target.TakeDamage(stats.Damage);
                break;
            }

            TracerRpc(origin, end);
        }

        // 발사 피드백 — 전 클라이언트에 짧은 궤적 선을 그린다.
        [Rpc(SendTo.Everyone)]
        private void TracerRpc(Vector3 _start, Vector3 _end)
        {
            var go = new GameObject("PopgunTracer");
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, _start);
            line.SetPosition(1, _end);
            line.startWidth = 0.05f;
            line.endWidth = 0.02f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.yellow;
            line.endColor = new Color(1f, 0.9f, 0.2f, 0.2f);
            Destroy(go, 0.12f);
        }
    }
}
