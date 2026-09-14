using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 깡패 쥐의 쉬는 얼굴 — **살짝 화난 눈**(2026-09-14). <see cref="GangLook"/>이 켜고 끈다.
    ///
    /// 팩에 "Angry" 표정이 없다. <c>Eyes_Rabid</c>(광분)를 100으로 틀면 너무 세서, 쉬는 동안만
    /// 셰이프키를 **섞어** 쓴다: <c>eyes.rabid</c> <see cref="RABID_WEIGHT"/> + 기본 <c>eyes.annoyed</c> 나머지.
    ///
    /// 애니메이터(Shapekey 레이어)가 매 프레임 <c>eyes.annoyed</c>=100을 쓰므로 **LateUpdate에서 덮는다**.
    /// 원샷 표정(맞음·광분 등)이 도는 동안은 손대지 않는다 — 그 표정이 이긴다.
    /// 렌더러는 LOD 전부를 굴린다(CLAUDE.md 함정 19 — 첫 렌더러만 굴리면 멀어졌을 때 얼굴이 돌아간다).
    /// </summary>
    public class GangAngryFace : MonoBehaviour
    {
        public const float RABID_WEIGHT = 55f;
        private const string RABID = "eyes.rabid";
        private const string ANNOYED = "eyes.annoyed";

        private readonly List<(SkinnedMeshRenderer skin, int rabid, int annoyed)> m_Targets =
            new List<(SkinnedMeshRenderer, int, int)>();
        private FaceExpression m_Expression;

        private void Awake()
        {
            m_Expression = GetComponent<FaceExpression>();
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                int rabid = skin.sharedMesh.GetBlendShapeIndex(RABID);
                int annoyed = skin.sharedMesh.GetBlendShapeIndex(ANNOYED);
                if (rabid >= 0 && annoyed >= 0) m_Targets.Add((skin, rabid, annoyed));
            }
            enabled = false;   // GangLook이 켠다
        }

        private void OnDisable()
        {
            // 끄는 순간 섞은 값을 걷는다 — 다음 프레임 애니메이터가 annoyed를 다시 쓰지만 rabid는 아무도 안 지운다.
            foreach (var t in m_Targets)
            {
                if (t.skin != null) t.skin.SetBlendShapeWeight(t.rabid, 0f);
            }
        }

        private void LateUpdate()
        {
            if (m_Expression != null && m_Expression.DevRequestedExpression != FaceExpressionLogic.DEFAULT) return;

            foreach (var t in m_Targets)
            {
                if (t.skin == null) continue;
                t.skin.SetBlendShapeWeight(t.rabid, RABID_WEIGHT);
                t.skin.SetBlendShapeWeight(t.annoyed, 100f - RABID_WEIGHT);
            }
        }
    }
}
