using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 미니맵 규칙(2026-09-08) — 순수 함수. 씬도 컴포넌트도 안 쓴다(EditMode에서 그대로 돈다).
    ///
    /// 미니맵은 레이더가 아니라 **창고 평면도**다. 답해 주는 질문은
    /// 「나 어디 · 상점 어디 · 내 창고 어디」뿐이고 「적 어디」는 답하지 않는다.
    /// 그래서 좌표 변환과 함께 **역할별 가시성 판정도 여기 모은다** —
    /// View나 Model에 if를 흩어 두면 「남을 안 그린다」는 원칙이 조용히 깨진다.
    ///
    /// 지도는 **전체 고정 · 북 고정**이다. 따라다니지도, 돌지도 않는다.
    /// </summary>
    public static class MinimapLogic
    {
        /// <summary>
        /// 지형 사각형의 최소 두께(px). 120 m를 256 px에 넣으면 1 m짜리 선반이
        /// 0.x px가 되어 통째로 사라진다 — 구조 제약이라 CSV로 내리지 않는다.
        /// </summary>
        public const int MIN_THICKNESS = 2;

        /// <summary>
        /// 월드 (x, z)를 텍스처 픽셀로. 월드 원점이 텍스처 중앙이고 월드 z+가 텍스처 위쪽(y+)이다
        /// (Texture2D의 (0,0)이 좌하단이라 z를 그대로 y에 태우면 북이 위가 된다).
        /// 범위 밖은 [0, _px-1]로 클램프한다 — 맵 밖으로 밀려난 마커도 가장자리에는 남는다.
        /// </summary>
        public static Vector2Int WorldToPixel(Vector2 _world, float _halfExtent, int _px)
        {
            if (_px <= 0) return Vector2Int.zero;
            float extent = Mathf.Max(_halfExtent, 1e-4f);
            float scale = _px / (extent * 2f);
            int x = Mathf.FloorToInt((_world.x + extent) * scale);
            int y = Mathf.FloorToInt((_world.y + extent) * scale);
            return new Vector2Int(Mathf.Clamp(x, 0, _px - 1), Mathf.Clamp(y, 0, _px - 1));
        }

        /// <summary>
        /// <see cref="WorldToPixel"/>의 역 — 지도 위 픽셀이 가리키는 월드 (x, z).
        ///
        /// QA가 「저 점이 진짜 어디를 가리키나」를 되물을 때 쓴다(적 자리에 점이 안 찍히는지).
        /// 픽셀은 정수로 반올림된 값이라 되돌린 좌표에는 **픽셀 한 칸만큼의 오차**가 남는다 —
        /// 「같은 자리인가」를 재는 데는 되지만 좌표를 복원하는 데 쓰면 안 된다.
        /// </summary>
        public static Vector2 PixelToWorld(Vector2 _pixel, float _halfExtent, int _px)
        {
            if (_px <= 0) return Vector2.zero;
            float extent = Mathf.Max(_halfExtent, 1e-4f);
            float scale = _px / (extent * 2f);
            return new Vector2(_pixel.x / scale - extent, _pixel.y / scale - extent);
        }

        /// <summary>
        /// 월드 사각형(중심·크기 m)을 텍스처 픽셀 사각형으로.
        /// 폭/높이가 <paramref name="_minThickness"/>보다 얇으면 **중심을 지킨 채** 그만큼 벌린다.
        /// 텍스처 밖으로 나간 부분은 잘라내고, 완전히 밖이면 width·height가 0인 사각형을 준다.
        /// </summary>
        public static RectInt RectToPixels(Vector2 _center, Vector2 _size, float _halfExtent, int _px, int _minThickness)
        {
            if (_px <= 0) return new RectInt(0, 0, 0, 0);
            float extent = Mathf.Max(_halfExtent, 1e-4f);
            float scale = _px / (extent * 2f);
            float halfW = Mathf.Abs(_size.x) * 0.5f;
            float halfH = Mathf.Abs(_size.y) * 0.5f;

            int x0 = Mathf.RoundToInt((_center.x - halfW + extent) * scale);
            int x1 = Mathf.RoundToInt((_center.x + halfW + extent) * scale);
            int y0 = Mathf.RoundToInt((_center.y - halfH + extent) * scale);
            int y1 = Mathf.RoundToInt((_center.y + halfH + extent) * scale);

            int minThickness = Mathf.Max(_minThickness, 1);
            Widen(ref x0, ref x1, minThickness);
            Widen(ref y0, ref y1, minThickness);

            int cx0 = Mathf.Max(x0, 0);
            int cx1 = Mathf.Min(x1, _px);
            int cy0 = Mathf.Max(y0, 0);
            int cy1 = Mathf.Min(y1, _px);
            if (cx1 <= cx0 || cy1 <= cy0) return new RectInt(0, 0, 0, 0);
            return new RectInt(cx0, cy0, cx1 - cx0, cy1 - cy0);
        }

        /// <summary>얇은 변을 중심 유지한 채 최소 두께까지 벌린다.</summary>
        private static void Widen(ref int _lo, ref int _hi, int _minThickness)
        {
            int span = _hi - _lo;
            if (span >= _minThickness) return;
            int grow = _minThickness - span;
            int lower = grow / 2;
            _lo -= lower;
            _hi += grow - lower;
        }

        /// <summary>
        /// 그 마커에 가 본 적이 있는가. **경계값 포함** — 딱 <paramref name="_meters"/>면 발견이다.
        /// (판정을 &lt;로 두면 CSV 값이 「닿아도 안 되는 거리」가 돼 뜻이 어긋난다.)
        /// </summary>
        public static bool IsDiscovered(Vector2 _marker, Vector2 _me, float _meters)
        {
            return (_marker - _me).sqrMagnitude <= _meters * _meters;
        }

        // ── 역할별 가시성 (기획 2절 표를 그대로 옮긴 것) ─────────────────
        // 관전자(RoleId.None)는 전부 false다 — 죽어서 보는 사람에게 산 사람의 정보를 주지 않는다.

        /// <summary>상점 자리 4곳 — 쥐는 가야 하고 고양이는 막아야 한다. **여기서 만난다.**</summary>
        public static bool ShowsShops(RoleId _role) => _role == RoleId.Mouse || _role == RoleId.Cat;

        /// <summary>치즈 매장지 — 쥐만, 그것도 발견분만. 고양이가 알면 매복이 공짜다.</summary>
        public static bool ShowsCheese(RoleId _role) => _role == RoleId.Mouse;

        /// <summary>내 창고·내 요새 — 쥐만. 「째고 나서 어디로 돌아가나」에 답한다.</summary>
        public static bool ShowsOwnStructures(RoleId _role) => _role == RoleId.Mouse;

        /// <summary>
        /// 팀 쥐 위치 — **지금은 누구에게도 안 보인다.** 뭉치면 안 되는 게임이라서다.
        ///
        /// 그런데도 상수가 아니라 함수로 두는 이유: 이건 기술 제약이 아니라 **밸런스 판단**이다.
        /// 쥐가 너무 흩어져 고양이에게 하나씩 잘린다는 결론이 나오면 여기 한 줄을
        /// <c>_role == RoleId.Mouse</c>로 바꾸는 것만으로 팀 표시가 켜져야 한다 —
        /// 호출부(Model·View)는 손대지 않는다.
        /// </summary>
        public static bool ShowsTeammates(RoleId _role) => false;

        /// <summary>
        /// 북 고정 지도 위에서 내 화살표를 돌릴 각(UI z회전).
        /// 월드 yaw 0(=+z, 북)이면 화살표는 위를 본다.
        /// 월드 yaw는 위에서 볼 때 **시계 방향**으로 늘고(yaw 90 = +x = 동),
        /// Unity UI의 z회전은 **반시계**라 부호를 뒤집는다. 결과는 [0, 360).
        /// </summary>
        public static float YawToUiAngle(float _yawDeg)
        {
            float angle = -_yawDeg % 360f;
            if (angle < 0f) angle += 360f;
            return angle;
        }
    }
}
