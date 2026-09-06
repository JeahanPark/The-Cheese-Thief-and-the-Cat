namespace CTC.Runtime.Player
{
    /// <summary>
    /// "이 몸을 누가 움직이나"의 순수 판정(EditMode TDD, 2026-09-06).
    /// NetworkBehaviour를 모르는 bool 셋만 받는다 — <see cref="LocalControl"/>이 그걸 읽어 넘긴다.
    /// </summary>
    public static class LocalControlLogic
    {
        /// <summary>로컬 사람이 조종하는 몸 — 입력(키보드·마우스)을 읽어도 되는 유일한 경우.</summary>
        public static bool Mine(bool _isOwner, bool _isPlayerObject) => _isOwner && _isPlayerObject;

        /// <summary>
        /// 이 프로세스가 몸을 **움직여야** 하는가 — 내 몸이거나, 서버가 조종하는 봇.
        /// 봇은 플레이어 오브젝트가 아닌 채 서버 소유로 선다. 입력은 안 읽지만 이동·중력은 돌아야 한다.
        /// 2026-09-02에 이동 게이트를 <see cref="Mine"/>으로만 두자 봇이 한 발도 못 뗐다.
        /// </summary>
        public static bool Drives(bool _isOwner, bool _isPlayerObject, bool _isServer)
        {
            return Mine(_isOwner, _isPlayerObject) || (_isServer && !_isPlayerObject);
        }
    }
}
