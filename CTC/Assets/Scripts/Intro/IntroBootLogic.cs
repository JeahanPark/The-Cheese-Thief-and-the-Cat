using System.Collections.Generic;

namespace CTC.Runtime.Intro
{
    /// <summary>인트로에서 확인하는 항목. 순서가 곧 화면에 뜨는 순서다.</summary>
    public enum IntroStepId
    {
        GameData = 0,
        UserSettings = 1,
        SteamConnect = 2,
        SteamRelay = 3
    }

    public enum IntroStepState
    {
        Waiting = 0,
        Busy = 1,
        Done = 2,
        Failed = 3,

        /// <summary>앞 단계가 죽어 볼 필요가 없어진 항목 — 실패로 세지 않는다.</summary>
        Skipped = 4
    }

    /// <summary>
    /// 인트로 부팅 체크의 판정 규칙. MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **어떤 실패가 게임을 막는가**가 이 파일의 전부다:
    ///   - 게임 데이터(CSV)가 없으면 밸런스가 없다 → 못 간다.
    ///   - 스팀 연결이 없으면 남과 못 논다 → 못 간다(재시도).
    ///   - 개인 설정은 없으면 기본값으로 계속한다.
    ///   - 릴레이는 경고만 — 혼자 놀거나 로컬로는 갈 수 있다.
    /// </summary>
    public static class IntroBootLogic
    {
        public static readonly IntroStepId[] STEPS =
        {
            IntroStepId.GameData,
            IntroStepId.UserSettings,
            IntroStepId.SteamConnect,
            IntroStepId.SteamRelay
        };

        /// <summary>이 항목이 실패하면 로비로 넘어갈 수 없다.</summary>
        public static bool IsFatal(IntroStepId _step)
        {
            return _step == IntroStepId.GameData || _step == IntroStepId.SteamConnect;
        }

        public static int DoneCount(IReadOnlyList<IntroStepState> _states)
        {
            int done = 0;
            for (int i = 0; i < _states.Count; i++)
            {
                if (_states[i] == IntroStepState.Done) done++;
            }
            return done;
        }

        /// <summary>진행바 값 0~1. 항목이 없으면 0이다 — 배선 누락으로 나누기가 터지지 않게.</summary>
        public static float Progress(IReadOnlyList<IntroStepState> _states)
        {
            if (_states.Count <= 0) return 0f;
            return (float)DoneCount(_states) / _states.Count;
        }

        /// <summary>더 기다릴 항목이 없는가 — Waiting·Busy가 하나도 없으면 끝난 것이다.</summary>
        public static bool AllSettled(IReadOnlyList<IntroStepState> _states)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                if (_states[i] == IntroStepState.Waiting || _states[i] == IntroStepState.Busy) return false;
            }
            return true;
        }

        /// <summary>막는 실패가 하나라도 있는가. 인덱스는 STEPS 순서를 따른다.</summary>
        public static bool HasFatalFailure(IReadOnlyList<IntroStepState> _states)
        {
            int count = _states.Count < STEPS.Length ? _states.Count : STEPS.Length;
            for (int i = 0; i < count; i++)
            {
                if (_states[i] == IntroStepState.Failed && IsFatal(STEPS[i])) return true;
            }
            return false;
        }

        /// <summary>"시작하기"를 눌러도 되는 상태인가.</summary>
        public static bool CanStart(IReadOnlyList<IntroStepState> _states)
        {
            return AllSettled(_states) && !HasFatalFailure(_states);
        }

        /// <summary>막는 실패 중 첫 번째 — 화면에 사유를 보여줄 때 쓴다. 없으면 -1.</summary>
        public static int FirstFatalIndex(IReadOnlyList<IntroStepState> _states)
        {
            int count = _states.Count < STEPS.Length ? _states.Count : STEPS.Length;
            for (int i = 0; i < count; i++)
            {
                if (_states[i] == IntroStepState.Failed && IsFatal(STEPS[i])) return i;
            }
            return -1;
        }

        public static string LabelOf(IntroStepId _step)
        {
            switch (_step)
            {
                case IntroStepId.GameData: return "게임 데이터";
                case IntroStepId.UserSettings: return "개인 설정";
                case IntroStepId.SteamConnect: return "스팀 연결";
                case IntroStepId.SteamRelay: return "스팀 릴레이";
                default: return "";
            }
        }
    }
}
