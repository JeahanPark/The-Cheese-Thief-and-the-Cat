// TestManager의 일부 — 눈 셰이프키 QA 훅(2026-09-13).
//
// **렌더러에 실제로 들어간 값을 읽는다.** 계산한 값을 돌려주면 배선이 끊겨도 QA가 통과한다
// (2026-08-09 「볼륨은 바뀌는데 소리는 그대로」와 같은 부류).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Player;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        // TestSetEyeMode는 2026-09-13에 지웠다 — 구 방식과 셰이프키 방식을 오가는 A/B 손잡이였고,
        // 검수가 끝나 셰이프키로 정해지면서 **바꿀 상대가 없어졌다**.

        /// <summary>"in,out,up,down,blink" — 쉼표로 붙인 다섯 값. 셰이프키가 없으면 빈 문자열.</summary>
        public string TestGetEyeShapeWeights()
        {
            // FindFirstObjectByType는 6000.5에서 폐기 경고가 난다 — 순서가 필요 없으니 Any로 찾는다.
            EyeBlendShapes shapes = FindAnyObjectByType<EyeBlendShapes>();
            if (shapes == null) return string.Empty;

            EyeShapeWeights w = shapes.DevWeights();
            return string.Format("{0:0.##},{1:0.##},{2:0.##},{3:0.##},{4:0.##}",
                w.LookIn, w.LookOut, w.LookUp, w.LookDown, w.Blink);
        }
    }
}
#endif
