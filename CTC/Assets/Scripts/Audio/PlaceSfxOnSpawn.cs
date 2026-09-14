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
            // 먼지 링(2026-09-14) — 구조물이 "섰다"는 사건은 이미 보인다. 소리와 같은 자리에서 한 번.
            Fx.FxPlayer.PlayAt(Fx.FxId.PlaceDust, transform.position);
        }
    }
}
