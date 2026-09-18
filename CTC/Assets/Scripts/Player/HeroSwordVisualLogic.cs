using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 용사 칼 소품의 **보이기와 방향** — 순수 함수(2026-09-19, 기획 `2026-09-19-hero-sword-design.md` §5).
    ///
    /// ── 칼은 쓸 때만 보인다 ──
    /// 네 발로 걷는 쥐가 앞발에 칼을 들고 있으면 걸음마다 칼날이 **바닥과 볼을 뚫는다**(블렌더 렌더로 확인 —
    /// 앞다리가 155mm로 짧고 큰 머리 바로 밑에 붙어 있다). 그래서 총·몽둥이와 같은 규칙으로 **휘두를 때와 변신할 때만**
    /// 앞발에 나타난다. 용사라는 것은 변신 연출(1.5초)과 매 휘두르기, 맞은 쪽 머리 위 별로 드러난다.
    ///
    /// ── 칼 방향은 코드가 준다 ──
    /// 리그에 손목 뼈가 없어서 앞다리 하나로는 칼끝이 90도도 못 돈다. 그래서 칼은 **앞발 끝에 붙어 따라가되
    /// 방향은 캐릭터 기준으로 직접 준다**(<see cref="HeroLook"/>이 LateUpdate에서 덮어쓴다).
    /// 키는 블렌더 클립 원본(<c>tools/blender/clips/sword_swing.py</c>의 <c>SWORD</c>)과 **같은 숫자**다 —
    /// 하나를 고치면 둘 다 고친다. 보간도 같다(키 사이 스무스스텝).
    ///
    /// 방향 규약: yaw 0 = 앞, + = 오른쪽(180 = 뒤) · pitch 0 = 수평, 90 = 하늘, 90을 넘으면 뒤로 눕는다.
    /// </summary>
    public static class HeroSwordVisualLogic
    {
        /// <summary>블렌더 클립 프레임레이트. 키의 프레임 번호를 초로 바꾼다(1프레임 = 0초).</summary>
        public const float FPS = 24f;

        /// <summary>휘두르기 전체(<c>SwordSwing</c> 15프레임) — 원샷 길이로도 쓴다.</summary>
        public const float SWING_SECONDS = 14f / FPS;

        /// <summary>칼이 다 나타나는 순간(2프레임). 한 프레임에 차오른다 — 늦게 뜨면 빈손으로 치켜든다.</summary>
        public const float SWING_SHOW_AT = 1f / FPS;

        /// <summary>칼이 사라지기 시작하는 순간(12프레임) — 13프레임에 0.</summary>
        public const float SWING_HIDE_AT = 11f / FPS;

        /// <summary>나타나기·사라지기 한 번의 길이(한 프레임).</summary>
        public const float FADE_SECONDS = 1f / FPS;

        // sword_swing.py SWORD — (프레임, yaw, pitch). 오른쪽 위로 치켜들고(5·6) → 오른쪽 앞으로 내리벤다(7·8) → 앞으로 거둔다.
        private static readonly float[] SWING_AIM =
        {
            1, 20f, 60f,   3, 25f, 95f,   5, 18f, 110f,   6, 15f, 112f,   7, 32f, 40f,
            8, 38f, -4f,   9, 20f, -6f,   10, 2f, -2f,    13, -4f, 6f,    15, 0f, 6f,
        };

        /// <summary>휘두르기 시작 후 <paramref name="_seconds"/>초의 칼 알파.</summary>
        public static float SwingAlpha(float _seconds)
            => Alpha(_seconds, SWING_SHOW_AT, SWING_HIDE_AT, FADE_SECONDS, FADE_SECONDS);

        /// <summary>휘두르기 시작 후 <paramref name="_seconds"/>초의 칼 방향 (yaw, pitch).</summary>
        public static Vector2 SwingAim(float _seconds) => Sample(SWING_AIM, _seconds);

        /// <summary>
        /// 보이기 규칙 — <paramref name="_showAt"/>에 다 차오르도록 그 전 <paramref name="_fadeIn"/> 동안 0→1,
        /// <paramref name="_hideAt"/>부터 <paramref name="_fadeOut"/> 동안 1→0. 그 밖은 0.
        /// </summary>
        public static float Alpha(float _seconds, float _showAt, float _hideAt, float _fadeIn, float _fadeOut)
        {
            if (_seconds < _showAt)
                return _fadeIn <= 0f ? 0f : Mathf.Clamp01(1f - (_showAt - _seconds) / _fadeIn);
            if (_seconds <= _hideAt) return 1f;
            return _fadeOut <= 0f ? 0f : Mathf.Clamp01(1f - (_seconds - _hideAt) / _fadeOut);
        }

        /// <summary>
        /// (프레임, yaw, pitch)를 세 칸씩 늘어놓은 키를 초로 샘플한다 — 키 사이는 스무스스텝
        /// (블렌더 쪽 미리보기·측정 스크립트와 같은 보간). 앞·뒤 밖은 끝 키에 붙는다.
        /// </summary>
        public static Vector2 Sample(float[] _keys, float _seconds)
        {
            int count = _keys.Length / 3;
            if (count == 0) return Vector2.zero;
            float frame = 1f + _seconds * FPS;
            if (frame <= _keys[0]) return new Vector2(_keys[1], _keys[2]);
            for (int i = 0; i < count - 1; i++)
            {
                float f0 = _keys[i * 3], f1 = _keys[(i + 1) * 3];
                if (frame > f1) continue;
                float t = (frame - f0) / (f1 - f0);
                t = t * t * (3f - 2f * t);
                return new Vector2(
                    Mathf.Lerp(_keys[i * 3 + 1], _keys[(i + 1) * 3 + 1], t),
                    Mathf.Lerp(_keys[i * 3 + 2], _keys[(i + 1) * 3 + 2], t));
            }
            int last = (count - 1) * 3;
            return new Vector2(_keys[last + 1], _keys[last + 2]);
        }

        /// <summary>캐릭터 기준 방향(x 오른쪽 · y 위 · z 앞). 길이 1.</summary>
        public static Vector3 Direction(float _yawDegrees, float _pitchDegrees)
        {
            float yaw = _yawDegrees * Mathf.Deg2Rad;
            float pitch = _pitchDegrees * Mathf.Deg2Rad;
            float flat = Mathf.Cos(pitch);
            return new Vector3(flat * Mathf.Sin(yaw), Mathf.Sin(pitch), flat * Mathf.Cos(yaw));
        }

        /// <summary>
        /// 캐릭터 기준 칼 회전 — 칼 메시의 +Z(날)가 <paramref name="_direction"/>을 보고, +X(날의 넓이)는
        /// **캐릭터의 오른쪽을 날에 직교시킨 것**이다. 그래서 수평으로 든 칼은 넓은 면이 위를 보고(위 카메라에 보인다),
        /// 하늘을 찌른 칼은 넓은 면이 앞뒤를 본다(뒤 카메라에 보인다).
        /// </summary>
        public static Quaternion LocalRotation(Vector3 _direction)
        {
            Vector3 d = _direction.sqrMagnitude > 1e-8f ? _direction.normalized : Vector3.forward;
            Vector3 width = Vector3.right - d * Vector3.Dot(Vector3.right, d);
            // 날이 정확히 옆을 볼 때만 넓이 축이 사라진다(키에는 없다) — 그때는 위를 넓이로 쓴다.
            if (width.sqrMagnitude < 1e-6f) width = Vector3.up - d * Vector3.Dot(Vector3.up, d);
            width.Normalize();
            return Quaternion.LookRotation(d, Vector3.Cross(d, width));
        }
    }
}
