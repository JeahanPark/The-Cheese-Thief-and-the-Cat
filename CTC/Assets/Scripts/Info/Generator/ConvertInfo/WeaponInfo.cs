// 이 파일은 CSV에서 자동 생성된다 — 직접 고치지 마라.
// 원본: Assets/Resources/CSVInfo/WeaponInfo.csv → 메뉴 Tools/CTC/Convert CSV → Info
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;

namespace CTC.Runtime.Info
{
    public partial class WeaponInfo
    {
        public int Tier;
        public int Damage;
        public float CooldownSeconds;
        public float Range;
        public string Note;
    }

    public partial class InfoManager
    {
        public List<WeaponInfo> WeaponInfoList = new List<WeaponInfo>();
    }
}
