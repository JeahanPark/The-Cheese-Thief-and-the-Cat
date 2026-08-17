// TestManager의 일부 — 2:8 기준 밸런스(2026-08-10). 맵 규모·소리 거리·치즈 총량처럼
// "Model 값만 봐서는 배선 누락을 못 잡는" 것들을 실제 오브젝트에서 읽는다.
#if UNITY_EDITOR
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// QA: 지정 클라이언트의 발소리가 **지금 실제로 몇 m까지 닿는지**(AudioSource.maxDistance).
        /// 안 움직이면 -1. 잠행 판정을 "재생 중인가"로만 보면 반경 축소를 못 잡는다.
        /// </summary>
        public float TestGetFootstepRange(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            if (player == null) return -1f;

            // 한 걸음씩 재생(2026-08-17)이라 isPlaying은 걸음 사이에 false다 — FootstepPlayer 상태를 읽는다
            var steps = player.GetComponentInChildren<CTC.Runtime.Audio.FootstepPlayer>();
            if (steps == null || !steps.IsStepping) return -1f;
            return steps.CurrentRange;
        }

        /// <summary>QA: 지금 쥐를 고른 사람 수 — 치즈 총량이 여기 비례한다.</summary>
        public int TestGetMouseCount()
        {
            var lobby = FindFirstObjectByType<LobbyController>();
            return lobby != null ? lobby.MouseCount : 0;
        }

        /// <summary>QA: 이 판의 치즈 총량(쥐 1인당 몫 × 인원, 맵 권장 최소 인원 하한 적용).</summary>
        public int TestGetMapTotalCheese()
        {
            var director = FindFirstObjectByType<MapDirector>();
            MapLayout layout = director != null ? director.ActiveLayout : null;
            int floor = layout != null ? layout.RecommendedMiceMin : 1;
            return CheeseSpotLogic.MapTotal(
                CheeseSpotLogic.EffectiveMiceCount(TestGetMouseCount(), floor));
        }

        /// <summary>QA: 고양이 정원.</summary>
        public int TestGetCatCapacity() => LobbySession.CAT_CAPACITY;

        /// <summary>QA: 지금 깔린 맵의 규모 — "rooms=49;half=60;cheese=14;fort=8".</summary>
        public string TestGetMapShape()
        {
            var director = FindFirstObjectByType<MapDirector>();
            MapLayout layout = director != null ? director.ActiveLayout : null;
            if (layout == null) return "";

            return $"rooms={layout.Rooms.Count};half={layout.HalfExtent};" +
                   $"cheese={layout.Cheese.Count};fort={layout.Fortresses.Count};" +
                   $"micemin={layout.RecommendedMiceMin};micemax={layout.RecommendedMiceMax}";
        }

        /// <summary>QA: 실제로 씬에 선 치즈 지점 오브젝트 수 — 레이아웃 수와 어긋나면 배선 사고다.</summary>
        public int TestGetCheeseSpotObjectCount()
        {
            return FindObjectsByType<CheeseSpot>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// QA: 지점들이 **판 시작에 받은** 매장량 합계.
        ///
        /// `TestGetTotalCheeseReserve`는 지금 남은 양이라 쥐가 캐는 동안 줄어든다 —
        /// 총량 보존("지점 + 벽감 + 상자 = 총량")을 그걸로 재면 파밍 속도에 따라 실패한다
        /// (2026-08-10 실측: 빈 몫 120). 보존은 설계값으로 재는 게 맞다.
        /// </summary>
        public int TestGetDesignedSpotReserve()
        {
            var director = FindFirstObjectByType<MapDirector>();
            MapLayout layout = director != null ? director.ActiveLayout : null;
            if (layout == null) return 0;

            int spots = TestGetCheeseSpotObjectCount();
            int reserved = AlcoveLogic.TotalCheese(layout.Alcoves)
                           + LootBoxLogic.TotalCheese(layout.Boxes);
            int mice = CheeseSpotLogic.EffectiveMiceCount(TestGetMouseCount(), layout.RecommendedMiceMin);
            return spots * CheeseSpotLogic.ReservePerSpot(spots, reserved, mice);
        }

        /// <summary>QA: 로컬 플레이어의 실제 이동 속도(프리팹에 구운 값).</summary>
        public float TestGetMoveSpeed()
        {
            var move = LocalComponent<CTC.Runtime.Player.PlayerMovement>();
            return move != null ? move.BaseSpeed : -1f;
        }
    }
}
#endif
