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
            if (Player.ActionLock.Locked(this)) return; // 깡패 변신 중에는 못 친다
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
        public float BotRange => SwordActive ? Info.InfoManager.Instance.Balance.HeroSwordRange
            : ClubActive ? Info.InfoManager.Instance.Balance.GangClubRange
            : ShopLogic.WeaponStatsForTier(m_Tier.Value).Range;

        private Match.MouseJob m_Job;
        private Match.PhaseDirector m_Phase;

        /// <summary>
        /// 깡패 몽둥이가 총을 대신하는가(2026-09-14) — 2페이즈 + 깡패 편. 판정은 <see cref="Match.GangLookLogic.Active"/>.
        /// 같은 버튼·같은 Rpc(<see cref="FireRpc"/>)를 타므로 봇도 그대로 몽둥이를 쓴다.
        /// </summary>
        public bool ClubActive
        {
            get
            {
                if (m_Job == null) m_Job = GetComponent<Match.MouseJob>();
                if (m_Phase == null) m_Phase = FindAnyObjectByType<Match.PhaseDirector>();
                return m_Job != null && m_Phase != null && Match.GangLookLogic.Active(m_Job.Value, m_Phase.Phase);
            }
        }

        /// <summary>
        /// 용사 칼이 총을 대신하는가(2026-09-19) — 용사면 단계와 무관하게 참(<see cref="HeroSwordLogic.Active"/>).
        /// 몽둥이와 같은 버튼·같은 Rpc를 탄다. 둘이 겹칠 일은 없다(직업이 하나다).
        /// </summary>
        public bool SwordActive
        {
            get
            {
                if (m_Job == null) m_Job = GetComponent<Match.MouseJob>();
                return m_Job != null && HeroSwordLogic.Active(m_Job.Value, Match.MatchPhase.Hunt);
            }
        }

        /// <summary>
        /// 봇 전용 — 지금 쏠 수 있나. 서버가 <see cref="FireRpc"/>에서 같은 판정을 다시 하지만,
        /// 봇이 모르면 판단 틱마다(0.5초) 거절당하는 죽은 RPC를 계속 보낸다.
        /// 봇은 서버에서 도므로 서버 시계를 그대로 읽는다.
        /// </summary>
        public bool BotCanFire()
        {
            if (NetworkManager == null) return false;
            if (Player.ActionLock.Locked(this)) return false;

            var balance = Info.InfoManager.Instance.Balance;
            float cooldown = SwordActive ? balance.HeroSwordCooldown
                : ClubActive ? balance.GangClubCooldown
                : ShopLogic.WeaponStatsForTier(m_Tier.Value).CooldownSeconds;
            return CombatLogic.IsCooldownReady(m_LastUseTime, NetworkManager.ServerTime.Time, cooldown);
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
            // 변신 중(서버가 보는 값) — 날아온 입력은 버린다. 이미 처리된 발은 그대로 성공이다.
            if (Player.ActionLock.Locked(this)) return;

            if (SwordActive)
            {
                ServerSword(_direction);
                return;
            }

            if (ClubActive)
            {
                ServerClub(_direction);
                return;
            }

            WeaponStats stats = ShopLogic.WeaponStatsForTier(m_Tier.Value);

            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, stats.CooldownSeconds)) return;
            m_LastUseTime = now;

            // 원점을 전진시키면 밀착한 고양이 콜라이더 '내부'에서 시작해 빗나간다(리뷰 I-1).
            // 자기 위치에서 RaycastAll 후 자기 자신만 스킵하고 최근접 유효 히트를 취한다.
            Vector3 origin = transform.position + Vector3.up * MUZZLE_HEIGHT;
            Vector3 end = origin + _direction * stats.Range;
            // **트리거는 무시한다**(SightLine과 같은 규칙). 맵 옷(MapDresser)의 팩 가구가 트리거 콜라이더를 달고 오는데,
            // 그게 먼저 걸리면 「벽이 막았다」로 끝나 바로 앞 쥐가 안 맞았다(2026-09-14 qa_v41 쥐끼리 항목 — 자리에 따라 났다 안 났다).
            RaycastHit[] hits = Physics.RaycastAll(origin, _direction, stats.Range, ~0, QueryTriggerInteraction.Ignore);
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

        // ===== 용사 칼(2026-09-19) =====


        /// <summary>
        /// 몸 앞 부채꼴 안의 표적을 한 번씩 **베고 굳힌다**. 피해는 약하다(주목적은 스턴 — 사용자 결정).
        /// 몽둥이와 같은 규칙이되, 편은 「같은 용사만 못 벤다」다 — 2단계의 깡패도 벤다.
        /// </summary>
        private void ServerSword(Vector3 _direction)
        {
            var balance = Info.InfoManager.Instance.Balance;
            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, balance.HeroSwordCooldown)) return;
            m_LastUseTime = now;

            Vector3 forward = _direction.sqrMagnitude > 1e-4f ? _direction : transform.forward;
            Match.MouseJobId myJob = m_Job != null ? m_Job.Value : Match.MouseJobId.None;
            int damage = Mathf.RoundToInt(balance.HeroSwordDamage * HeroMultiplier());
            int hits = 0;

            foreach (Collider col in Physics.OverlapSphere(transform.position, balance.HeroSwordRange + 0.5f))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.IsDead || target.transform.root == transform.root) continue;
                if (!Match.GangLookLogic.InArc(transform.position, forward, target.transform.position,
                        balance.HeroSwordRange, balance.HeroSwordArcDegrees)) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                bool isPlayer = target.NetworkObject != null && target.NetworkObject.IsPlayerObject;
                bool isMapLoot = target.GetComponent<Map.Alcove>() != null || target.GetComponent<Map.LootBox>() != null;
                if (tag == null || !CombatLogic.CanAttack(RoleId.Mouse, tag.Role, isPlayer, isMapLoot)) continue;

                var targetJob = target.GetComponent<Match.MouseJob>();
                if (targetJob != null && !HeroSwordLogic.CanCut(myJob, targetJob.Value)) continue;

                if (s_ClubHitThisSwing.Contains(target)) continue;   // 콜라이더가 여럿인 몸은 한 번만
                s_ClubHitThisSwing.Add(target);

                target.TakeDamage(damage);
                Stun stun = target.GetComponent<Stun>();
                if (stun != null) stun.ServerApply(balance.StunSeconds);
                hits++;
            }
            s_ClubHitThisSwing.Clear();

            SwordSwingRpc(transform.position + forward * 1.0f, hits > 0);
        }

        [Rpc(SendTo.Everyone)]
        private void SwordSwingRpc(Vector3 _front, bool _hit)
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.PounceSwing, transform.position);   // 근접 공격 소리 — 덮치기·몽둥이와 같은 뜻

            Player.PlayerAnimationDriver driver = GetComponent<Player.PlayerAnimationDriver>();
            // 원샷 길이 = 클립 길이 — 칼 소품 시간표(HeroLook)가 이 원샷이 걸리는 순간 같은 박자로 돈다
            if (driver != null) driver.PlayOneShot(Player.AnimationStateLogic.SwordSwing, Player.HeroSwordVisualLogic.SWING_SECONDS);

            if (_hit) Fx.FxPlayer.PlayAt(Fx.FxId.ClubSmash, _front, transform.rotation, 0.8f);
        }

        // ===== 깡패 몽둥이(2026-09-14) =====

        private const float CLUB_ANIM_SECONDS = 0.5f;   // ClubSwing 12프레임(0.46초) — 짧게 잡으면 휘두르다 걷기로 튄다

        // 몸 앞 부채꼴 안의 **모든** 표적을 한 번씩 친다(CatPounce와 같은 규칙). 깡패 편끼리는 안 친다.
        private void ServerClub(Vector3 _direction)
        {
            var balance = Info.InfoManager.Instance.Balance;
            double now = NetworkManager.ServerTime.Time;
            if (!CombatLogic.IsCooldownReady(m_LastUseTime, now, balance.GangClubCooldown)) return;
            m_LastUseTime = now;

            Vector3 forward = _direction.sqrMagnitude > 1e-4f ? _direction : transform.forward;
            Match.MouseJobId myJob = m_Job != null ? m_Job.Value : Match.MouseJobId.None;
            int hits = 0;

            foreach (Collider col in Physics.OverlapSphere(transform.position, balance.GangClubRange + 0.5f))
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target.IsDead || target.transform.root == transform.root) continue;
                if (!Match.GangLookLogic.InArc(transform.position, forward, target.transform.position,
                        balance.GangClubRange, balance.GangClubArcDegrees)) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                bool isPlayer = target.NetworkObject != null && target.NetworkObject.IsPlayerObject;
                bool isMapLoot = target.GetComponent<Map.Alcove>() != null || target.GetComponent<Map.LootBox>() != null;
                if (tag == null || !CombatLogic.CanAttack(RoleId.Mouse, tag.Role, isPlayer, isMapLoot)) continue;

                var targetJob = target.GetComponent<Match.MouseJob>();
                if (targetJob != null && !Match.GangLookLogic.CanClub(myJob, targetJob.Value)) continue;

                // 한 몸에 콜라이더가 여럿이면 두 번 맞는다 — 이번 휘두르기에서 이미 친 몸은 건너뛴다.
                if (s_ClubHitThisSwing.Contains(target)) continue;
                s_ClubHitThisSwing.Add(target);

                target.TakeDamage(balance.GangClubDamage);
                hits++;
            }
            s_ClubHitThisSwing.Clear();

            ClubSwingRpc(transform.position + forward * 1.0f, hits > 0);
        }

        private static readonly System.Collections.Generic.HashSet<Health> s_ClubHitThisSwing =
            new System.Collections.Generic.HashSet<Health>();

        [Rpc(SendTo.Everyone)]
        private void ClubSwingRpc(Vector3 _front, bool _hit)
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.PounceSwing, transform.position);   // 휘두르는 소리 — 덮치기와 같은 뜻(근접 공격)

            Player.PlayerAnimationDriver driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null) driver.PlayOneShot(Player.AnimationStateLogic.ClubSwing, CLUB_ANIM_SECONDS);

            // 내려치는 순간(0.25초) 앞에 먼지·별 — 맞았든 아니든 "쾅"은 보인다. 맞았으면 별이 하나 더 크다.
            ClubImpactAsync(_front, _hit).Forget();
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid ClubImpactAsync(Vector3 _front, bool _hit)
        {
            await Cysharp.Threading.Tasks.UniTask.Delay(250, cancellationToken: destroyCancellationToken)
                .SuppressCancellationThrow();
            if (this == null) return;
            Fx.FxPlayer.PlayAt(Fx.FxId.ClubSmash, _front, transform.rotation, _hit ? 1.3f : 1f);
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

            // 총구 섬광·탄착(2026-09-14 기술 이펙트) — 궤적선과 같은 자리, 같은 순간.
            Vector3 dir = _end - _start;
            Quaternion aim = dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir) : transform.rotation;
            Fx.FxPlayer.PlayAt(Fx.FxId.MuzzleFlash, _start, aim);
            Fx.FxPlayer.PlayAt(Fx.FxId.BulletImpact, _end, aim);

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
