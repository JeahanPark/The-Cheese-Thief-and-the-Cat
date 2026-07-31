using System.Collections.Generic;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비 역할 선택·시작 가능 판정 규칙. NetworkBehaviour와 분리된 순수 함수(EditMode TDD).
    /// </summary>
    public static class LobbyRoleLogic
    {
        /// <summary>selfIndex 참가자가 requested 역할을 고를 수 있는가. 고양이는 정원(catCapacity) 검사.</summary>
        public static bool CanSelect(IReadOnlyList<RoleId> _roles, int _selfIndex, RoleId _requested, int _catCapacity)
        {
            if (_requested == RoleId.None) return false;
            if (_selfIndex < 0 || _selfIndex >= _roles.Count) return false;
            if (_requested != RoleId.Cat) return true;

            int otherCats = 0;
            for (int i = 0; i < _roles.Count; i++)
            {
                if (i != _selfIndex && _roles[i] == RoleId.Cat) otherCats++;
            }
            return otherCats < _catCapacity;
        }

        /// <summary>전원 역할 선택 완료 + 고양이·쥐 각 1 이상이어야 시작 가능.</summary>
        public static bool CanStart(IReadOnlyList<RoleId> _roles)
        {
            if (_roles.Count < 2) return false;

            int cats = 0, mice = 0;
            for (int i = 0; i < _roles.Count; i++)
            {
                if (_roles[i] == RoleId.None) return false;
                if (_roles[i] == RoleId.Cat) cats++;
                else mice++;
            }
            return cats >= 1 && mice >= 1;
        }

        /// <summary>
        /// 맵 인덱스를 유효 범위로 접는다. 맵이 없으면 0 —
        /// 배선 누락으로 목록이 비어도 인덱싱이 터지지 않게 한다.
        /// </summary>
        public static int ClampMapIndex(int _index, int _mapCount)
        {
            if (_mapCount <= 0) return 0;
            if (_index < 0) return 0;
            return _index >= _mapCount ? _mapCount - 1 : _index;
        }
    }
}
