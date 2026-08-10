// 이 파일은 CSV에서 자동 생성된다 — 직접 고치지 마라.
// 원본: Assets/Resources/CSVInfo/PlayerInfo.csv → 메뉴 Tools/CTC/Convert CSV → Info
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;

namespace CTC.Runtime.Info
{
    public partial class PlayerInfo
    {
        public RoleId Role;
        public int MaxHp;
        public float MoveSpeed;
        public float RunThreshold;
        public float TurnSpeedDegPerSec;
        public float Fov;
        public float CamDistance;
        public float CcHeight;
        public float CcRadius;
        public int PounceDamage;
        public float PounceCooldown;
        public float PounceRange;
        public float PounceHalfAngleDeg;
        public float SneakSpeedMultiplier;
        public float FootstepRangeMeters;
        public float SneakFootstepRangeMeters;
    }

    public partial class InfoManager
    {
        public List<PlayerInfo> PlayerInfoList = new List<PlayerInfo>();
    }
}
