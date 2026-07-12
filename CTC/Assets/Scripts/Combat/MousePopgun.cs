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

        [SerializeField] private float m_Range = 8f;
        [SerializeField] private int m_Damage = 5;
        [SerializeField] private float m_CooldownSeconds = 0.8f;

        private double m_LastUseTime = -999.0;

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
            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, m_CooldownSeconds)) return;
            m_LastUseTime = now;

            // 원점을 전진시키면 밀착한 고양이 콜라이더 '내부'에서 시작해 빗나간다(리뷰 I-1).
            // 자기 위치에서 RaycastAll 후 자기 자신만 스킵하고 최근접 유효 히트를 취한다.
            Vector3 origin = transform.position + Vector3.up * MUZZLE_HEIGHT;
            RaycastHit[] hits = Physics.RaycastAll(origin, _direction, m_Range);
            System.Array.Sort(hits, (_a, _b) => _a.distance.CompareTo(_b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.root == transform.root) continue; // 자기 몸 스킵

                Health target = hit.collider.GetComponentInParent<Health>();
                if (target == null)
                {
                    return; // 벽/가구가 먼저 막음
                }
                if (target.IsDead) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat) return; // 쥐 등 비대상이 막음

                target.TakeDamage(m_Damage);
                return;
            }
        }
    }
}
