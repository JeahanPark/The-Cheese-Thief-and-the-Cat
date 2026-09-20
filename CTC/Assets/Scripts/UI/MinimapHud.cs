using System.Collections.Generic;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 좌상단 미니맵 View (2026-09-08) — 「나 어디 · 상점 어디 · 내 창고 어디」에만 답한다.
    ///
    /// **입력이 없다.** 클릭도 토글도 확대도 없다(기획 4절: 새 화면 0 · 커서 해제 0).
    /// 그래서 raycastTarget을 전부 끈다 — 코너에 깔린 판이 뒤의 조작을 먹으면 안 된다.
    ///
    /// 하는 일은 셋뿐이다.
    /// ⑴ 지형: <see cref="MinimapPainter"/>로 **판 시작에 한 장** 굽는다(맵이 바뀌면 다시).
    /// ⑵ 마커: <see cref="MinimapMarkers"/>가 0.2초마다 흘려 주는 목록을 **풀에 재사용**해 찍는다.
    /// ⑶ 나: 화살표만 매 프레임 따라간다 — 목록과 달리 5 Hz면 회전이 눈에 띄게 튄다.
    ///
    /// 무엇을 그릴지는 하나도 여기서 안 정한다. 역할 판정은 <see cref="MinimapLogic"/>,
    /// 목록은 Model이 걸러 준다(규칙 5).
    /// </summary>
    public class MinimapHud : MonoBehaviour
    {
        private const float MARKER_PX = 7f;   // 200 px 평면도에서 점으로 읽히는 최소 크기

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private MinimapMarkers m_Markers;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private RawImage m_Terrain;
        [SerializeField] private RectTransform m_MarkerLayer;
        [SerializeField] private RectTransform m_MeIcon;
        // 시선 부채꼴(2026-09-20) — 내 삼각형 뒤에 깔리고 카메라 방향을 따라 돈다. 삼각형 끝(빨강)은 몸 방향이다.
        [SerializeField] private RectTransform m_ViewCone;

        private readonly List<Image> m_Pool = new List<Image>();

        // 풀 칸마다 「지금 무엇을 그리고 있나」. QA가 Model 목록이 아니라 **켜진 아이콘**을 셀 수
        // 있어야 배선 누락이 잡힌다 — 목록만 보면 View가 하나도 안 켜도 통과한다(2026-08-09 실측).
        private readonly List<MinimapMarkerKind> m_PoolKinds = new List<MinimapMarkerKind>();

        private MapLayout m_BakedLayout;
        private Texture2D m_Texture;
        private float m_HalfExtent;

        private static int SizePx => Info.InfoManager.Instance.Balance.MinimapSizePx;
        private static int TexturePx => Info.InfoManager.Instance.Balance.MinimapTexturePx;

        /// <summary>미니맵이 실제로 화면에 떠 있는가 — 켜졌다는 Model 값이 아니라 판이 켜졌는지를 본다.</summary>
        public bool IsShown => m_Root != null && m_Root.activeInHierarchy;

        /// <summary>
        /// QA: 지금 이 화면에 **실제로 켜진** 마커 아이콘 수(종류를 안 주면 전부).
        ///
        /// 총계와 종류별 합이 어긋나면 세 종류가 아닌 무언가가 그려지고 있다는 뜻이다 —
        /// 「남은 절대 안 그린다」(기획 2절)가 깨진 자리를 여기서 잡는다.
        /// </summary>
        public int CountShownIcons(MinimapMarkerKind? _kind)
        {
            int count = 0;
            for (int i = 0; i < m_Pool.Count; i++)
            {
                Image icon = m_Pool[i];
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                if (_kind.HasValue && m_PoolKinds[i] != _kind.Value) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// QA: 켜진 마커 중 그 월드 좌표에 가장 가까운 것까지의 거리(m). 하나도 없으면 -1.
        /// 「상대가 서 있는 자리에 점이 찍히지 않는다」를 재는 자다 — 종류를 세는 것만으로는
        /// 적 마커가 <see cref="MinimapMarkerKind.Shop"/>인 척 섞여 들어와도 안 걸린다.
        /// </summary>
        public float NearestShownIconMeters(Vector2 _world)
        {
            if (m_HalfExtent <= 0f) return -1f;

            float best = -1f;
            for (int i = 0; i < m_Pool.Count; i++)
            {
                Image icon = m_Pool[i];
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;

                // 아이콘이 실제로 놓인 픽셀을 다시 월드로 되돌린다 — Model 목록이 아니라
                // **화면에 놓인 자리**를 재야 View가 엉뚱한 데 찍는 사고도 같이 잡힌다.
                Vector2 at = MinimapLogic.PixelToWorld(
                    icon.rectTransform.anchoredPosition, m_HalfExtent, SizePx);
                float meters = Vector2.Distance(at, _world);
                if (best < 0f || meters < best) best = meters;
            }
            return best;
        }

        private void Start()
        {
            m_Root.SetActive(false);

            m_Markers.Active.Subscribe(OnActiveChanged).AddTo(this);
            m_Markers.MarkersChanged.Subscribe(DrawMarkers).AddTo(this);
        }

        private void OnDestroy()
        {
            // 구운 텍스처는 에셋이 아니라 런타임 생성물이다 — 안 지우면 판마다 한 장씩 샌다
            if (m_Texture != null) Destroy(m_Texture);
        }

        private void OnActiveChanged(bool _active)
        {
            if (_active) BakeTerrainIfNeeded();
            if (m_Root.activeSelf != _active) m_Root.SetActive(_active);
        }

        // 지형은 판 중에 안 변한다. 맵이 바뀌었을 때만 다시 굽는다(매 프레임·매 갱신 금지).
        private void BakeTerrainIfNeeded()
        {
            MapLayout layout = m_Lobby.ActiveLayout;
            if (layout == null || layout == m_BakedLayout) return;

            if (m_Texture != null) Destroy(m_Texture);
            m_Texture = MinimapPainter.Paint(layout, TexturePx);
            m_Terrain.texture = m_Texture;
            m_BakedLayout = layout;
            m_HalfExtent = layout.HalfExtent;
        }

        private void DrawMarkers(IReadOnlyList<MinimapMarker> _markers)
        {
            if (m_HalfExtent <= 0f) return;

            for (int i = 0; i < _markers.Count; i++)
            {
                Image icon = IconAt(i);
                m_PoolKinds[i] = _markers[i].Kind;
                icon.color = ColorOf(_markers[i].Kind);
                Place(icon.rectTransform, _markers[i].World);
                if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
            }

            // 남는 아이콘은 지우지 않고 끈다 — 다음 갱신에 그대로 다시 쓴다
            for (int i = _markers.Count; i < m_Pool.Count; i++)
            {
                if (m_Pool[i].gameObject.activeSelf) m_Pool[i].gameObject.SetActive(false);
            }
        }

        // 내 화살표만 매 프레임이다. 위치는 북 고정 지도 위 픽셀, 각은 MinimapLogic이 준다.
        private void LateUpdate()
        {
            if (!m_Root.activeSelf || m_HalfExtent <= 0f) return;

            Transform me = m_Markers.Me;
            if (me == null) return;

            Place(m_MeIcon, new Vector2(me.position.x, me.position.z));
            m_MeIcon.localRotation = Quaternion.Euler(0f, 0f,
                MinimapLogic.YawToUiAngle(me.eulerAngles.y));

            if (m_ViewCone == null) return;
            Transform view = m_Markers.View;
            bool show = view != null;
            if (m_ViewCone.gameObject.activeSelf != show) m_ViewCone.gameObject.SetActive(show);
            if (!show) return;
            m_ViewCone.anchoredPosition = m_MeIcon.anchoredPosition;
            m_ViewCone.localRotation = Quaternion.Euler(0f, 0f,
                MinimapLogic.YawToUiAngle(view.eulerAngles.y));
        }

        /// <summary>QA: 지금 화면의 두 각(UI 도) — 삼각형(몸)과 부채꼴(시선). 부채꼴이 없거나 꺼져 있으면 -1.</summary>
        public Vector2 DevHeadingAngles()
        {
            float body = m_MeIcon != null ? m_MeIcon.localEulerAngles.z : -1f;
            float view = m_ViewCone != null && m_ViewCone.gameObject.activeInHierarchy ? m_ViewCone.localEulerAngles.z : -1f;
            return new Vector2(body, view);
        }

        // 마커 레이어는 좌하단(0,0) 기준이라 픽셀 좌표가 그대로 anchoredPosition이 된다.
        private void Place(RectTransform _rt, Vector2 _world)
        {
            Vector2Int px = MinimapLogic.WorldToPixel(_world, m_HalfExtent, SizePx);
            _rt.anchoredPosition = new Vector2(px.x + 0.5f, px.y + 0.5f);
        }

        private Image IconAt(int _index)
        {
            while (m_Pool.Count <= _index) m_Pool.Add(CreateIcon());
            return m_Pool[_index];
        }

        private Image CreateIcon()
        {
            m_PoolKinds.Add(MinimapMarkerKind.Shop);   // 첫 그리기에서 바로 덮인다

            var go = new GameObject($"Marker{m_Pool.Count}", typeof(RectTransform));
            go.transform.SetParent(m_MarkerLayer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(MARKER_PX, MARKER_PX);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;   // 미니맵은 입력을 안 받는다
            return image;
        }

        /// <summary>기획 6절 표 — 색은 <see cref="UiPalette"/>에서만 온다.</summary>
        private static Color ColorOf(MinimapMarkerKind _kind)
        {
            switch (_kind)
            {
                case MinimapMarkerKind.Shop: return UiPalette.Accent;
                case MinimapMarkerKind.Cheese: return UiPalette.AccentSoft;
                case MinimapMarkerKind.Structure: return UiPalette.Good;
                default: return UiPalette.TextDim;
            }
        }
    }
}
