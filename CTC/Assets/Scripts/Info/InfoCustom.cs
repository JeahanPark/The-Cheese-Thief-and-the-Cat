using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Info
{
    /// <summary>
    /// 자동 생성된 테이블에 손으로 붙이는 조회 헬퍼 (WES `InfoCustom` 대응).
    /// 생성기가 덮어쓰지 않는 파일이라 여기에만 사람이 쓴다.
    ///
    /// 표에 행이 없을 때 **조용히 0을 돌려주지 않는다** — 밸런스가 0이 된 채로 게임이 돌면
    /// 원인을 찾는 데 몇 시간이 든다. 로그를 남기고 빈 인스턴스를 준다.
    /// </summary>
    public partial class InfoManager
    {
        /// <summary>단일 행 표 — 전역 밸런스 수치.</summary>
        public BalanceInfo Balance
        {
            get
            {
                if (BalanceInfoList.Count > 0) return BalanceInfoList[0];
                Debug.LogError("[InfoManager] BalanceInfo.csv 행 없음 — 밸런스가 전부 0이 된다");
                return new BalanceInfo();
            }
        }

        public PlayerInfo PlayerOf(RoleId _role)
        {
            foreach (PlayerInfo info in PlayerInfoList)
            {
                if (info.Role == _role) return info;
            }
            Debug.LogError($"[InfoManager] PlayerInfo.csv에 {_role} 행 없음");
            return new PlayerInfo();
        }

        public WeaponInfo WeaponOf(int _tier)
        {
            int clamped = Mathf.Clamp(_tier, 0, WeaponInfoList.Count - 1);
            foreach (WeaponInfo info in WeaponInfoList)
            {
                if (info.Tier == clamped) return info;
            }
            Debug.LogError($"[InfoManager] WeaponInfo.csv에 티어 {_tier} 행 없음");
            return new WeaponInfo();
        }

        public IReadOnlyList<ShopInfo> Shop => ShopInfoList;
    }
}
