using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>구조물(요새·미끼) 스폰 시 설치음 — 전 클라 각자 로컬 재생(3D).</summary>
    public class PlaceSfxOnSpawn : NetworkBehaviour
    {
        [SerializeField] private SfxId m_Sfx = SfxId.FortressPlace;

        public override void OnNetworkSpawn()
        {
            SfxPlayer.PlayAt(m_Sfx, transform.position);
        }
    }
}
