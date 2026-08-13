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
        /// ├ [위협]     → 잠행·도망·쏘기
        /// ├ [동료갇힘] → 구출
        /// ├ [쓸 지점]  → 이동 / 파밍
        /// └ 배회
        /// </code>
        /// </summary>
        public static BtNode BuildMouse(BotAgent _self)
        {
            return new BtSelector(
                // **구출이 도주보다 먼저다 — 단, 고양이가 보이면 양보한다**(2026-08-12).
                // 구출을 위협 아래 두면 간수의 발소리만으로 모든 구출이 포기되고,
                // 잡힌 쥐가 체류 만료로 전부 탈락한다(실측: 구출 0회 · 2게임분 전멸).
                // 소리만 들리는 고양이는 무시하고 간다 — 보이는 순간 아래 도주로 넘어간다.
                new BtSequence(
                    // 예약 가중치 게이트 — **한 감옥에 한 마리만** 간다. 전원이 달려가면
                    // 파밍이 통째로 서고 간수 앞에 먹잇감이 줄을 선다.
                    new BtCondition(() =>
                        !CatTooCloseToRescue(_self)
                        && _self.TryGetJailWithAlly(out Vector3 jail)
                        && _self.ReservationWeight(jail) > 0.5f),
                    BtAction.Once(() =>
                    {
                        _self.Activity = "Rescue";
                        _self.TryGetJailWithAlly(out Vector3 jail);
                        _self.Reserve(jail);
                        // 가까워지면 잠행 — 뛰는 발소리(18m)가 간수를 부른다. 멀리서는 뛴다.
                        _self.SetSneak(MouseBotLogic.ShouldSneakForRescue(_self.DistanceTo(jail)));
                        _self.Nav.SetGoal(jail);   // 반경 안에 버티면 JailDirector가 알아서 푼다
                        _self.BuyForMouse();
                        _self.RecruitIfGang();
                    })),

                // **위협을 만나면 무조건 도망이다** — 캐다 잡히면 그 판의 성장이 통째로 날아간다.
                //
                // **맞서 쏘지 않는다.** 그전에는 도망치며 쐈는데, 여덟 마리가 쉬지 않고 쏘니
                // 97초 만에 고양이 둘이 죽었다(실측). 쥐의 목표는 싸움이 아니라
                // **벌어서 크는 것**이고, 그렇게 노는 봇이라야 잰 승률이 뜻을 갖는다.
                new BtSequence(
                    new BtCondition(() => ThreatPosition(_self, out _)),
                    BtAction.Once(() =>
                    {
                        _self.Activity = "Flee";
                        ThreatPosition(_self, out Vector3 threat);
                        // **보이면 달린다.** 잠행은 0.45배(2.7m/s)라 7.8m/s 고양이에게 무조건
                        // 잡힌다 — 실제로 첫 발견 후 2게임분 안에 8마리가 전멸했다(2026-08-12).
                        // 소리만 들리면 조용히 — 발소리(18m)로 위치를 다시 알려 주지 않는다.
                        bool seen = _self.Senses.NearestVisible() != null;
                        _self.SetSneak(!seen);
                        // 정반대가 아니라 **위협에게 안 보이는 자리**로 — 차폐벽·기둥 뒤가 걸린다
                        _self.Nav.SetGoal(MouseBotLogic.FleeGoal(
                            _self.Position, threat, BotAgent.FLEE_DISTANCE, BotAgent.IsHiddenFrom));
                        // 눈앞에서 쫓기면 도주 수단을 쓴다 — 사서 안 쓰면 못 산 것과 같다.
                        // 미끼는 전방(도망 방향)에 서고 쥐만 통과시킨다 — 추격선 위의 벽.
                        // 시작 치즈 60이 정확히 미끼 1개분인 이유가 이거다.
                        if (seen)
                        {
                            _self.DoDash();
                            _self.PlaceDecoyForFlee();
                        }
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

        // 보이면 사거리 안인가. 안 보이면 false — 소리로만 아는 상대에게 덮치기를 눌러 봐야 헛손질이다.
        private static bool InPounceRange(BotAgent _self)
        {
            Transform prey = _self.VisiblePrey();
            return prey != null && _self.DistanceTo(prey.position) <= _self.PounceRange;
        }

        // 구출을 접는 기준은 "보이는 고양이가 **가까울** 때"만 — 거리 없이 접으면
        // 간수가 감옥에 서 있기만 해도 온 맵의 구출이 죽는다.
        private static bool CatTooCloseToRescue(BotAgent _self)
        {
            Transform cat = _self.Senses.NearestVisible();
            return cat != null && _self.DistanceTo(cat.position) <= MouseBotLogic.RESCUE_ABORT_RADIUS;
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
