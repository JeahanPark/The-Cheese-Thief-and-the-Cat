// TestManager의 일부 — 미니맵 훅 (2026-09-08). wesqa가 리플렉션으로 Instance.<method>를 부르므로
// **타입을 쪼개지 않는다**(CLAUDE.md). 개발 빌드에도 실린다 — 배포 빌드에선 빠진다.
//
// 여기 있는 것들은 전부 **View가 실제로 켠 것**을 센다. Model(MinimapMarkers)의 목록 길이를 세면
// View가 아이콘을 하나도 안 켜도 통과한다 — 볼륨은 바뀌는데 소리는 그대로였던 사고(2026-08-09)와
// 같은 종류의 눈속임이다. 예외는 「발견 기억」뿐인데, 그건 화면에 그려지는 것이 아니라 기억이라
// 애초에 Model에만 있다(그리고 그 기억이 화면에 닿았는지는 cheese 마커 수가 따로 증언한다).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.UI;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// QA: 미니맵 판이 **실제로 화면에 떠 있는가**. Model의 Active 값이 아니라
        /// 루트 GameObject의 activeInHierarchy다 — 배선이 빠지면 여기서 false가 나온다.
        /// </summary>
        public bool TestIsMinimapVisible()
        {
            MinimapHud hud = FindFirstObjectByType<MinimapHud>();
            return hud != null && hud.IsShown;
        }

        /// <summary>
        /// QA: 지금 내 화면에 켜진 마커 아이콘 수. 미니맵을 못 찾으면 -1.
        ///
        /// <paramref name="_kind"/>: "shop" · "cheese" · "structure" · "all"(또는 빈 문자열).
        /// 모르는 이름은 -1이다 — 오타가 0으로 보이면 "적이 안 보인다"가 거짓으로 통과한다.
        ///
        /// **"all"이 종류별 합과 어긋나면 세 종류가 아닌 무언가가 그려지고 있다**는 뜻이다.
        /// 기획 2절의 「남은 절대 안 그린다」를 이 한 줄로 잡는다.
        /// </summary>
        public int TestGetMinimapMarkerCount(string _kind)
        {
            MinimapHud hud = FindFirstObjectByType<MinimapHud>();
            if (hud == null) return -1;

            string kind = _kind != null ? _kind.Trim().ToLowerInvariant() : "";
            switch (kind)
            {
                case "":
                case "all":
                case "total": return hud.CountShownIcons(null);
                case "shop": return hud.CountShownIcons(MinimapMarkerKind.Shop);
                case "cheese": return hud.CountShownIcons(MinimapMarkerKind.Cheese);
                case "structure": return hud.CountShownIcons(MinimapMarkerKind.Structure);
                default:
                    Core.GameDebug.LogWarning($"[TestManager] TestGetMinimapMarkerCount — 모르는 종류 '{_kind}'");
                    return -1;
            }
        }

        /// <summary>
        /// QA: 이 클라이언트가 지금까지 「발견」한 치즈 매장지 수(-1 = 미니맵 없음).
        /// **클라 전용 기억**이라 인스턴스마다 다르다 — 물어보는 쪽이 곧 답이다.
        /// </summary>
        public int TestGetMinimapDiscoveredCheese()
        {
            MinimapMarkers markers = FindFirstObjectByType<MinimapMarkers>();
            return markers != null ? markers.DiscoveredCheeseCount : -1;
        }

        /// <summary>
        /// QA: 켜진 마커 중 그 월드 좌표(x, z)에 가장 가까운 것까지의 거리(m).
        /// 마커가 하나도 없거나 미니맵이 없으면 -1.
        ///
        /// 「상대가 서 있는 자리에 점이 안 찍힌다」를 재는 자다. 종류만 세는 것으로는
        /// 적 마커가 상점인 척 섞여 들어와도 안 걸린다 — 그래서 **자리**를 따로 본다.
        /// </summary>
        public float TestGetMinimapNearestMarkerMeters(float _x, float _z)
        {
            MinimapHud hud = FindFirstObjectByType<MinimapHud>();
            if (hud == null) return -1f;
            return hud.NearestShownIconMeters(new Vector2(_x, _z));
        }

        /// <summary>
        /// QA: 그 클라이언트가 서 있는 자리 "x,z"(못 찾으면 빈 문자열).
        /// 위치는 NetworkTransform으로 복제되므로 어느 인스턴스에서 물어도 같다 —
        /// 미니맵 QA가 "상대는 지금 여기 있다"를 알아야 그 자리에 점이 없는지 물어볼 수 있다.
        /// </summary>
        public string TestGetPlayerPos(long _clientId)
        {
            NetworkObject player = Core.NetUtil.FindPlayerObject((ulong)_clientId);
            if (player == null) return "";
            Vector3 p = player.transform.position;
            return $"{p.x:F2},{p.z:F2}";
        }
    }
}
#endif
