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

            Vector3 origin = transform.position + Vector3.up * MUZZLE_HEIGHT;
            // 자기 CC에 막히지 않도록 반경 밖에서 시작
            origin += _direction * 0.4f;

            if (!Physics.Raycast(origin, _direction, out RaycastHit hit, m_Range)) return;

            Health target = hit.collider.GetComponentInParent<Health>();
            if (target == null || target.IsDead) return;

            RoleTag tag = target.GetComponent<RoleTag>();
            if (tag == null || tag.Role != RoleId.Cat) return;

            target.TakeDamage(m_Damage);
        }
    }
}
