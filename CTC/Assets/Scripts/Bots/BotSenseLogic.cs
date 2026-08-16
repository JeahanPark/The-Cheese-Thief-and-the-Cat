using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 봇이 무엇을 알 수 있는가(순수). **이 게임에서 가장 조심해야 하는 코드다.**
    ///
    /// 서버는 전지적이다. 봇이 그냥 좌표를 읽으면 고양이 봇이 신이 되고,
    /// 그 판의 승률은 "소리 밸런스가 좋다"가 아니라 "봇이 반칙했다"를 뜻하게 된다.
    /// 여기를 지나지 않은 정보는 두뇌가 못 쓴다.
    /// </summary>
    public static class BotSenseLogic
    {
        /// <summary>
        /// 소리는 **좌표를 주지 않는다.** 사람은 3D 오디오로 "대략 저쪽, 좀 멀리"를 알 뿐이다.
        /// 정확한 자리를 주면 고양이가 소리 나는 즉시 직선으로 덮쳐 실제보다 훨씬 강해진다.
        ///
        /// 그래서 들린 자리를 흐린다. 흐리는 폭은 **거리에 비례**한다 — 코앞의 소리는 거의
        /// 정확하고, 먼 소리는 방향만 남는다. 사람의 청각과 같은 성질이다.
        /// </summary>
        public static Vector3 Blur(Vector3 _heard, Vector3 _listener, float _errorPerMeter, float _angleRad)
        {
            float distance = Flat(_heard - _listener).magnitude;
            float radius = Mathf.Max(0f, _errorPerMeter) * distance;
            var offset = new Vector3(Mathf.Cos(_angleRad), 0f, Mathf.Sin(_angleRad)) * radius;
            return _heard + offset;
        }

        /// <summary>그 소리가 내 귀에 닿는가 — 재생에 쓰인 반경을 그대로 쓴다.</summary>
        public static bool CanHear(Vector3 _listener, Vector3 _source, float _maxDistance)
        {
            return Flat(_source - _listener).sqrMagnitude <= _maxDistance * _maxDistance;
        }

        /// <summary>
        /// 눈에 들어오는가 — **카메라와 같은 각도**로 판단한다(가림 검사는 호출부의 레이캐스트가 한다).
        /// 시야각을 게임 카메라(고양이 70° · 쥐 55°)에서 가져오는 게 핵심이다.
        /// 봇이 못 보는 건 사람도 못 보는 것이어야 한다.
        /// </summary>
        public static bool IsInView(Vector3 _eye, Vector3 _forward, Vector3 _target, float _fovDeg)
        {
            Vector3 to = Flat(_target - _eye);
            if (to.sqrMagnitude < 0.0001f) return true;

            float half = Mathf.Clamp(_fovDeg, 1f, 179f) * 0.5f;
            return Vector3.Angle(Flat(_forward), to) <= half;
        }

        /// <summary>
        /// **그 소리를 낸 게 사냥감인가** (2026-08-12).
        ///
        /// 소리 기록에는 누가 냈는지가 없다. 그래서 고양이가 **제 덮치기 소리와 제 스킬 소리**를
        /// 듣고 "저기 뭔가 있다"며 제자리를 조사하러 갔다 — 고양이 스킬은 45m를 울려서 두 마리가
        /// 서로의 소리에 계속 붙들렸다(실측: 고양이 이동 1.3m/s, 최고속은 7.8이다).
        ///
        /// 사람은 제 발소리와 남의 발소리를 구분한다. 그 구분을 소리 종류로 대신한다 —
        /// 종류마다 낼 수 있는 쪽이 정해져 있기 때문이다.
        ///
        /// 강아지 소리는 양쪽 다 뺀다. 봇은 제3세력을 모델링하지 않는다(추적 대상이 아니다).
        /// </summary>
        public static bool IsPreySound(Audio.SfxId _id, Lobby.RoleId _prey)
        {
            return _prey == Lobby.RoleId.Mouse ? IsMouseMade(_id) : IsCatMade(_id);
        }

        // 쥐만 낼 수 있는 소리 — 쏘고, 캐고, 짓고, 부수고, 맞는다.
        private static bool IsMouseMade(Audio.SfxId _id)
        {
            switch (_id)
            {
                case Audio.SfxId.PopgunFire:
                case Audio.SfxId.SlingshotFire:
                case Audio.SfxId.MachinegunFire:
                case Audio.SfxId.MouseHurt:
                case Audio.SfxId.MouseDeath:
                case Audio.SfxId.MouseDash:
                case Audio.SfxId.FortressPlace:
                case Audio.SfxId.CheeseTick:
                case Audio.SfxId.WarehouseTick:
                case Audio.SfxId.HealTick:
                case Audio.SfxId.AlcoveHit:
                case Audio.SfxId.AlcoveBreak:
                case Audio.SfxId.BoxHit:
                case Audio.SfxId.BoxBreak:
                    return true;
                default:
                    return false;
            }
        }

        // 고양이만 낼 수 있는 소리 — 휘두르고, 맞고, 쥐 구조물을 부순다.
        private static bool IsCatMade(Audio.SfxId _id)
        {
            switch (_id)
            {
                case Audio.SfxId.PounceSwing:
                case Audio.SfxId.CatHurt:
                case Audio.SfxId.CatDeath:
                case Audio.SfxId.FortressHit:
                case Audio.SfxId.FortressBreak:
                    return true;
                default:
                    return false;
            }
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
