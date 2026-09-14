using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>런타임에 만든 메시는 오브젝트와 따로 산다 — 맵을 다시 지을 때 같이 지운다(<see cref="MapCeiling"/>).</summary>
    public class MeshLifetime : MonoBehaviour
    {
        private Mesh m_Mesh;

        public void Own(Mesh _mesh) => m_Mesh = _mesh;

        private void OnDestroy()
        {
            if (m_Mesh != null) Destroy(m_Mesh);
        }
    }
}
