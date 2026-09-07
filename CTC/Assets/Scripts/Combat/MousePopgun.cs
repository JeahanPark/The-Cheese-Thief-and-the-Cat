using CTC.Runtime.Core;
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

        /// <summary>무기 티어 스트림(우상단 상태 배지용) — 구독 즉시 현재 값이 흐른다.</summary>
        public R3.Observable<int> TierChanged => m_Tier.AsObservableWithCurrent();

        /// <summary>서버 전용(ShopController). 단방향 상승만.</summary>
        public void ServerSetTier(int _tier)
        {
            if (!IsServer || _tier <= m_Tier.Value) return;
            m_Tier.Value = _tier;
        }

        private void Update()
        {
            if (!Player.LocalControl.Mine(this) || Cursor.lockState != CursorLockMode.Locked) return;

            if (Core.UiFocus.BlocksGameplay) return; // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
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
                // 고양이 + 제3세력(강아지) + **다른 쥐**(v3.9). 제 구조물은 안 쏜다.
                RoleTag tag = h.GetComponent<RoleTag>();
                bool isPlayer = h.NetworkObject != null && h.NetworkObject.IsPlayerObject;
                if (tag == null || !CombatLogic.CanAttack(RoleId.Mouse, tag.Role, isPlayer) || h.IsDead) continue;
                if (h.NetworkObject == NetworkObject) continue;   // 자기 자신은 표적이 아니다

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
                bool hitIsPlayer = target.NetworkObject != null && target.NetworkObject.IsPlayerObject;
                // 맵이 깔아 둔 중립 노획물(벽감·상자)은 쥐 총알도 부순다(2026-08-14) —
                // 제 구조물(요새·창고)은 여전히 비대상이다.
                bool hitIsMapLoot = target.GetComponent<Map.Alcove>() != null
                                    || target.GetComponent<Map.LootBox>() != null;
                if (tag == null || !CombatLogic.CanAttack(RoleId.Mouse, tag.Role, hitIsPlayer, hitIsMapLoot))
                    break; // 비대상이 막음

                // 용사쥐(v3.9)는 그냥 세진다 — 화력도 오른다. 직업이 없으면 배율이 1이라
                // 직업을 모르는 씬에서도 그대로 돈다.
                target.TakeDamage(Mathf.RoundToInt(stats.Damage * HeroMultiplier()));

                // 후추 탄(빌드 심화 v1) — 고양이·강아지(RoleTag None인 제3세력)에게만 도트.
                // 구조물·상자·쥐엔 안 붙는다: 벽감 치즈 총량 QA(qa_v38)를 흔들면 안 되고,
                // 쥐끼리는 매울 이유가 없다. 강아지는 hitIsPlayer=false·비노획물로 걸러진다.
                var pepper = GetComponent<PepperRounds>();
                bool livingEnemy = tag.Role == RoleId.Cat || (tag.Role == RoleId.None && !hitIsMapLoot);
                if (pepper != null && pepper.Owned && livingEnemy)
                {
                    // 후추 레벨이 중첩·지속을 정한다(TechInfo P1·P2). Lv1 = 3중첩·4초(옛 BalanceInfo 값과 같다)
                    int pepperLv = TechOwner.LevelOf(gameObject, TechId.Pepper);
                    PepperDot.ServerApply(target,
                        Mathf.RoundToInt(TechLogic.ParamOf(TechId.Pepper, pepperLv, 1)),
                        TechLogic.ParamOf(TechId.Pepper, pepperLv, 2));
                }
                break;
            }

            TracerRpc(origin, end);
        }

        // 발사 모션 지속 — 이동 애니메이션 잠금 시간
        private const float ATTACK_ANIM_SECONDS = 0.4f;

        // 발사 피드백 — 전 클라이언트에 짧은 궤적 선 + 발사 모션.
        // 쿨다운 통과한 실제 발사에서만 서버가 보내므로 별도 게이트 불필요.
        [Rpc(SendTo.Everyone)]
        private void TracerRpc(Vector3 _start, Vector3 _end)
        {
            // 발사음 — 티어별 무기 소리(3D, 발사 위치). m_Tier는 NetworkVariable이라 전 클라 일치
            Audio.SfxId fireSfx = m_Tier.Value >= 2 ? Audio.SfxId.MachinegunFire
                : m_Tier.Value == 1 ? Audio.SfxId.SlingshotFire : Audio.SfxId.PopgunFire;
            Audio.SfxPlayer.PlayAt(fireSfx, _start);

            Player.PlayerAnimationDriver driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null)
            {
                driver.PlayOneShot(Player.AnimationStateLogic.Attack, ATTACK_ANIM_SECONDS);
            }

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

        // 용사쥐 배율. 직업 컴포넌트가 없으면 1이라 직업을 모르는 씬에서도 그대로 돈다.
        private float HeroMultiplier()
        {
            var job = GetComponent<CTC.Runtime.Match.MouseJob>();
            if (job == null || job.Value != CTC.Runtime.Match.MouseJobId.Hero) return 1f;
            return CTC.Runtime.Match.MouseJobLogic.HeroDamageMultiplier;
        }


    }
}
