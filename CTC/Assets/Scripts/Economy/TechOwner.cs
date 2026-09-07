using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 효과 훅이 "이 플레이어가 그 테크를 몇 레벨 가졌나"를 묻는 한 가지 길 (테크 v3 계획 2a).
    /// 서버·클라 어디서든 TechSlots(NetworkList)를 읽으므로 표시용으로도 같은 값이다.
    /// </summary>
    public static class TechOwner
    {
        public static int LevelOf(GameObject _player, TechId _id)
        {
            if (_player == null) return 0;
            TechSlots slots = _player.GetComponent<TechSlots>();
            return slots != null ? slots.LevelOf(_id) : 0;
        }

        public static int LevelOf(ulong _clientId, TechId _id)
        {
            NetworkObject no = Core.NetUtil.FindPlayerObject(_clientId);
            return no != null ? LevelOf(no.gameObject, _id) : 0;
        }

        /// <summary>레벨의 파라미터 — 미보유면 0.</summary>
        public static float Param(GameObject _player, TechId _id, int _index)
        {
            return TechLogic.ParamOf(_id, LevelOf(_player, _id), _index);
        }
    }
}
