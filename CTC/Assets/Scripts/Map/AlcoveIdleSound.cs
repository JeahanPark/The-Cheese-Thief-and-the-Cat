using CTC.Runtime.Audio;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 벽감이 흘리는 소리 (v3.8 5번). **강아지 벽만 낑낑거리고 치즈 벽은 조용하다.**
    ///
    /// 벽감 스펙이 예약해 둔 규칙을 여기서 채운다 — 투명(항상 보임)이면 "치즈 벽만 골라 부순다"가
    /// 최적해가 되어 강아지 벽이 죽은 콘텐츠가 되고, 불투명(전혀 모름)이면 지는 쪽이 판을 엎으려
    /// 해도 운에 맡겨야 한다. 이 게임은 이미 소리가 정보인 게임이라 같은 문법에 태운다.
    ///
    /// 도달 거리를 짧게 두는 게 핵심이다(`DogAlcoveIdleSoundMeters`) — **가까이 가야 안다.**
    /// 멀리서 다 들리면 투명한 것과 같아진다.
    ///
    /// 클라도 각자 낸다. 서버가 Rpc로 쏘면 루프를 붙였다 뗐다 해야 하고, 늦게 들어온 사람이
    /// 조용한 벽을 보게 된다 — `Alcove.Payload`가 복제되므로 각자 보고 각자 틀면 된다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AlcoveIdleSound : NetworkBehaviour
    {
        private AudioSource m_Source;
        private Alcove m_Alcove;

        public override void OnNetworkSpawn()
        {
            m_Source = GetComponent<AudioSource>();
            m_Alcove = GetComponent<Alcove>();
            if (m_Alcove == null) return;

            // 스폰 순서에 기대지 않는다 — 지금 값으로 한 번, 늦게 오면 그때 다시.
            m_Alcove.PayloadChanged += Apply;
            Apply(m_Alcove.Payload);
        }

        public override void OnNetworkDespawn()
        {
            if (m_Alcove != null) m_Alcove.PayloadChanged -= Apply;
        }

        private void Apply(AlcovePayload _payload)
        {
            if (m_Source == null) return;

            if (_payload != AlcovePayload.Dog)
            {
                m_Source.Stop();
                return;
            }
            if (m_Source.isPlaying) return;

            AudioClip clip = SfxPlayer.ClipFor(SfxId.DogAlcoveIdle);
            if (clip == null) return;

            m_Source.clip = clip;
            m_Source.loop = true;
            m_Source.maxDistance = Info.InfoManager.Instance.Balance.DogAlcoveIdleSoundMeters;
            m_Source.Play();
        }
    }
}
