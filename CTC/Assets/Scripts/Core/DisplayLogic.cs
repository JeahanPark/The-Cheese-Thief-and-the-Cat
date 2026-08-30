using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>화면 표시 방식. 이름은 유저가 옵션 창에서 읽을 말로 짓는다.</summary>
    public enum WindowMode : byte
    {
        /// <summary>테두리 없는 전체화면 창 — 알탭이 빠르고 **스팀 오버레이가 가장 안전하다.**</summary>
        Borderless = 0,

        /// <summary>독점 전체화면. 해상도를 실제로 바꾸므로 낮은 해상도에서 프레임을 번다.</summary>
        Fullscreen = 1,

        Windowed = 2,
    }

    /// <summary>
    /// 해상도·창모드 규칙 — 순수 함수(EditMode TDD) (2026-08-30).
    ///
    /// **왜 만들었나**: 옵션 창에 감도·볼륨밖에 없었다. 스팀 유저는 창모드·해상도를 당연히
    /// 기대하고, 없으면 리뷰 평점에 바로 나타난다(출시 점검 #9).
    ///
    /// 주사율은 목록에서 **뺀다.** 같은 1920×1080이 60/120/144Hz로 세 번 나오면 고르는 사람은
    /// 무엇이 다른지 알 수 없고, 우리는 `targetFrameRate`로 상한을 따로 두고 있어 의미도 없다.
    /// </summary>
    public static class DisplayLogic
    {
        /// <summary>이보다 작은 해상도는 UI가 안 들어간다(캔버스 기준 1920×1080의 절반 아래).</summary>
        public const int MIN_WIDTH = 1024;
        public const int MIN_HEIGHT = 576;

        public static string ResolutionLabel(int _width, int _height) => $"{_width} × {_height}";

        public static string ModeLabel(WindowMode _mode)
        {
            switch (_mode)
            {
                case WindowMode.Fullscreen: return "전체화면";
                case WindowMode.Windowed: return "창";
                default: return "테두리 없는 창";
            }
        }

        public static FullScreenMode ToUnity(WindowMode _mode)
        {
            switch (_mode)
            {
                case WindowMode.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
                case WindowMode.Windowed: return FullScreenMode.Windowed;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        public static WindowMode FromUnity(FullScreenMode _mode)
        {
            switch (_mode)
            {
                case FullScreenMode.ExclusiveFullScreen: return WindowMode.Fullscreen;
                case FullScreenMode.Windowed: return WindowMode.Windowed;
                default: return WindowMode.Borderless;
            }
        }

        /// <summary>
        /// 고를 수 있는 해상도 목록. 주사율 차이로 겹치는 것을 하나로 합치고, 너무 작은 것은 뺀다.
        /// 큰 것이 위로 오게 **내림차순** — 대부분은 제일 큰 것을 고른다.
        /// </summary>
        public static List<Vector2Int> Options(IReadOnlyList<Vector2Int> _available)
        {
            var list = new List<Vector2Int>();
            if (_available == null) return list;

            for (int i = 0; i < _available.Count; i++)
            {
                Vector2Int r = _available[i];
                if (r.x < MIN_WIDTH || r.y < MIN_HEIGHT) continue;
                if (list.Contains(r)) continue;

                list.Add(r);
            }

            list.Sort((a, b) => (b.x * b.y).CompareTo(a.x * a.y));

            // 화면이 아주 작아 전부 걸러졌다면 하한을 어겨서라도 하나는 준다 —
            // 목록이 비면 드롭다운이 빈 채로 떠서 아무것도 못 고른다.
            if (list.Count == 0 && _available.Count > 0) list.Add(_available[0]);
            return list;
        }

        /// <summary>
        /// 지금 해상도가 목록의 몇 번째인가. 정확히 없으면 **화소 수가 가장 가까운 것**으로 붙인다 —
        /// 모니터를 바꿨거나 저장된 값이 이 기기에 없을 때 0번으로 튕기면 최대 해상도가 강요된다.
        /// </summary>
        public static int NearestIndex(IReadOnlyList<Vector2Int> _options, int _width, int _height)
        {
            if (_options == null || _options.Count == 0) return 0;

            int best = 0;
            long bestDelta = long.MaxValue;
            long want = (long)_width * _height;

            for (int i = 0; i < _options.Count; i++)
            {
                if (_options[i].x == _width && _options[i].y == _height) return i;

                long delta = System.Math.Abs((long)_options[i].x * _options[i].y - want);
                if (delta >= bestDelta) continue;

                bestDelta = delta;
                best = i;
            }
            return best;
        }

        public static int ClampIndex(int _index, int _count)
        {
            if (_count <= 0) return 0;
            if (_index < 0) return 0;
            return _index >= _count ? _count - 1 : _index;
        }
    }
}
