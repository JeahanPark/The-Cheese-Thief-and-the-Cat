using System.Collections.Generic;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비 역할 선택·시작 가능 판정 규칙. NetworkBehaviour와 분리된 순수 함수(EditMode TDD).
    /// </summary>
    public static class LobbyRoleLogic
    {
        /// <summary>selfIndex 참가자가 requested 역할을 고를 수 있는가. 고양이는 정원(catCapacity) 검사.</summary>
        public static bool CanSelect(IReadOnlyList<RoleId> roles, int selfIndex, RoleId requested, int catCapacity)
        {
            if (requested == RoleId.None) return false;
            if (selfIndex < 0 || selfIndex >= roles.Count) return false;
            if (requested != RoleId.Cat) return true;

            int otherCats = 0;
            for (int i = 0; i < roles.Count; i++)
            {
                if (i != selfIndex && roles[i] == RoleId.Cat) otherCats++;
            }
            return otherCats < catCapacity;
        }

        /// <summary>전원 역할 선택 완료 + 고양이·쥐 각 1 이상이어야 시작 가능.</summary>
        public static bool CanStart(IReadOnlyList<RoleId> roles)
        {
            if (roles.Count < 2) return false;

            int cats = 0, mice = 0;
            for (int i = 0; i < roles.Count; i++)
            {
                if (roles[i] == RoleId.None) return false;
                if (roles[i] == RoleId.Cat) cats++;
                else mice++;
            }
            return cats >= 1 && mice >= 1;
        }
    }
}
