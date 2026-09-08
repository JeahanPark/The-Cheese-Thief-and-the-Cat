#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Ai.Bt;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 봇의 판단을 **트리로 적는다** (2026-08-12).
    ///
    /// 기술소개서(리그오브레전드 6p)의 챔피언 트리 구조를 그대로 가져왔다 —
    /// **개인행동 → 지휘관명령 → 배회** 순이다. 눈앞의 일은 스스로 처리하고,
    /// 그게 없을 때만 명령을 따르고, 명령도 없으면 돌아다닌다.
    ///
    /// **위아래 순서가 곧 우선순위다.** 그전에는 `Decide()`가 if 순서로 같은 일을 했는데,
    /// 규칙이 함수 안에 숨어 있어 "왜 지금 이걸 하나"를 읽으려면 코드를 따라가야 했다.
    ///
    /// 잎은 씬을 모른다 — `BotAgent`가 공개한 손잡이만 부른다.
    /// </summary>
    public static class BotBrainTrees
    {
        /// <summary>
        /// 고양이.
        /// <code>
        /// Selector
        /// ├ 개인행동   [보인다][사거리] → 덮치기
        /// │            [보인다]         → 쫓기
        /// │            [단서]           → 조사
        /// ├ 지휘관명령 [명령]           → 그 자리로
        /// └ 배회
        /// </code>
        /// </summary>
        public static BtNode BuildCat(BotAgent _self)
        {
            return new BtSelector(
                // ── 개인행동 ──
                new BtSequence(
                    new BtCondition(() => InPounceRange(_self)),
                    BtAction.Once(() =>
                    {
                        _self.Activity = "Pounce";
                        Transform prey = _self.VisiblePrey();
                        _self.SetSneak(false);   // 느린 채로 덮치면 사람이 안 하는 짓이다
                        _self.BuyForCat();
                        _self.UseSkill();
                        // 덮치기는 앞을 봐야 맞는다 — 표적 쪽으로 몸을 밀어 방향을 맞춘 뒤 친다
                        if (prey != null) _self.Nav.SetGoal(prey.position);
                        _self.DoPounce();
                    })),

                // ── 포위 유지(2026-08-24) — 벽을 부수는 중에 쥐가 밖에 보여도, 추격이
                //    진 경주면 벽을 계속 판다. 목표는 쥐가 아니라 **쥐의 은신처(요새)**다 —
                //    쥐를 향하면 열린 우회로로 빙 돌아 여태 판 벽을 버리게 된다.
                new BtSequence(
                    new BtCondition(() => _self.ShouldKeepSieging()),
                    BtAction.Once(() =>
                    {
                        _self.Activity = "Siege";
                        Transform prey = _self.VisiblePrey();
                        _self.SetSneak(false);
                        _self.BuyForCat();
                        if (prey != null && _self.TryGetPreyRefuge(prey.position, out Vector3 fort, out _))
                        {
                            _self.Reserve(fort);
                            _self.Nav.SetGoal(fort);
                        }
                        _self.TryBreakBlocking();
                    })),

                new BtSequence(
                    new BtCondition(() => _self.VisiblePrey() != null),
                    BtAction.Once(() =>
                    {
                        _self.Activity = "Chase";
                        Transform prey = _self.VisiblePrey();
                        _self.SetSneak(false);
                        _self.BuyForCat();
                        _self.UseSkill();
                        if (prey != null)
                        {
                            _self.Reserve(prey.position);
                            _self.Nav.SetGoal(prey.position);
                            // 요새 안으로 숨었다 — 길이 벽을 관통해 나왔으면 그 벽을 부순다
                            _self.TryBreakBlocking();
                        }
                    })),

                new BtSequence(
                    new BtCondition(() => _self.HasLead),
                    BtAction.Once(() =>
                    {
                        _self.Activity = "Investigate";
                        _self.SetSneak(true);   // 소리를 쫓는 동안은 조용히 — 발소리로 위치를 알리지 않는다
                        _self.BuyForCat();
                        _self.Reserve(_self.Lead);
                        _self.Nav.SetGoal(_self.Lead);
                        _self.TryBreakBlocking();   // 소리 난 자리가 요새 안이면 벽부터 연다
                        // 도착했는데 아무도 없으면 그 단서를 버린다
                        if (_self.DistanceTo(_self.Lead) < BotAgent.LEAD_ARRIVE) _self.DropLead();
                    })),

                // ── 지휘관명령 ──
                new BtSequence(
                    new BtCondition(() => _self.Order.HasOrder),
                    BtAction.Once(() =>
                    {
                        BotOrder order = _self.Order;
                        _self.Activity = order.Id.ToString();
                        _self.SetSneak(false);
                        _self.BuyForCat();
                        _self.Reserve(order.Point);
                        _self.Nav.SetGoal(order.Point);
                    })),

                // ── 배회 ──
                BtAction.Once(() =>
                {
                    _self.Activity = "Patrol";
                    _self.SetSneak(false);
                    _self.BuyForCat();
                    // 서 있으면 판에서 사라진 것과 같다 — 소리로 찾는 시간이 측정되지 않는다
                    if (!_self.Nav.IsMoving) _self.Nav.SetWanderGoal(_self.Rng);
                }));
        }

        /// <summary>
        /// 쥐. 지휘관이 없다 — 여덟 마리가 각자 논다(예약 시스템이 뭉치는 것만 막는다).
        /// <code>
        /// Selector
        /// ├ [위협][쏠 만함] → 물러나며 쏜다
        /// ├ [위협]          → 잠행·도망
        /// ├ [살 것·상점]    → 쇼핑
        /// ├ [쓸 지점]       → 이동 / 파밍
        /// └ 배회
        /// </code>
        /// </summary>
        public static BtNode BuildMouse(BotAgent _self)
        {
            return new BtSelector(
                // (구출 가지는 감옥과 함께 2026-08-17에 뺐다 — 잡히면 즉시 탈락이다.)

                // ── 반격(2026-09-09, 승률 진단 §9-3의 4번) ── **도망 가지 바로 앞**이다.
                //
                // 2026-08-12에 「맞서 쏘지 않는다」로 뺐었다 — 여덟이 쉬지 않고 쏘니 97초에
                // 고양이 둘이 죽어 계측이 안 됐기 때문이다. 그런데 그러면 **「고양이 전멸 = 쥐 승」**
                // 이라는 승리 경로가 계측에서 통째로 빠진다. 그래서 뺀 게 아니라 **사람 수준으로
                // 되돌린다** — 사거리·조준 오차·「들켰을 때만」이 그 몫이고, 규칙은 전부
                // <see cref="MouseCombatLogic"/>에 있다(EditMode로 잰다).
                //
                // **도망을 대체하지 않는다.** 같은 몸짓(<see cref="Retreat"/>)으로 물러나면서 쏠 뿐이라
                // 이 가지가 걸려도 봇은 여전히 달아난다 — 이 게임은 카메라 기준 이동이라
                // 사람도 전속력으로 물러나며 뒤로 쏜다. `Activity`도 안 바꾼다: 사망 표
                // (`Flee`/`Shelter`)를 반격 전후로 비교해야 하니까.
                new BtSequence(
                    new BtCondition(() => _self.CanShootNow()),
                    BtAction.Once(() =>
                    {
                        if (_self.TryGetShootTarget(out Vector3 cat)) Retreat(_self, cat);
                        _self.Shoot();
                        _self.BuyForMouse();
                        _self.RecruitIfGang();
                    })),

                // **위협을 만나면 도망이다** — 캐다 잡히면 그 판의 성장이 통째로 날아간다.
                new BtSequence(
                    new BtCondition(() => ThreatPosition(_self, out _)),
                    BtAction.Once(() =>
                    {
                        ThreatPosition(_self, out Vector3 threat);
                        Retreat(_self, threat);
                        _self.BuyForMouse();
                        _self.RecruitIfGang();
                    })),

                // ── 쇼핑(계획 3 Task 6) ── **위협 다음, 파밍 앞**이다.
                // 순서가 곳 우선순위다: 도망 > 쇼핑 > 파밍 > 배회. 파밍 뒤에 두면
                // 치즈가 마를 때까지 영영 안 사서 상점이 있나 마나가 된다.
                new BtSequence(
                    new BtCondition(() => _self.TryGetShop(out _)),
                    BtAction.Once(() =>
                    {
                        _self.TryGetShop(out Vector3 shop);
                        _self.Activity = "Shop";
                        _self.SetSneak(false);

                        // 도착 판정은 **원반 안인가**다(ShopSpotLogic.Contains) — 거리로 재면
                        // 반경 밖에 서서 영원히 구매에 실패한다. 사는 것 자체는 BuyForMouse가
                        // 하고, 서버가 상점 안인지 다시 보므로 도착 전 호출은 그냥 거절된다.
                        bool inside = _self.IsInsideShop();
                        _self.MarkShopping(inside, BotAgent.DECIDE_INTERVAL);
                        if (inside) _self.Nav.Stop();
                        else _self.Nav.SetGoal(shop);

                        _self.BuyForMouse();
                        _self.RecruitIfGang();
                    })),

                new BtSequence(
                    new BtCondition(() => _self.TryGetSpot(out _)),
                    BtAction.Once(() =>
                    {
                        _self.TryGetSpot(out Vector3 spot);
                        _self.SetSneak(false);
                        _self.Reserve(spot);

                        if (_self.DistanceTo(spot) <= MouseBotLogic.ARRIVE_DISTANCE)
                        {
                            _self.Activity = "Farm";
                            _self.Nav.Stop();
                        }
                        else
                        {
                            _self.Activity = "Travel";
                            _self.Nav.SetGoal(spot);
                        }

                        _self.BuyForMouse();
                        _self.RecruitIfGang();
                    })),

                // 쓸 자리가 하나도 없다(전부 마르거나 남이 쓴다) — 돌아다니며 빈자리를 찾는다
                BtAction.Once(() =>
                {
                    _self.Activity = "Wander";
                    _self.SetSneak(false);
                    if (!_self.Nav.IsMoving) _self.Nav.SetWanderGoal(_self.Rng);
                    _self.BuyForMouse();
                    _self.RecruitIfGang();
                }));
        }

        /// <summary>
        /// 위협에서 물러난다. **반격 가지와 도망 가지가 같은 몸짓을 쓴다** — 둘로 나눠 적으면
        /// 한쪽만 고쳐져 봇이 「쏘면서 도망칠 때」와 「그냥 도망칠 때」 다르게 움직인다.
        /// </summary>
        private static void Retreat(BotAgent _self, Vector3 _threat)
        {
            // **노출되면 달리고, 시야를 끊으면 조용해진다.**
            //
            // 기준이 「내 눈에 보이나」면 안 된다(2026-09-09 감사 A1) — 봇의 몸은 걷는 방향으로
            // 돌고 카메라가 없어서, 등을 돌리는 **다음 틱에 스스로 잠행을 켰다**(6.6 → 2.97 m/s).
            // 고양이가 7.4이니 실효 추격비가 1.12배가 아니라 **2.49배**였다.
            //
            // 그렇다고 「한 번 봤으면 무조건 질주」도 틀렸다(실측: 그것만 고쳤더니 승률 0%) —
            // 쥐는 고양이보다 느려서(6.6 < 7.4) **달려서는 절대 못 벗어난다.** 살아남는 길은
            // 기둥·선반 뒤로 **시야를 끊고 조용해지는 것**뿐이다(잠행 발소리 22 → 6 m).
            //
            // 그래서 기준은 **서로 시선이 통하는가**다. 통하는 동안은 숨어 봐야 소용없으니 달리고,
            // 끊기면 몇 초 뒤 잠행으로 자취를 지운다. 사람이 하는 짓 그대로다.
            bool exposed = _self.TryGetShootTarget(out _);
            _self.MarkChased(exposed);
            bool chased = _self.IsBeingChased;
            _self.SetSneak(!chased);

            // **요새가 이길 수 있는 거리에 있으면 그리로**(원작 벙커 플레이) —
            // 좌우 통벽·쥐 전용 문이라 고양이는 **벽을 부수는 동안은** 못 들어온다.
            // 2026-08-18부터 고양이가 벽을 뚫을 수 있으므로 요새는 '안전지대'가 아니라
            // **버는 시간**이다 — 이미 맞고 있는 요새는 `TryGetRefugeFortress`가 걸러 내고,
            // 그러면 아래 else로 떨어져 반대쪽으로 달아난다.
            // 없으면 위협에게 안 보이는 자리로 꺾는다(차폐벽·기둥 뒤).
            if (_self.TryGetRefugeFortress(_threat, out Vector3 fort))
            {
                bool inside = _self.DistanceTo(fort) <= MouseBotLogic.ARRIVE_DISTANCE;
                _self.Activity = inside ? "Shelter" : "Flee";
                _self.Nav.SetGoal(fort);
            }
            else
            {
                _self.Activity = "Flee";
                _self.Nav.SetGoal(MouseBotLogic.FleeGoal(
                    _self.Position, _threat, BotAgent.FLEE_DISTANCE, BotAgent.IsHiddenFrom));
            }

            // 쫓기면 도주 수단을 쓴다 — 사서 안 쓰면 못 산 것과 같다.
            // 요새는 내 위치 중심에 즉시 서고 고양이를 밀어낸다(2026-08-15) — 3방 동안의 벽.
            // **여기도 `seen`이 아니라 `chased`다**(감사 A1) — 같은 이유로 도망치는 봇이
            // 대시를 영영 못 눌렀다. 사서 안 쓰는 게 아니라 못 쓰고 있었다.
            if (chased)
            {
                _self.DoDash();
                _self.PlaceFortressForFlee();
            }
        }

        // 보이면 사거리 안인가. 안 보이면 false — 소리로만 아는 상대에게 덮치기를 눌러 봐야 헛손질이다.
        private static bool InPounceRange(BotAgent _self)
        {
            Transform prey = _self.VisiblePrey();
            return prey != null && _self.DistanceTo(prey.position) <= _self.PounceRange;
        }

        // 위협의 자리. **보이는 고양이가 우선**, 없으면 **가까운** 소리만 —
        // 먼 소리(덮치기 45m)에도 도망치면 온 맵의 쥐가 상시 도주에 빠진다.
        private static bool ThreatPosition(BotAgent _self, out Vector3 _threat)
        {
            Transform seen = _self.Senses.NearestVisible();
            if (seen != null) { _threat = seen.position; return true; }
            if (_self.HasLead && MouseBotLogic.IsSoundThreat(_self.DistanceTo(_self.Lead)))
            {
                _threat = _self.Lead;
                return true;
            }

            _threat = Vector3.zero;
            return false;
        }
    }
}
#endif
