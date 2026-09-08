// TestManager의 일부 — 2:8 기준 밸런스(2026-08-10). 맵 규모·소리 거리·치즈 총량처럼
// "Model 값만 봐서는 배선 누락을 못 잡는" 것들을 실제 오브젝트에서 읽는다.
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        /// QA: **지금 이 판이 실제로 어떤 수치로 도는가** — `BalanceInfo`의 필드 하나를 이름으로 읽는다.
        ///
        /// CSV를 고쳤는데 계측이 안 변하면 「밸런스가 그 손잡이를 안 탄다」와
        /// 「CSV가 아직 안 실렸다」를 구분해야 한다. 그 구분을 이 훅이 한다(2026-09-09).
        /// 없는 이름이면 NaN.
        /// </summary>
        public float TestGetBalanceValue(string _key)
        {
            var balance = Info.InfoManager.Instance.Balance;
            System.Reflection.FieldInfo field = balance.GetType().GetField(_key);
            if (field == null) return float.NaN;

            object value = field.GetValue(balance);
            return value is float f ? f : (value is int i ? i : float.NaN);
        }

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

        /// <summary>
        /// QA: 지금 쥐를 고른 **사람** 수. 로비를 못 찾으면 -1.
        ///
        /// **이 수는 치즈 총량의 분모가 아니다**(2026-09-09 감사 T7) — 총량은 봇을 포함한
        /// 「실제로 선 몸」에 비례한다(<see cref="TestGetSpawnedMouseCount"/>). 이 훅은
        /// 「계측 판에 사람이 섞였나」를 묻는 오염 검사 전용이다.
        /// </summary>
        public int TestGetMouseCount()
        {
            var lobby = FindFirstObjectByType<LobbyController>();
            if (lobby == null)
            {
                GameDebug.LogWarning("[TestManager] LobbyController 없음 — 사람 쥐 수를 못 센다");
                return -1;
            }
            return lobby.MouseCount;
        }

        /// <summary>
        /// QA: 지금 씬에 **실제로 선 쥐의 몸** 수(봇 포함) — 치즈 총량의 분모다.
        /// 게임이 쓰는 바로 그 함수를 부른다(<see cref="CheeseSpotDirector.CountSpawnedMice"/>).
        /// </summary>
        public int TestGetSpawnedMouseCount() => CheeseSpotDirector.CountSpawnedMice();

        /// <summary>
        /// QA: 게임이 **매장량을 깔 때 실제로 쓴** 쥐 수. 총량 산수의 분모는 이 수다.
        /// 아직 안 깔았거나(로비) 서버가 아니면 지금 선 몸 수로 대신하고 경고를 남긴다.
        /// </summary>
        public int TestGetDesignedMouseCount()
        {
            var spots = FindFirstObjectByType<CheeseSpotDirector>();
            if (spots != null && spots.DesignedMiceCount >= 0) return spots.DesignedMiceCount;

            GameDebug.LogWarning("[TestManager] 매장량을 아직 안 깔았다 — 지금 선 몸 수로 답한다");
            return TestGetSpawnedMouseCount();
        }

        /// <summary>
        /// QA: 이 판의 치즈 총량(쥐 1인당 몫 × 인원, 맵 권장 최소 인원 하한 적용).
        ///
        /// 분모는 **로비 사람 수가 아니라 실제로 선 몸**이다(2026-09-09 감사 T7).
        /// 예전에는 훅만 로비를 봐서, 봇이 8마리 낀 판에서 게임의 실제 총량과
        /// **최대 8배** 어긋났다(사람만 있는 스위트라 아직 안 물렸을 뿐이다).
        /// </summary>
        public int TestGetMapTotalCheese()
        {
            var director = FindFirstObjectByType<MapDirector>();
            MapLayout layout = director != null ? director.ActiveLayout : null;
            int floor = layout != null ? layout.RecommendedMiceMin : 1;
            return CheeseSpotLogic.MapTotal(
                CheeseSpotLogic.EffectiveMiceCount(TestGetDesignedMouseCount(), floor));
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
            if (layout == null)
            {
                GameDebug.LogWarning("[TestManager] MapLayout 없음 — 설계 매장량을 못 낸다");
                return -1;
            }

            int spots = TestGetCheeseSpotObjectCount();
            int reserved = AlcoveLogic.TotalCheese(layout.Alcoves)
                           + LootBoxLogic.TotalCheese(layout.Boxes);
            // 분모는 게임과 같은 것을 쓴다(감사 T7·E-16).
            int mice = CheeseSpotLogic.EffectiveMiceCount(
                TestGetDesignedMouseCount(), layout.RecommendedMiceMin);
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
