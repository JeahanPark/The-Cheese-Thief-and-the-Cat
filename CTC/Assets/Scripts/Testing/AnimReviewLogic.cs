using System.Collections.Generic;
using CTC.Runtime.Player;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// 애니메이션 검수 씬의 순수 계산(2026-09-13) — 어떤 클립을 어떤 순서로 보여 줄지, 프레임 번호 환산.
    /// 트랜스폼·애니메이터를 모른다(EditMode 대상).
    /// </summary>
    public static class AnimReviewLogic
    {
        /// <summary>팩·자작 클립이 전부 이 속도로 구워졌다(블렌더 export_clips.py · 팩 FBX).</summary>
        public const float FPS = 24f;

        /// <summary>
        /// 그 역할이 보여 줄 상태 이름 — **자작(검수 대상)을 앞에**, 팩 원본을 뒤에 둔다.
        /// 목록의 원본은 <see cref="AnimationStateLogic"/> 하나다(여기 이름을 따로 적으면 클립이 늘 때 조용히 빠진다).
        /// </summary>
        public static List<string> ClipsFor(bool _cat)
        {
            var list = new List<string>(_cat ? AnimationStateLogic.CatOnly : AnimationStateLogic.MouseOnly);
            list.AddRange(AnimationStateLogic.Shared);
            return list;
        }

        /// <summary>자작 클립인가(화면에 [자작] 표시).</summary>
        public static bool IsSelfMade(string _state)
        {
            return System.Array.IndexOf(AnimationStateLogic.MouseOnly, _state) >= 0
                || System.Array.IndexOf(AnimationStateLogic.CatOnly, _state) >= 0;
        }

        /// <summary>클립 길이 → 마지막 프레임 번호(0부터). 0.4167초면 10(키 11개).</summary>
        public static int LastFrame(float _lengthSeconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(_lengthSeconds * FPS));
        }

        /// <summary>정규화 시간(반복하면 1을 넘는다) → 지금 프레임 번호.</summary>
        public static int FrameAt(float _normalizedTime, int _lastFrame)
        {
            float t = _normalizedTime - Mathf.Floor(_normalizedTime);
            if (_normalizedTime > 0f && t == 0f) t = 1f;   // 정확히 끝에 멈춘 순간은 마지막 프레임이다
            return Mathf.Clamp(Mathf.RoundToInt(t * _lastFrame), 0, _lastFrame);
        }

        /// <summary>프레임 번호 → 정규화 시간(한 프레임씩 넘길 때).</summary>
        public static float NormalizedOf(int _frame, int _lastFrame)
        {
            return Mathf.Clamp01((float)_frame / Mathf.Max(1, _lastFrame));
        }

        /// <summary>목록을 앞뒤로 돈다(끝에서 넘기면 처음으로).</summary>
        public static int Wrap(int _index, int _count)
        {
            if (_count <= 0) return 0;
            int r = _index % _count;
            return r < 0 ? r + _count : r;
        }

        /// <summary>재생 배속 단계 — [ ] 키로 오르내린다.</summary>
        public static readonly float[] SPEEDS = { 0.1f, 0.25f, 0.5f, 1f, 1.5f, 2f };
    }
}
