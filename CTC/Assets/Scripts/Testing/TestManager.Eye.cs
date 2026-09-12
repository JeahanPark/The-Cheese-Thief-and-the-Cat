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
        /// <summary>구 방식(0) ↔ 셰이프키 방식(1). A/B를 같은 카메라로 찍기 위한 것이다.</summary>
        public void TestSetEyeMode(int _useBlendShapes)
        {
            EyeLook[] all = FindObjectsByType<EyeLook>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (EyeLook eye in all)
            {
                eye.DevSetEyeMode(_useBlendShapes != 0);
            }
        }

        /// <summary>"in,out,up,down,blink" — 쉼표로 붙인 다섯 값. 셰이프키 모드가 아니면 빈 문자열.</summary>
        public string TestGetEyeShapeWeights()
        {
            EyeBlendShapes shapes = FindFirstObjectByType<EyeBlendShapes>();
            if (shapes == null) return string.Empty;

            EyeShapeWeights w = shapes.DevWeights();
            return string.Format("{0:0.##},{1:0.##},{2:0.##},{3:0.##},{4:0.##}",
                w.LookIn, w.LookOut, w.LookUp, w.LookDown, w.Blink);
        }
    }
}
#endif
