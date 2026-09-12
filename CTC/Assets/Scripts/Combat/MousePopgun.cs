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

        // 총알 한 발마다 GetComponent를 돌리지 않는다 — 명중 처리에서 테크 레벨을 읽는다(리뷰 4).
        private TechSlots m_Slots;

        public int Tier => m_Tier.Value;

        private void Awake()
        {
            m_Slots = GetComponent<TechSlots>();
        }

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

        /// <summary>
        /// 봇 전용 — 지금 무기의 사거리(m). 티어를 타므로 총을 사면 늘어난다.
        /// 봇이 "안 맞을 거리에선 안 쏜다"를 판단하려면 사람이 보는 것과 같은 값을 봐야 한다.
        /// </summary>
        public float BotRange => ShopLogic.WeaponStatsForTier(m_Tier.Value).Range;

        /// <summary>
        /// 봇 전용 — 지금 쏠 수 있나. 서버가 <see cref="FireRpc"/>에서 같은 판정을 다시 하지만,
        /// 봇이 모르면 판단 틱마다(0.5초) 거절당하는 죽은 RPC를 계속 보낸다.
        /// 봇은 서버에서 도므로 서버 시계를 그대로 읽는다.
        /// </summary>
        public bool BotCanFire()
        {
            if (NetworkManager == null) return false;

            WeaponStats stats = ShopLogic.WeaponStatsForTier(m_Tier.Value);
            return CombatLogic.IsCooldownReady(m_LastUseTime, NetworkManager.ServerTime.Time, stats.CooldownSeconds);
        }

        /// <summary>
        /// 봇 전용 — 이 방향으로 쏜다. **사람과 같은 입구**(<see cref="FireRpc"/>)를 탄다 —
        /// 사거리·쿨다운·명중 판정·후추 도트·발사음이 전부 사람 총알과 같은 코드를 지나야
        /// 잰 값이 뜻을 갖는다. 겨누는 방향(조준 오차 포함)은 두뇌가 정한다.
        /// </summary>
        public void BotFire(Vector3 _direction) => FireRpc(_direction);

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

        // 형제 무기(CatPounce.PounceRpc)와 같은 규칙 — **남의 몸으로 대신 쏘지 못한다**.
        // 봇은 서버가 곧 소유자라 그대로 통과한다(2026-09-09 감사 P0-8).
        [Rpc(SendTo.Server, RequireOwnership = true)]
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
                // 후추 보유의 단일 원본은 **테크 레벨**이다. PepperRounds.Owned와 둘로 나누면
                // 어긋났을 때 0중첩·0초짜리 도트가 조용히 붙는다 — 컴포넌트는 HUD 스트림용으로만 남긴다.
                bool livingEnemy = tag.Role == RoleId.Cat || (tag.Role == RoleId.None && !hitIsMapLoot);
                int pepperLv = m_Slots != null ? m_Slots.LevelOf(TechId.Pepper) : 0;
                if (pepperLv > 0 && livingEnemy)
                {
                    // 후추 레벨이 중첩·지속을 정한다(TechInfo P1·P2). Lv1 = 3중첩·4초(옛 BalanceInfo 값과 같다)
                    PepperDot.ServerApply(target,
                        Mathf.RoundToInt(TechLogic.ParamOf(TechId.Pepper, pepperLv, 1)),
                        TechLogic.ParamOf(TechId.Pepper, pepperLv, 2));
                }
                break;
            }

            TracerRpc(origin, end);
        }

        // 발사 모션 지속 — 이동 애니메이션 잠금 시간.
        // 이름을 바꿨다(2026-09-12): 이제 트는 것은 Attack이 아니라 Shoot/ShootDraw다.
        // 두 클립 다 10프레임 @24fps = 0.417초라 그 길이에 맞춘다 — 짧게 잡으면 뽑는
        // 동작 중간에 이동 상태가 끼어들어 총이 손에서 튄다.
        private const float SHOOT_ANIM_SECONDS = 0.42f;

        // 발사 피드백 — 전 클라이언트에 짧은 궤적 선 + 발사 모션.
        // 쿨다운 통과한 실제 발사에서만 서버가 보내므로 별도 게이트 불필요.
        [Rpc(SendTo.Everyone)]
        private void TracerRpc(Vector3 _start, Vector3 _end)
        {
            // 발사음 — 티어별 무기 소리(3D, 발사 위치). m_Tier는 NetworkVariable이라 전 클라 일치
            Audio.SfxId fireSfx = m_Tier.Value >= 2 ? Audio.SfxId.MachinegunFire
                : m_Tier.Value == 1 ? Audio.SfxId.SlingshotFire : Audio.SfxId.PopgunFire;
            Audio.SfxPlayer.PlayAt(fireSfx, _start);

            // 총을 손에 띄우고(없었으면 이제 뽑는다) **직전 상태**를 돌려받는다.
            // 이미 들고 있었으면 반동만, 아니면 뽑기부터 — 기관총 테크가 5배 빨라서
            // 매 발에 뽑기를 넣으면 0.1초마다 총을 다시 뽑는 그림이 된다.
            // 이 RPC는 SendTo.Everyone이라 **남의 쥐가 쏘는 것도 똑같이 보인다** —
            // 소유자 검사를 여기 넣으면 안 된다.
            bool alreadyOut = false;
            var gun = GetComponent<Player.MouseGunVisual>();
            if (gun != null) alreadyOut = gun.ShowForShot();

            Player.PlayerAnimationDriver driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null)
            {
                driver.PlayOneShot(Player.MouseGunVisualLogic.ClipForShot(alreadyOut), SHOOT_ANIM_SECONDS);
            }

            // 제 총소리에 눈을 질끈 감는다 — 제 몸만 한 총을 든 짐승의 움찔(2026-09-12).
            // **클립과 같은 `alreadyOut`을 넘긴다**: 클립마다 터지는 프레임이 달라(Shoot 5 · ShootDraw 7)
            // 감는 시각도 같이 갈려야 한다. 하나만 바꾸면 첫 발에서 겨누는 내내 눈이 감긴다.
            var eyes = GetComponent<Player.EyeLook>();
            if (eyes != null) eyes.BlinkForShot(alreadyOut);

            var go = new GameObject("PopgunTracer");
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, _start);
            line.SetPosition(1, _end);
            line.startWidth = 0.05f;
            line.endWidth = 0.02f;
            // **공용 머티리얼 한 장**을 돌려 쓴다. 예전엔 총알마다 `new Material(...)`을 만들어
            // 넘겼는데, 내가 만들어 넣은 머티리얼은 렌더러가 회수해 주지 않아 `Destroy(go, 0.12f)`
            // 뒤에도 그대로 샜다(2026-09-09 감사 B). 색은 LineRenderer의 start/endColor라
            // 머티리얼을 공유해도 총알마다 따로 칠해진다.
            Material tracer = TracerMaterial();
            if (tracer != null) line.sharedMaterial = tracer;
            line.startColor = Color.yellow;
            line.endColor = new Color(1f, 0.9f, 0.2f, 0.2f);
            Destroy(go, 0.12f);
        }

        // 궤적선 공용 머티리얼 — 셰이더를 못 찾으면 **만들지 않는다**(CLAUDE.md 함정 #6:
        // `new Material(null)`은 예외도 경고도 없이 마젠타를 굽는다). 선이 안 보이는 편이 낫다.
        private static Material s_TracerMaterial;

        private static Material TracerMaterial()
        {
            if (s_TracerMaterial != null) return s_TracerMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                GameDebug.LogWarning("[MousePopgun] Sprites/Default 셰이더를 못 찾았다 — 궤적선을 그리지 않는다");
                return null;
            }
            s_TracerMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return s_TracerMaterial;
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
