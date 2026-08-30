namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 빌드 버전을 NGO의 프로토콜 번호로 접는다 — 순수 함수(EditMode TDD).
    ///
    /// **왜 필요한가**: `NetworkConfig.ProtocolVersion`이 0으로 고정돼 있었다(2026-08-30 확인).
    /// 그러면 구버전 클라이언트가 신버전 호스트에 그냥 붙는다. 패치를 올리는 동안 두 버전이
    /// 섞이면 RPC 서명이 어긋나 **접속은 되는데 판이 이상하게 도는** 형태로 깨진다.
    /// 번호가 다르면 NGO가 붙기 전에 끊어 준다.
    ///
    /// 패치 자리(`0.1.**2**`)는 **안 본다.** 버그 수정만 올린 판까지 접속을 끊으면
    /// 유저가 업데이트를 받기 전에는 친구와 못 논다. 프로토콜이 바뀌는 건 기능이 바뀔 때다.
    /// </summary>
    public static class NetVersionLogic
    {
        /// <summary>버전 문자열을 못 읽을 때 쓰는 값. 0이 아니어야 "설정 안 함"과 구분된다.</summary>
        public const ushort FALLBACK = 1;

        /// <summary>major·minor만 쓴다 — `"1.4.7"` → `104`.</summary>
        public static ushort Protocol(string _version)
        {
            if (string.IsNullOrWhiteSpace(_version)) return FALLBACK;

            string[] parts = _version.Trim().Split('.');
            if (parts.Length == 0) return FALLBACK;

            if (!int.TryParse(parts[0], out int major)) return FALLBACK;

            int minor = 0;
            if (parts.Length > 1 && !int.TryParse(parts[1], out minor)) minor = 0;

            // 100은 minor 자릿수다. minor가 100을 넘을 일은 없고, 넘으면 major로 올려야 한다.
            int value = major * 100 + minor;
            if (value <= 0) return FALLBACK;

            return value > ushort.MaxValue ? ushort.MaxValue : (ushort)value;
        }
    }
}
