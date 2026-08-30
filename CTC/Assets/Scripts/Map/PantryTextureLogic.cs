using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 이음새 없는 널빤지 텍스처의 **순수 수학**. 에디터 베이커가 PNG로 굽는다.
    /// 모든 함수는 u·v ∈ [0,1]에서 주기적이다 — u=0과 u=1이 같은 값(트리플래너가 타일마다 반복).
    /// AI 텍스처(텍스처 프롬프트.md)가 오면 같은 파일명으로 덮어 교체한다 — 이 코드는 폴백이다.
    /// </summary>
    public static class PantryTextureLogic
    {
        /// <summary>정수 격자 해시 → [0,1). 시드 포함, 플랫폼 무관 결정적.</summary>
        public static float Hash(int _x, int _y, int _seed)
        {
            unchecked
            {
                uint h = (uint)(_x * 374761393 + _y * 668265263 + _seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>주기적 값 노이즈 — 격자를 _period로 감아 u=0과 u=1이 같은 값이 된다.</summary>
        public static float PeriodicNoise(float _u, float _v, int _period, int _seed)
        {
            float x = Mathf.Repeat(_u, 1f) * _period;
            float y = Mathf.Repeat(_v, 1f) * _period;
            int x0 = (int)x, y0 = (int)y;
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x1 = (x0 + 1) % _period, y1 = (y0 + 1) % _period;
            x0 %= _period; y0 %= _period;
            float a = Hash(x0, y0, _seed), b = Hash(x1, y0, _seed);
            float c = Hash(x0, y1, _seed), d = Hash(x1, y1, _seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>
        /// 가로 널빤지 색. _rows줄, 줄마다 이음이 어긋나고 널마다 밝기가 다르다.
        /// 평평한 알베도(그림자·하이라이트 없음) — 조명은 URP가 준다.
        /// </summary>
        public static Color PlankColor(float _u, float _v, int _rows, int _seed, Color _base)
        {
            float u = Mathf.Repeat(_u, 1f);
            float v = Mathf.Repeat(_v, 1f);

            float row = v * _rows;
            int rowIdx = (int)row % _rows;
            float inRow = row - (int)row;

            // 줄마다 세로 이음 위치가 어긋난다 (한 줄에 널 2장).
            // plankIdx는 %2로 감는다 — 안 감으면 u=0과 u=1에서 다른 널이 되어 이음새가 생긴다.
            float offset = Hash(rowIdx, 0, _seed);
            float plankPos = Mathf.Repeat(u * 2f + offset, 1f);
            int plankIdx = ((int)Mathf.Floor(u * 2f + offset)) % 2;

            // 널 판 밝기 + 저대비 결
            float tone = 0.82f + 0.30f * Hash(rowIdx * 7 + plankIdx, 3, _seed);
            float grain = 0.93f + 0.14f * PeriodicNoise(u, v, 24, _seed + 1);

            // 틈 — 널 경계만 살짝 어둡게 (베벨·AO 아님, 선 하나)
            float gap = 1f;
            if (inRow < 0.05f || inRow > 0.95f) gap = 0.72f;
            else if (plankPos < 0.02f || plankPos > 0.98f) gap = 0.78f;

            float k = tone * grain * gap;
            return new Color(_base.r * k, _base.g * k, _base.b * k, 1f);
        }
    }
}
