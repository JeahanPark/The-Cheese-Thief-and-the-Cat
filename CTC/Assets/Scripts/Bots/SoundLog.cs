#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Audio;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 방금 난 소리들 (2026-08-11). 봇이 "들었다"를 판단하는 근거다.
    ///
    /// **게임 객체를 하나도 안 건드린다** — 3D 단발음이 전부 <see cref="SfxPlayer.PlayAt"/>을
    /// 지나가므로 거기서 나오는 이벤트만 구독한다. 벽감·상자·강아지·스킬은 자기가 관찰당하는 줄 모른다.
    ///
    /// 반경은 재생에 쓰인 값을 그대로 받는다 — 봇이 듣는 거리와 사람이 듣는 거리가
    /// 정의상 같아진다. 여기서 따로 정하면 그 순간 둘이 어긋난다.
    /// </summary>
    public class SoundLog
    {
        /// <summary>이 시간이 지난 소리는 잊는다. 사람도 몇 초 전 소리를 쫓지는 않는다.</summary>
        public const float MEMORY_SECONDS = 6f;

        public readonly struct Heard
        {
            public readonly SfxId Id;
            public readonly Vector3 Position;
            public readonly float MaxDistance;
            public readonly float Time;

            public Heard(SfxId _id, Vector3 _pos, float _maxDistance, float _time)
            {
                Id = _id; Position = _pos; MaxDistance = _maxDistance; Time = _time;
            }
        }

        private readonly List<Heard> m_Recent = new List<Heard>();

        public IReadOnlyList<Heard> Recent => m_Recent;

        public void Start() => SfxPlayer.Played += OnPlayed;

        public void Stop()
        {
            SfxPlayer.Played -= OnPlayed;
            m_Recent.Clear();
        }

        /// <summary>오래된 소리를 버린다. 매니저가 매 틱 한 번 부른다.</summary>
        public void Forget(float _now)
        {
            for (int i = m_Recent.Count - 1; i >= 0; i--)
            {
                if (_now - m_Recent[i].Time > MEMORY_SECONDS) m_Recent.RemoveAt(i);
            }
        }

        private void OnPlayed(SfxId _id, Vector3 _position, float _maxDistance)
        {
            m_Recent.Add(new Heard(_id, _position, _maxDistance, Time.time));
        }
    }
}
#endif
