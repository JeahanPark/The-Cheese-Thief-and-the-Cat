using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Audio
{
    /// <summary>uGUI Button 클릭음(2D) — 씬 생성기가 모든 버튼에 부착.</summary>
    [RequireComponent(typeof(Button))]
    public class UiClickSfx : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => SfxPlayer.PlayUi(SfxId.UiClick));
        }
    }
}
