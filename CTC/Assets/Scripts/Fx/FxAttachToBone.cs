using CTC.Runtime.Core;
using UnityEngine;

namespace CTC.Runtime.Fx
{
    /// <summary>
    /// 이펙트 프리팹 하나를 이 오브젝트의 **본**에 붙인다. 그게 전부다.
    ///
    /// 이펙트가 무엇으로 이루어졌고 어디에 몇 개가 있는지는 **프리팹이 갖는다** — 이 컴포넌트는
    /// 모른다. 여기서 정하는 건 "어느 본에, 어떤 자세로" 뿐이다.
    ///
    /// 두 가지를 바로잡아 준다. 안 하면 프리팹 저작 좌표가 뜻을 잃는다:
    ///   - **자세**: 본의 로컬 축은 모델마다 제멋대로다. 그래서 이 오브젝트(캐릭터)의 자세를 따른다.
    ///   - **크기**: 본은 리그 스케일을 물고 있다(고양이 리그는 수십 배다). 그걸 상쇄해
    ///     프리팹을 **월드 단위**로 저작할 수 있게 한다.
    /// </summary>
    public class FxAttachToBone : MonoBehaviour
    {
        [SerializeField] private GameObject m_Prefab;

        /// <summary>붙일 본의 이름. 계층에서 이름으로 찾는다.</summary>
        [SerializeField] private string m_BoneName = "head";

        /// <summary>본에서 더 옮길 거리 — 이 오브젝트의 앞·위·옆 기준(월드 단위).</summary>
        [SerializeField] private Vector3 m_Offset = Vector3.zero;

        private void Start()
        {
            if (m_Prefab == null)
            {
                GameDebug.LogWarning($"[FxAttach] 프리팹이 없다: {name}");
                return;
            }

            Transform bone = FindBone(transform, m_BoneName);
            if (bone == null)
            {
                GameDebug.LogWarning($"[FxAttach] 본을 못 찾았다: {m_BoneName} ({name})");
                return;
            }

            GameObject fx = Instantiate(m_Prefab, bone);
            fx.name = m_Prefab.name;

            fx.transform.position = bone.position + transform.rotation * m_Offset;
            fx.transform.rotation = transform.rotation;

            Vector3 parent = bone.lossyScale;
            fx.transform.localScale = new Vector3(
                SafeInverse(parent.x),
                SafeInverse(parent.y),
                SafeInverse(parent.z));
        }

        private static float SafeInverse(float _v) => Mathf.Abs(_v) > 0.0001f ? 1f / _v : 1f;

        private static Transform FindBone(Transform _root, string _name)
        {
            if (_root.name == _name) return _root;

            for (int i = 0; i < _root.childCount; i++)
            {
                Transform found = FindBone(_root.GetChild(i), _name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
