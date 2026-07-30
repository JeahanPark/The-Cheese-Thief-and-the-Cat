// 이 파일은 CSV에서 자동 생성된다 — 직접 고치지 마라.
// 원본: Assets/Resources/CSVInfo/ShopInfo.csv → 메뉴 Tools/CTC/Convert CSV → Info
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;

namespace CTC.Runtime.Info
{
    public partial class ShopInfo
    {
        public ShopItemId ItemId;
        public RoleId Role;
        public int Cost;
    }

    public partial class InfoManager
    {
        public List<ShopInfo> ShopInfoList = new List<ShopInfo>();
    }
}
