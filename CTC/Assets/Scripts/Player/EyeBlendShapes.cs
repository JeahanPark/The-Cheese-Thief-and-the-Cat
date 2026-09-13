using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 캐릭터 메시의 눈 셰이프키에 가중치를 넣는다 — **그것만** 한다(규칙 7).
    ///
    /// **왜 얹은 구가 아니라 이것인가**(2026-09-13 실측): 팩 메시에는 셰이프키가 30개 있고
    /// 그중 eyes.lookIn/Out/Up/Down이 동공을 2D로 옮긴다. 「팩 메시는 눈동자를 못 움직인다」는
    /// <see cref="EyeLook"/>의 옛 전제는 틀렸다.
    ///
    /// **수평 키는 팩 원본이 아니라 좌우로 쪼갠 것을 쓴다**(<c>eyes.lookIn.R</c> 등).
    /// 원본은 두 눈을 거울 대칭으로 묶어 「옆 목표를 쫓는 시선」이 안 됐다. 쪼갠 키는
    /// <c>EyeSplitMeshBaker</c>가 파생 메시에 굽고, 프리팹이 그 메시를 문다.
    ///
    /// **LOD 렌더러 전부를 굴린다.** 팩 프리팹은 LODGroup에 `Mesh_LOD0..3` 넷이다 — 하나만 굴리면
    /// 멀어져서 LOD가 바뀌는 순간 **눈이 멈춘다.** 얹은 구 시절엔 구가 머리 본의 자식이라 모든 LOD에서
    /// 보였으므로, 셰이프키로 갈아탈 때 **이걸 놓치면 퇴행**이 된다(2026-09-13에 놓쳤다가 잡았다).
    /// LOD마다 메시가 달라 **인덱스도 렌더러마다 따로** 푼다.
    ///
    /// 이름→인덱스 조회는 **Awake에 한 번**만 한다 — 매 프레임 문자열로 찾으면 안 된다.
    /// 무엇을 넣을지는 <see cref="EyeBlendShapeLogic"/>이 정한다. 이 클래스는 배선만 안다.
    /// </summary>
    public class EyeBlendShapes : MonoBehaviour
    {
        private static readonly string[] SHAPE_NAMES =
        {
            "eyes.lookIn.R", "eyes.lookOut.R", "eyes.lookIn.L", "eyes.lookOut.L",
            "eyes.lookUp", "eyes.lookDown", "eyes.blink",
        };

        private const int IN_R = 0, OUT_R = 1, IN_L = 2, OUT_L = 3, UP = 4, DOWN = 5, BLINK = 6;

        /// <summary>굴릴 렌더러 하나와 그 메시에서 푼 인덱스 일곱.</summary>
        private struct Target
        {
            public SkinnedMeshRenderer Skin;
            public int[] Index;
        }

        private readonly List<Target> m_Targets = new List<Target>();
        private bool m_Ready;

        public bool Ready => m_Ready;

        /// <summary>QA 전용 — 실제로 굴리는 렌더러 수(LOD 넷이면 4여야 한다).</summary>
        public int DevRendererCount => m_Targets.Count;

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            m_Ready = false;
            m_Targets.Clear();

            SkinnedMeshRenderer[] skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer skin in skins)
            {
                if (skin == null || skin.sharedMesh == null) continue;

                var index = new int[SHAPE_NAMES.Length];
                string missing = null;
                for (int i = 0; i < SHAPE_NAMES.Length; i++)
                {
                    index[i] = skin.sharedMesh.GetBlendShapeIndex(SHAPE_NAMES[i]);
                    if (index[i] < 0) { missing = SHAPE_NAMES[i]; break; }
                }

                if (missing != null)
                {
                    // 눈 키가 아예 없는 렌더러(소품·총 등)는 조용히 넘기고, **눈 키가 있는데 쪼갠 키만 없으면**
                    // 파생 메시를 안 구운 것이라 알린다 — 그 LOD에서만 눈이 굳는 종류라 찾기 어렵다.
                    if (skin.sharedMesh.GetBlendShapeIndex("eyes.lookIn") >= 0)
                    {
                        Debug.LogWarning("[EyeBlendShapes] '" + skin.name + "'에 셰이프키가 없다: " + missing +
                                         " (메시 '" + skin.sharedMesh.name +
                                         "' — Tools/CTC/Dev/Bake Eye Split Meshes를 돌렸나?) 이 렌더러에서는 눈이 굳는다");
                    }
                    continue;
                }

                m_Targets.Add(new Target { Skin = skin, Index = index });
            }

            if (m_Targets.Count == 0)
            {
                Debug.LogWarning("[EyeBlendShapes] 눈 셰이프키를 가진 렌더러가 하나도 없다 — 눈 셰이프키를 끈다");
                return;
            }

            m_Ready = true;
        }

        public void Apply(in EyeShapeWeights _weights)
        {
            if (!m_Ready) return;
            for (int t = 0; t < m_Targets.Count; t++)
            {
                SkinnedMeshRenderer skin = m_Targets[t].Skin;
                if (skin == null) continue;
                int[] index = m_Targets[t].Index;
                skin.SetBlendShapeWeight(index[IN_R], _weights.InR);
                skin.SetBlendShapeWeight(index[OUT_R], _weights.OutR);
                skin.SetBlendShapeWeight(index[IN_L], _weights.InL);
                skin.SetBlendShapeWeight(index[OUT_L], _weights.OutL);
                skin.SetBlendShapeWeight(index[UP], _weights.Up);
                skin.SetBlendShapeWeight(index[DOWN], _weights.Down);
                skin.SetBlendShapeWeight(index[BLINK], _weights.Blink);
            }
        }

        /// <summary>눈이 돌아간 채로 남지 않게 한다.</summary>
        public void ClearWeights()
        {
            Apply(new EyeShapeWeights(0f, 0f, 0f, 0f, 0f, 0f, 0f));
        }

        /// <summary>
        /// QA 전용 — **렌더러에서 되읽는다.** 계산한 값을 돌려주면 배선이 끊겨도 QA가 통과한다
        /// (2026-08-09 볼륨 사고와 같은 부류). `_lod`로 어느 렌더러를 읽을지 고른다 —
        /// LOD1~3도 실제로 굴러가는지는 거기를 읽어야 안다.
        /// </summary>
        public EyeShapeWeights DevWeights(int _lod = 0)
        {
            if (!m_Ready || _lod < 0 || _lod >= m_Targets.Count || m_Targets[_lod].Skin == null)
            {
                return new EyeShapeWeights(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }
            SkinnedMeshRenderer skin = m_Targets[_lod].Skin;
            int[] index = m_Targets[_lod].Index;
            return new EyeShapeWeights(
                skin.GetBlendShapeWeight(index[IN_R]),
                skin.GetBlendShapeWeight(index[OUT_R]),
                skin.GetBlendShapeWeight(index[IN_L]),
                skin.GetBlendShapeWeight(index[OUT_L]),
                skin.GetBlendShapeWeight(index[UP]),
                skin.GetBlendShapeWeight(index[DOWN]),
                skin.GetBlendShapeWeight(index[BLINK]));
        }
    }
}
