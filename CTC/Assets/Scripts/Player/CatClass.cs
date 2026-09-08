using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 고양이가 무엇으로 전직했는가 (v3.9). **상태만 갖는다** — 고르는 UI는 `UI/CatClassPopup`,
    /// 스킬을 쓰는 것은 `CatSkill`이다(기능 1개 = 컴포넌트 1개).
    ///
    /// 전직은 **누적 획득 장난기**가 문턱(CSV `CatAdvanceMischief`)을 넘으면 열린다 —
    /// 무엇을 샀는지는 묻지 않는다. 옛 조건("강화 둘 다 만렙")은 정해진 두 테크를 사라는 강제였다.
    ///
    /// 값은 전원이 읽는다 — 쥐도 상대가 무엇으로 전직했는지 알아야 대응할 수 있다.
    /// </summary>
    public class CatClass : NetworkBehaviour
    {
        private readonly NetworkVariable<CatClassId> m_Class = new NetworkVariable<CatClassId>(CatClassId.None);

        public CatClassId Value => m_Class.Value;

        /// <summary>전직 상태 스트림(MVC의 Model 노출) — 구독 즉시 현재 값이 흐른다.</summary>
        public Observable<CatClassId> ClassChanged => m_Class.AsObservableWithCurrent();

        /// <summary>
        /// 지금 전직할 수 있는가 — UI가 이걸 보고 창을 띄운다. 보는 것은 **지갑의 누적 획득** 하나다
        /// (강화 스택을 더는 보지 않는다 — 2026-09-09 계획 3 Task 5).
        /// </summary>
        public bool CanAdvance
        {
            get
            {
                var wallet = GetComponent<CheeseWallet>();
                if (wallet == null) return false;

                return CatClassLogic.CanAdvance(
                    wallet.EarnedValue, CatClassLogic.MischiefThreshold, m_Class.Value);
            }
        }

        /// <summary>소유자가 고른다. 서버가 조건을 다시 본다 — 클라 주장만으로 전직하지 않는다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void ChooseRpc(CatClassId _id)
        {
            if (!CatClassLogic.IsRealClass(_id)) return;
            if (!CanAdvance) return;

            m_Class.Value = _id;
            AdvanceFxRpc();
        }

        /// <summary>QA 전용 — 조건을 건너뛰고 전직시킨다(서버).</summary>
        public void DevSetClass(CatClassId _id)
        {
            if (!IsServer) return;
            m_Class.Value = _id;
        }

        // 전직은 판의 전환점이라 소리로 알린다 — 이 게임에서 큰 사건에는 항상 소리가 붙는다.
        [Rpc(SendTo.Everyone)]
        private void AdvanceFxRpc()
        {
            Audio.SfxPlayer.PlayUi(Audio.SfxId.CatAdvance);
        }
    }
}
