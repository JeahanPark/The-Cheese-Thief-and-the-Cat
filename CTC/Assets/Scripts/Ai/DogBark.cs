using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 배회하는 강아지가 주기적으로 짖는다 (v3.8 5번).
    ///
    /// **소리가 정보인 게임이라 이게 유일한 위치 정보다.** 강아지는 미니맵에 없고 팀도 아니라
    /// "어디쯤 있나"를 알 길이 짖는 소리뿐이다. 그래서 파밍(20m)과 같은 급으로 멀리 보낸다.
    ///
    /// 쫓는 중에는 안 짖는다 — 물기 소리가 이미 나고, 그때는 위치를 숨겨야 긴장이 산다.
    /// </summary>
    public class DogBark : NetworkBehaviour
    {
        private DogBrain m_Brain;
        private float m_NextBark;

        private void Awake()
        {
            m_Brain = GetComponent<DogBrain>();
        }

        private void Update()
        {
            if (!IsServer || m_Brain == null) return;
            if (m_Brain.State != DogState.Roam) return;

            m_NextBark -= Time.deltaTime;
            if (m_NextBark > 0f) return;

            m_NextBark = DogAiLogic.BarkInterval;
            BarkRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void BarkRpc()
        {
            Audio.SfxPlayer.PlayAt(
                Audio.SfxId.DogBark, transform.position, 1f,
                Info.InfoManager.Instance.Balance.DogBarkSoundMeters);
        }
    }
}
