using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 팩 메시의 눈 셰이프키에 가중치를 넣는다 — **그것만** 한다(규칙 7).
    ///
    /// **왜 얹은 구가 아니라 이것인가**(2026-09-13 실측): 팩 메시에는 셰이프키가 30개 있고
    /// 그중 eyes.lookIn/Out/Up/Down이 동공을 2D로 옮긴다. Mouse_LOD0~3·Cat_LOD0 전부
    /// 갖고 있고 importBlendShapes도 이미 켜져 있다. 「팩 메시는 눈동자를 못 움직인다」는
    /// <see cref="EyeLook"/>의 옛 전제는 틀렸다.
    ///
    /// 이름→인덱스 조회는 **Awake에 한 번**만 한다 — 매 프레임 문자열로 찾으면 안 된다.
    /// 무엇을 넣을지는 <see cref="EyeBlendShapeLogic"/>이 정한다. 이 클래스는 배선만 안다.
    /// </summary>
    public class EyeBlendShapes : MonoBehaviour
    {
        private static readonly string[] SHAPE_NAMES =
        {
            "eyes.lookIn", "eyes.lookOut", "eyes.lookUp", "eyes.lookDown", "eyes.blink"
        };

        private const int IN = 0, OUT = 1, UP = 2, DOWN = 3, BLINK = 4;

        private SkinnedMeshRenderer m_Skin;
        private readonly int[] m_Index = new int[SHAPE_NAMES.Length];
        private bool m_Ready;

        public bool Ready => m_Ready;

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            m_Ready = false;
            m_Skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (m_Skin == null || m_Skin.sharedMesh == null)
            {
                Debug.LogWarning("[EyeBlendShapes] 스킨드 메시를 못 찾았다 — 눈 셰이프키를 끈다");
                return;
            }

            for (int i = 0; i < SHAPE_NAMES.Length; i++)
            {
                m_Index[i] = m_Skin.sharedMesh.GetBlendShapeIndex(SHAPE_NAMES[i]);
                if (m_Index[i] >= 0) continue;

                // 팩을 갈아 끼웠을 때 **에러 없이 눈이 굳는 것**을 막는 유일한 장치다.
                Debug.LogWarning("[EyeBlendShapes] 셰이프키가 없다 — 눈 셰이프키를 끈다: " + SHAPE_NAMES[i]);
                return;
            }

            m_Ready = true;
        }

        public void Apply(in EyeShapeWeights _weights)
        {
            if (!m_Ready || m_Skin == null) return;
            m_Skin.SetBlendShapeWeight(m_Index[IN], _weights.LookIn);
            m_Skin.SetBlendShapeWeight(m_Index[OUT], _weights.LookOut);
            m_Skin.SetBlendShapeWeight(m_Index[UP], _weights.LookUp);
            m_Skin.SetBlendShapeWeight(m_Index[DOWN], _weights.LookDown);
            m_Skin.SetBlendShapeWeight(m_Index[BLINK], _weights.Blink);
        }

        /// <summary>모드를 되돌릴 때 눈이 돌아간 채로 남지 않게 한다.</summary>
        public void ClearWeights()
        {
            Apply(new EyeShapeWeights(0f, 0f, 0f, 0f, 0f));
        }

        /// <summary>
        /// QA 전용 — **렌더러에서 되읽는다.** 계산한 값을 돌려주면 배선이 끊겨도 QA가 통과한다
        /// (2026-08-09 볼륨 사고와 같은 부류).
        /// </summary>
        public EyeShapeWeights DevWeights()
        {
            if (!m_Ready || m_Skin == null) return new EyeShapeWeights(0f, 0f, 0f, 0f, 0f);
            return new EyeShapeWeights(
                m_Skin.GetBlendShapeWeight(m_Index[IN]),
                m_Skin.GetBlendShapeWeight(m_Index[OUT]),
                m_Skin.GetBlendShapeWeight(m_Index[UP]),
                m_Skin.GetBlendShapeWeight(m_Index[DOWN]),
                m_Skin.GetBlendShapeWeight(m_Index[BLINK]));
        }
    }
}


