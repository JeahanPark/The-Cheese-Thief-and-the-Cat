using CTC.Runtime.Core;
using CTC.Runtime.Combat;
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
    /// 전직은 상점 강화를 **둘 다 끝까지** 올려야 열린다. 하나만 올린 채로 열리면
    /// "전직이 곧 최적 구매"가 되어 상점의 선택이 사라진다.
    ///
    /// 값은 전원이 읽는다 — 쥐도 상대가 무엇으로 전직했는지 알아야 대응할 수 있다.
    /// </summary>
    public class CatClass : NetworkBehaviour
    {
        private readonly NetworkVariable<CatClassId> m_Class = new NetworkVariable<CatClassId>(CatClassId.None);

        public CatClassId Value => m_Class.Value;

        /// <summary>전직 상태 스트림(MVC의 Model 노출) — 구독 즉시 현재 값이 흐른다.</summary>
        public Observable<CatClassId> ClassChanged => m_Class.AsObservableWithCurrent();

        /// <summary>지금 전직할 수 있는가 — UI가 이걸 보고 창을 띄운다.</summary>
        public bool CanAdvance
        {
            get
            {
                var pounce = GetComponent<CatPounce>();
                var move = GetComponent<PlayerMovement>();
                if (pounce == null || move == null) return false;

                return CatClassLogic.CanAdvance(
                    pounce.DamageStacks, move.SpeedStacks, ShopLogic.MAX_UPGRADE_STACKS, m_Class.Value);
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
