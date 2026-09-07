using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 창고 테크 수학 (테크 v3 계획 2a) — 값은 TechInfo.csv의 P열이 원본이고 여기엔 숫자를 박지 않는다.
    /// Warehouse: P1 = 동시 설치 상한 · P2 = 틱 수입 배율.
    /// HiddenWarehouse: P1 = 틱 소리 반경(m, Lv3은 0=무음) · P2 = 창고 최대 HP · P3 = 파괴 시 설치비 환급 비율.
    /// 순수 static — MonoBehaviour와 분리해 EditMode로 덮는다.
    ///
    /// 이름이 같은 <c>CTC.Runtime.Map.WarehouseLogic</c>(창고 맵 평면도)와는 남남이다 — 네임스페이스만 다르다.
    /// 한 파일에서 두 네임스페이스를 같이 using 하면 모호해지니, 그럴 땐 한쪽을 정식 이름으로 부른다.
    /// </summary>
    public static class WarehouseLogic
    {
        /// <summary>한 쥐가 동시에 세울 수 있는 창고 수. 미보유(0)여도 1 — 설치 자체는 서버가 따로 막는다.</summary>
        public static int LimitOf(int _warehouseLevel)
        {
            float p = TechLogic.ParamOf(TechId.Warehouse, _warehouseLevel, 1);
            return p > 0f ? Mathf.RoundToInt(p) : 1;
        }

        /// <summary>한 틱에 지급할 치즈. 배율이 0(미보유·표 누락)이면 기본 틱 그대로.</summary>
        public static int TickCheese(int _baseTick, int _warehouseLevel)
        {
            float mul = TechLogic.ParamOf(TechId.Warehouse, _warehouseLevel, 2);
            return mul > 0f ? Mathf.RoundToInt(_baseTick * mul) : _baseTick;
        }

        /// <summary>
        /// 틱 소리가 퍼지는 반경 — 숨은 창고 Lv3은 0(무음)이라 호출부가 재생을 아예 건너뛴다.
        /// 미보유(0)면 기본 반경. 0을 "표 누락"으로 오해하면 안 되므로 레벨로 먼저 가른다.
        /// </summary>
        public static float SoundRange(float _baseRange, int _hiddenLevel)
        {
            return _hiddenLevel <= 0 ? _baseRange : TechLogic.ParamOf(TechId.HiddenWarehouse, _hiddenLevel, 1);
        }

        /// <summary>창고 최대 HP — 숨은 창고가 없으면 프리팹/밸런스 기본값.</summary>
        public static int MaxHp(int _baseHp, int _hiddenLevel)
        {
            float p = TechLogic.ParamOf(TechId.HiddenWarehouse, _hiddenLevel, 2);
            return p > 0f ? Mathf.RoundToInt(p) : _baseHp;
        }

        /// <summary>부서졌을 때 소유 쥐에게 돌려줄 치즈 — Lv3만 설치비의 절반. 그 아래는 0.</summary>
        public static int Refund(int _placeCost, int _hiddenLevel)
        {
            return Mathf.RoundToInt(_placeCost * TechLogic.ParamOf(TechId.HiddenWarehouse, _hiddenLevel, 3));
        }
    }
}
