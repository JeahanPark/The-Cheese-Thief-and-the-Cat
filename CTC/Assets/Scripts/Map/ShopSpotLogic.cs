using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 자리 고르기 (테크 v3). 방 중심(또는 손 배치 후보) 중 서로 <c>_minGap</c> 이상 떨어진 N개를
    /// 시드로 고른다. **방 크기를 본다** — 상점 원반이 들어가는 방을 먼저 쓴다(<see cref="RoomHoldsShop"/>).
    ///
    /// 크기만으로는 모자랐다 (2026-09-08). 방이 넉넉해도 **그 안에 무엇이 서 있는지**를 안 봤고,
    /// 기본 맵의 상점이 엄폐물을 물고 앉아 요새·창고 둘 다 거절됐다. 그래서 원반 안을 직접 본다
    /// (<see cref="SpotCheck"/> · <see cref="DiscUsable"/>) — 고양이 스폰을 삼키는 자리도 같이 뺀다.
    ///
    /// 시드의 주인은 계획 3부터 <c>MatchRandom</c>이다(판마다 다르다). 맵 시드는 시드가 없을 때의 후퇴다.
    /// </summary>
    public static class ShopSpotLogic
    {
        /// <summary>
        /// 방 벽이 먹는 폭(m) — <c>MapBuilder.ROOM_WALL_THICK</c>(0.5)가 마주 보는 두 벽에 한 번씩.
        /// 상점 원반이 방에 들어가는지 재는 데만 쓴다.
        /// </summary>
        public const float ROOM_WALL_MARGIN = 1.0f;

        /// <summary>간격을 몇 번까지 반으로 줄여 볼까 — 25 → 12.5 → 6.25 → 3.125.</summary>
        public const int GAP_RELAX_STEPS = 3;

        /// <summary>원반 둘레에 찍는 탐침 수 — 중심까지 더해 아홉 자리를 본다.</summary>
        public const int DISC_PROBE_RING = 8;

        /// <summary>막힌 자리를 옮겨 볼 거리(m)와 몇 겹까지 — 1.5 → 3.0 → 4.5 m.</summary>
        public const float NUDGE_STEP_METERS = 1.5f;
        public const int NUDGE_STEPS = 3;

        /// <summary>
        /// 설치물 반치수의 후퇴값(m) — 요새 풋프린트 2.4²의 절반.
        /// 실제 값의 주인은 <c>ShopController.HalfExtentsOf(TechId.Fortress)</c>다.
        /// 부르는 쪽이 <see cref="SpotCheck.PlaceHalfExtent"/>로 넣어 주면 그게 이긴다 —
        /// 이 상수는 규칙만 테스트할 때의 기본값이다.
        /// </summary>
        public const float DEFAULT_PLACE_HALF_EXTENT = 1.2f;

        /// <summary>
        /// 원반이 **쓸 만한가**를 보는 규칙 한 벌 (2026-09-08).
        ///
        /// 왜 있나: <see cref="RoomHoldsShop"/>은 방 **크기만** 봤다. 방이 넓어도 한가운데
        /// 프롭이 서 있으면 원반이 통째로 막히는데 아무도 안 봤고, 기본 맵의 상점 하나가
        /// 실제로 그렇게 앉아 **요새·창고 둘 다 설치가 거절**됐다.
        /// 「좌표가 차단물 안에 박힌다」의 다섯 번째다.
        ///
        /// **굽기 검사(<c>MapPointAudit</c>)로는 못 잡는다** — 상점 자리는 굽는 시점이 아니라
        /// **판마다 시드로** 뽑기 때문이다. 층이 달라서 여기에 있어야 한다.
        ///
        /// 차단물 질의(<see cref="IsFree"/>)는 **밖에서 준다**. 그래야 이 파일이 순수 로직으로
        /// 남아 EditMode가 덮고, 런타임은 격자(<c>NavGridService.Grid</c>)를 준다 —
        /// 격자를 고른 이유는 절차 맵·손 배치 맵을 **한 벌로** 덮는 유일한 원본이라서다
        /// (<c>MapPointAudit</c>은 Props만 알아 절차 맵의 벽·엄폐물을 못 보고,
        /// <c>ShopController.PlacementFits</c>는 물리라 순수 로직으로 못 뽑는다).
        /// </summary>
        public sealed class SpotCheck
        {
            /// <summary>이 점에 설치물이 들어가나. null이면 전부 들어간다고 본다(= 예전 동작).</summary>
            public System.Func<Vector2, bool> IsFree;

            /// <summary>원반이 삼키면 안 되는 점 — 고양이 스폰. null·빈 목록이면 안 본다.</summary>
            public IReadOnlyList<Vector2> KeepAway;

            /// <summary>그 점이 원반 **밖으로** 이만큼 더 떨어져 있어야 한다(m) — 몸 반지름 + 여유.</summary>
            public float KeepAwayMargin;

            /// <summary>가장 큰 설치물의 반치수(m). 0 이하면 <see cref="DEFAULT_PLACE_HALF_EXTENT"/>.</summary>
            public float PlaceHalfExtent = DEFAULT_PLACE_HALF_EXTENT;
        }

        /// <summary>
        /// 상점 원반이 이 방 안에 들어가는가.
        ///
        /// 안 들어가면 원반의 대부분이 **벽 속**이다. 상점은 걸어 들어가는 구역인데
        /// 정작 그 안에서 겨눌 수 있는 자리가 전부 벽이 되어 「상점에서 설치권을 사고
        /// 그 자리에 세운다」가 통째로 막힌다 — 2026-09-08 실측: 기본 맵의 상점이
        /// 4×6 m 방(R2) 한가운데 앉는 바람에 반경 2.7 m 둘레 여덟 자리 중 일곱이 벽이었다.
        /// </summary>
        public static bool RoomHoldsShop(Vector2 _roomSize, float _radius)
        {
            float need = _radius * 2f + ROOM_WALL_MARGIN;
            return _roomSize.x >= need && _roomSize.y >= need;
        }

        /// <summary>
        /// 이 맵에 상점 몇 곳을 세울까 (기획서 §6 — 대형 4 · 기본·확장 2).
        ///
        /// **전역 상수 하나로는 안 된다**: 30×30에 넷을 세우면 서로 붙어 서서
        /// "한 곳을 지키는 동안 나머지가 열린다"가 성립하지 않는다.
        /// 간격 완화(<see cref="GAP_RELAX_STEPS"/>)만 넣고 개수를 그대로 두면
        /// 완화가 지키려던 것을 개수가 도로 깬다.
        /// </summary>
        public static int CountFor(float _halfExtent, int _largeCount, int _smallCount, float _largeHalfExtent)
        {
            int count = _halfExtent >= _largeHalfExtent ? _largeCount : _smallCount;
            return Mathf.Max(1, count);
        }

        /// <summary>
        /// 상점을 앉힐 방 고르기. 시드로 섞은 뒤 **원반이 들어가는 방을 앞으로 당기고**,
        /// 서로 <c>_minGap</c> 이상 떨어진 N개를 집는다.
        ///
        /// 좁은 방을 아예 버리지는 않는다 — 작은 맵에서는 들어가는 방이 하나도 없을 수 있고,
        /// 그때는 좁아도 상점이 서는 편이 상점이 없는 것보다 낫다.
        /// </summary>
        public static List<Vector2> Pick(IReadOnlyList<RoomRect> _rooms, int _count, float _minGap,
            float _radius, int _seed, SpotCheck _check = null)
        {
            if (_rooms == null || _rooms.Count == 0 || _count <= 0) return new List<Vector2>();

            List<int> order = Shuffled(_rooms.Count, _seed);

            // 섞은 순서는 그대로 두고 좋은 방만 앞으로 — 시드가 같으면 결과도 같다.
            // 1순위 원반이 열린 넉넉한 방 · 2순위 열린 좁은 방 · 3순위 막힌 방(마지막 후퇴).
            // **막힌 방도 버리지는 않는다** — 다 걸러 상점이 0곳이 되는 쪽이 더 나쁘다.
            var open = new List<Vector2>(order.Count);
            var tight = new List<Vector2>(order.Count);
            var blocked = new List<Vector2>(order.Count);
            foreach (int idx in order)
            {
                // 먼저 **막힌 자리를 옆으로 민다** — 방이 여섯뿐인 맵에서는 거르기만 해서는
                // 고를 것이 안 남는다(2026-09-08 기본 맵 실측: 방 중심 둘 다 나빴다).
                Vector2 center = NudgeToOpen(_rooms[idx].Center, _radius, _check);
                if (!DiscUsable(center, _radius, _check)) { blocked.Add(center); continue; }
                if (RoomHoldsShop(_rooms[idx].Size, _radius)) open.Add(center);
                else tight.Add(center);
            }
            open.AddRange(tight);
            open.AddRange(blocked);

            return SelectRelaxing(open, _count, _minGap, _radius);
        }

        /// <summary>
        /// 손 배치 후보에서 오늘 설 자리를 고른다 — 자리는 레벨디자이너 것이고,
        /// **오늘 어느 넷이 서는지**만 판이 정한다(기획서 §7-1).
        ///
        /// 후보가 목표 이하면 지금처럼 전부 쓴다(고를 것이 없다). 후보가 더 많으면 시드로 섞어
        /// 간격 규칙까지 태운다 — 레벨디자이너가 붙여 찍었어도 오늘 선 넷이 뭉치지는 않게.
        /// </summary>
        public static List<Vector2> PickPlaced(IReadOnlyList<Vector2> _placed, int _count, float _minGap,
            float _radius, int _seed, SpotCheck _check = null)
        {
            if (_placed == null || _placed.Count == 0 || _count <= 0) return new List<Vector2>();

            // 후보가 목표 이하라도 **열린 자리를 앞으로** 당긴다 — 그 순서가 그대로 스폰 순서다.
            if (_placed.Count <= _count) return OpenFirst(_placed, _radius, _check);

            List<int> order = Shuffled(_placed.Count, _seed);
            var ordered = new List<Vector2>(order.Count);
            foreach (int idx in order) ordered.Add(_placed[idx]);

            return SelectRelaxing(OpenFirst(ordered, _radius, _check), _count, _minGap, _radius);
        }

        /// <summary>
        /// 막힌 자리는 옆으로 밀고, 열린 원반을 앞으로 — **안정 분할**이라 검사가 없으면 순서가 그대로다.
        /// </summary>
        private static List<Vector2> OpenFirst(IReadOnlyList<Vector2> _spots, float _radius, SpotCheck _check)
        {
            var open = new List<Vector2>(_spots.Count);
            var blocked = new List<Vector2>(_spots.Count);
            for (int i = 0; i < _spots.Count; i++)
            {
                Vector2 at = NudgeToOpen(_spots[i], _radius, _check);
                if (DiscUsable(at, _radius, _check)) open.Add(at);
                else blocked.Add(at);
            }
            open.AddRange(blocked);
            return open;
        }

        /// <summary>
        /// 손 배치 맵(2026-09-07, `Layout_Warehouse01`)은 레벨디자이너가 찍은 자리 중에서 고른다 —
        /// 방 중심 규칙은 절차 맵(방 목록이 있는)에만. 손 배치인데 자리가 비어 있으면 방 규칙으로 후퇴한다.
        /// </summary>
        public static List<Vector2> Resolve(bool _handPlaced, IReadOnlyList<Vector2> _placed,
            IReadOnlyList<RoomRect> _rooms, int _count, float _minGap, float _radius, int _seed,
            SpotCheck _check = null)
        {
            if (_handPlaced && _placed != null && _placed.Count > 0)
                return PickPlaced(_placed, _count, _minGap, _radius, _seed, _check);
            return Pick(_rooms, _count, _minGap, _radius, _seed, _check);
        }

        // ===== 원반 안을 본다 (2026-09-08) =====

        /// <summary>
        /// 탐침을 찍을 둘레 반경(m) — 설치물이 원반 **안에** 통째로 들어가는 가장 바깥 고리다.
        /// 원반이 설치물보다 작으면 0이 되어 중심 한 점만 본다.
        /// </summary>
        public static float ProbeRingRadius(float _radius, float _placeHalfExtent) =>
            Mathf.Max(0f, _radius - (_placeHalfExtent > 0f ? _placeHalfExtent : DEFAULT_PLACE_HALF_EXTENT));

        /// <summary>원반 안에서 설치를 시도할 자리들 — 중심 + 둘레 <see cref="DISC_PROBE_RING"/>.</summary>
        public static void ProbePoints(Vector2 _center, float _radius, float _placeHalfExtent, List<Vector2> _into)
        {
            if (_into == null) return;
            _into.Clear();
            _into.Add(_center);

            float ring = ProbeRingRadius(_radius, _placeHalfExtent);
            if (ring <= 1e-3f) return;

            for (int i = 0; i < DISC_PROBE_RING; i++)
            {
                float a = i * (Mathf.PI * 2f / DISC_PROBE_RING);
                _into.Add(_center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring);
            }
        }

        /// <summary>
        /// 이 원반 안에서 설치가 되는 자리가 몇인가(중심 포함 아홉 중). 질의가 없으면 전부 된다고 본다.
        ///
        /// 합격·불합격을 가르지는 않는다 — <see cref="DiscUsable"/>이 그 일을 하고, 이 수는
        /// **어느 자리가 더 나은가**를 재는 데(밀어낼 곳 고르기·QA 훅) 쓴다.
        /// 2026-09-08 실측에서 물리(`ShopController.PlacementFits`)와 이 격자 셈이 정확히 같았다
        /// (기본 맵 (-10.5,-10.5) 3/9 · (10.5,12) 1/9).
        /// </summary>
        public static int FreeProbeCount(Vector2 _center, float _radius, SpotCheck _check)
        {
            var probes = new List<Vector2>(DISC_PROBE_RING + 1);
            ProbePoints(_center, _radius, _check != null ? _check.PlaceHalfExtent : 0f, probes);
            if (_check == null || _check.IsFree == null) return probes.Count;

            int free = 0;
            for (int i = 0; i < probes.Count; i++)
            {
                if (_check.IsFree(probes[i])) free++;
            }
            return free;
        }

        /// <summary>원반 **한가운데**에 설치물이 들어가나 — 상점 표시 위에 서서 겨누는 그 자리다.</summary>
        public static bool CenterFree(Vector2 _center, SpotCheck _check) =>
            _check == null || _check.IsFree == null || _check.IsFree(_center);

        /// <summary>
        /// 이 원반이 **삼키면 안 되는 점**(고양이 스폰)을 물고 있나.
        /// 물고 있으면 판이 시작하자마자 고양이가 상점 한복판에 서서 쥐가 아예 못 산다.
        /// </summary>
        public static bool SwallowsKeepAway(Vector2 _center, float _radius, SpotCheck _check)
        {
            if (_check == null || _check.KeepAway == null) return false;

            float reach = _radius + Mathf.Max(0f, _check.KeepAwayMargin);
            float reachSqr = reach * reach;
            for (int i = 0; i < _check.KeepAway.Count; i++)
            {
                if ((_check.KeepAway[i] - _center).sqrMagnitude <= reachSqr) return true;
            }
            return false;
        }

        /// <summary>
        /// 이 자리에 상점을 세워도 되나 — **원반 한가운데에 설치가 되고**, 고양이 스폰을 안 삼킨다.
        /// 검사(<paramref name="_check"/>)가 없으면 늘 참이다(예전 동작 그대로).
        ///
        /// 왜 중심이 기준인가: 상점 표시가 곧 쥐가 서서 겨누는 자리다. 실제로 무너진 것도 거기였다 —
        /// 2026-09-08 기본 맵 (-10.5,-10.5)에서 <c>PlacementFits</c>가 요새·창고 둘 다 거절했다.
        /// 「둘레 여덟 중 몇이 벽인가」는 합격선이 아니라 **더 나은 자리를 고르는 저울**이다
        /// (<see cref="FreeProbeCount"/>) — 좁은 맵에서 합격선으로 쓰면 후보가 통째로 사라진다.
        /// </summary>
        public static bool DiscUsable(Vector2 _center, float _radius, SpotCheck _check)
        {
            if (_check == null) return true;
            if (SwallowsKeepAway(_center, _radius, _check)) return false;
            return CenterFree(_center, _check);
        }

        /// <summary>
        /// 막힌 자리를 **가장 가까운 열린 자리로 민다** (2026-09-08).
        ///
        /// 거르기만으로는 안 된다: 기본 맵은 방이 여섯뿐이고 실측해 보니 방 중심이 둘 다 나빴다.
        /// 전부 떨어뜨리면 후퇴 규칙이 결국 그중 하나를 세우므로 **고쳐지는 게 없다**.
        /// 그래서 자리를 옮긴다 — 상점은 '이 방 어딘가'면 되지 '방의 정확한 중심'일 필요가 없다.
        ///
        /// 가까운 고리부터 본다. 한 고리에서 찾으면 더 멀리 안 나간다 — 상점이 방을 벗어나
        /// 엉뚱한 데 서는 것보다 조금 치우쳐 서는 편이 낫다. 끝내 못 찾으면 원래 자리를 돌려준다
        /// (상점이 사라지는 쪽이 더 나쁘다).
        /// </summary>
        public static Vector2 NudgeToOpen(Vector2 _center, float _radius, SpotCheck _check)
        {
            if (_check == null || DiscUsable(_center, _radius, _check)) return _center;

            for (int step = 1; step <= NUDGE_STEPS; step++)
            {
                float d = NUDGE_STEP_METERS * step;
                Vector2 best = _center;
                int bestFree = -1;

                for (int i = 0; i < DISC_PROBE_RING; i++)
                {
                    float a = i * (Mathf.PI * 2f / DISC_PROBE_RING);
                    Vector2 at = _center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (!DiscUsable(at, _radius, _check)) continue;

                    // 같은 거리면 **원반이 더 넓게 열린 쪽**을 고른다. 각도 순서가 고정이라
                    // 동점이면 늘 같은 답이 나온다(시드가 같으면 판도 같아야 한다).
                    int free = FreeProbeCount(at, _radius, _check);
                    if (free <= bestFree) continue;
                    bestFree = free;
                    best = at;
                }

                if (bestFree >= 0) return best;
            }
            return _center;
        }

        /// <summary>수평 반경 판정 — 높이는 무시(평면 맵).</summary>
        /// <summary>
        /// 상점 부스가 보는 방향(도, y축) — **맵 가운데를 본다**(창구가 방 안쪽으로 난다, 2026-09-19).
        /// 가운데에 거의 붙어 있으면 0(북쪽).
        /// </summary>
        public static float FacingYaw(Vector2 _position)
        {
            if (_position.sqrMagnitude < 0.25f) return 0f;
            return Mathf.Atan2(-_position.x, -_position.y) * Mathf.Rad2Deg;
        }

        public static bool Contains(Vector3 _center, Vector3 _point, float _radius)
        {
            float dx = _point.x - _center.x, dz = _point.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }

        /// <summary>0..n−1을 시드로 섞은 순서 — Fisher-Yates, `System.Random`이라 플랫폼이 달라도 같다.</summary>
        private static List<int> Shuffled(int _count, int _seed)
        {
            var order = new List<int>(_count);
            for (int i = 0; i < _count; i++) order.Add(i);
            var rng = new System.Random(_seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        /// <summary>
        /// 목표 개수를 채울 때까지 **간격을 반으로 줄여 가며** 다시 고른다.
        ///
        /// 맵 크기에 비례하는 간격은 기각했다 — 25 m는 "고양이 하나가 두 상점을 동시에 못 지키는 거리"라
        /// **절대값에 뜻이 있다**. 맵 크기로 곱하면 30×30에서 6 m가 되어 상점 둘이 나란히 서고,
        /// 그러면 상점을 여럿 만든 이유(기획서 §3 시간 결핍)가 조용히 사라진다.
        /// 재시도는 25를 **목표**로 두고 이 맵이 감당하는 만큼만 양보한다 — 대형 맵에선 한 번도 안 걸린다.
        ///
        /// 순서(<paramref name="_ordered"/>)는 **한 번만** 만들어 시도마다 재사용한다.
        /// 시도마다 다시 섞으면 같은 시드가 다른 답을 낸다.
        /// </summary>
        private static List<Vector2> SelectRelaxing(List<Vector2> _ordered, int _count, float _minGap, float _radius)
        {
            // 바닥: 원반 지름. 이보다 가까우면 원반끼리 겹쳐 "다른 곳"이 아니다.
            float floorGap = Mathf.Max(0f, _radius * 2f);
            float gap = _minGap;
            List<Vector2> best = null;

            for (int attempt = 0; attempt <= GAP_RELAX_STEPS; attempt++)
            {
                List<Vector2> got = SelectWithGap(_ordered, _count, gap);
                if (best == null || got.Count > best.Count) best = got;
                if (got.Count >= _count)
                {
                    if (attempt > 0)
                    {
                        Core.GameDebug.Log($"[ShopSpotLogic] 간격 완화 {attempt}회 — {_minGap:0.#} → {gap:0.#} m로 {got.Count}곳");
                    }
                    return got;
                }
                if (gap <= floorGap) break;
                gap = Mathf.Max(gap * 0.5f, floorGap);
            }

            Core.GameDebug.Log($"[ShopSpotLogic] 목표 {_count}곳을 못 채웠다 — 간격 {_minGap:0.#} m를 {gap:0.#} m까지 줄여 {best.Count}곳");
            return best;
        }

        /// <summary>순서대로 훑으며 이미 고른 자리에서 <paramref name="_gap"/> 이상 떨어진 것만 집는다.</summary>
        private static List<Vector2> SelectWithGap(List<Vector2> _ordered, int _count, float _gap)
        {
            var picked = new List<Vector2>(_count);
            float gapSqr = _gap * _gap;
            foreach (Vector2 p in _ordered)
            {
                bool ok = true;
                foreach (Vector2 q in picked)
                {
                    if ((p - q).sqrMagnitude < gapSqr) { ok = false; break; }
                }
                if (!ok) continue;
                picked.Add(p);
                if (picked.Count >= _count) break;
            }
            return picked;
        }
    }
}
