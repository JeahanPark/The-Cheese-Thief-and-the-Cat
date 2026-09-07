// 이 파일은 CSV에서 자동 생성된다 — 직접 고치지 마라.
// 원본: Assets/Resources/CSVInfo/TechInfo.csv → 메뉴 Tools/CTC/Convert CSV → Info
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;

namespace CTC.Runtime.Info
{
    public partial class TechInfo
    {
        public TechId TechId;
        public RoleId Role;
        public TechAxis Axis;
        public int Level;
        public int Cost;
        public string Label;
        public string Hint;
        public float P1;
        public float P2;
        public float P3;
    }

    public partial class InfoManager
    {
        public List<TechInfo> TechInfoList = new List<TechInfo>();
    }
}
