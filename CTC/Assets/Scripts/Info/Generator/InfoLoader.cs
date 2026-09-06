// 이 파일은 CSV에서 자동 생성된다 — 직접 고치지 마라.
// 메뉴 Tools/CTC/Convert CSV → Info 가 Assets/Resources/CSVInfo/*.csv 전부를 훑어 다시 만든다.

namespace CTC.Runtime.Info
{
    public partial class InfoManager
    {
        public void LoadAllInfo()
        {
            BalanceInfoList = LoadInfo<BalanceInfo>();
            PlayerInfoList = LoadInfo<PlayerInfo>();
            ShopInfoList = LoadInfo<ShopInfo>();
            TechInfoList = LoadInfo<TechInfo>();
            WeaponInfoList = LoadInfo<WeaponInfo>();
        }
    }
}
