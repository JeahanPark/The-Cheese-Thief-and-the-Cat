using R3;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Audio
{
    /// <summary>uGUI Button 클릭음(2D) — 씬 생성기가 모든 버튼에 부착. 연결은 R3(CTC 규칙 2).</summary>
    [RequireComponent(typeof(Button))]
    public class UiClickSfx : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().OnClickAsObservable().Subscribe(_ => OnClickPlaySfx()).AddTo(this);
        }

        private static void OnClickPlaySfx() => SfxPlayer.PlayUi(SfxId.UiClick);
    }
}
