using System;
using System.Threading;
using CTC.Runtime.Core;
using CTC.Runtime.Networking;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace CTC.Runtime.Intro
{
    /// <summary>
    /// 인트로 부팅 체크를 돌리는 Worker. 화면은 아무것도 모르고 이 스트림만 구독한다(CTC 규칙 3·5·6).
    ///
    /// 한 항목이 끝날 때마다 <see cref="Changed"/>가 울린다. 값은 <see cref="StateOf"/>로 읽는다 —
    /// 항목이 4개뿐이라 항목별 스트림을 4개 만드는 것보다 이쪽이 배선이 적다.
    ///
    /// 스팀 연결이 실패하면 거기서 멈춘다. 릴레이는 그 뒤라 자동으로 Skipped가 되고,
    /// 화면은 "다시 시도"를 띄운다 — <see cref="RetryAsync"/>가 그 버튼의 목적지다.
    /// </summary>
    public class IntroDirector : MonoBehaviour
    {
        /// <summary>각 항목을 최소 이만큼은 보여 준다 — 너무 빨리 지나가면 뭘 확인했는지 안 보인다.</summary>
        private const int STEP_MIN_MILLIS = 260;

        /// <summary>릴레이 예열이 붙기를 기다리는 시간. 넘어가도 게임은 진행한다.</summary>
        private const int RELAY_WARMUP_MILLIS = 900;

        private readonly IntroStepState[] m_States = new IntroStepState[IntroBootLogic.STEPS.Length];
        private readonly string[] m_Notes = new string[IntroBootLogic.STEPS.Length];

        private readonly Subject<Unit> m_Changed = new Subject<Unit>();
        private CancellationTokenSource m_Cts;

        /// <summary>항목 하나가 바뀔 때마다 울린다.</summary>
        public Observable<Unit> Changed => m_Changed;

        public int StepCount => m_States.Length;

        public IntroStepState StateOf(int _index) =>
            _index >= 0 && _index < m_States.Length ? m_States[_index] : IntroStepState.Waiting;

        /// <summary>항목 옆에 붙는 한 줄(계정 이름·실패 사유 등). 없으면 빈 문자열.</summary>
        public string NoteOf(int _index) =>
            _index >= 0 && _index < m_Notes.Length && m_Notes[_index] != null ? m_Notes[_index] : "";

        public System.Collections.Generic.IReadOnlyList<IntroStepState> States => m_States;

        public bool CanStart => IntroBootLogic.CanStart(m_States);
        public bool AllSettled => IntroBootLogic.AllSettled(m_States);
        public bool HasFatalFailure => IntroBootLogic.HasFatalFailure(m_States);

        /// <summary>막는 실패의 사유 한 줄 — 없으면 빈 문자열.</summary>
        public string FatalReason
        {
            get
            {
                int i = IntroBootLogic.FirstFatalIndex(m_States);
                return i < 0 ? "" : NoteOf(i);
            }
        }

        private void Start()
        {
            m_Cts = new CancellationTokenSource();
            RunAsync(m_Cts.Token).Forget();
        }

        private void OnDestroy()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
            m_Changed.Dispose();
        }

        /// <summary>"다시 시도" — 실패한 지점부터 다시 돈다.</summary>
        public void Retry()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = new CancellationTokenSource();

            for (int i = 0; i < m_States.Length; i++)
            {
                m_States[i] = IntroStepState.Waiting;
                m_Notes[i] = "";
            }
            m_Changed.OnNext(Unit.Default);
            RunAsync(m_Cts.Token).Forget();
        }

        private async UniTask RunAsync(CancellationToken _token)
        {
            for (int i = 0; i < IntroBootLogic.STEPS.Length; i++)
            {
                if (_token.IsCancellationRequested) return;

                Set(i, IntroStepState.Busy, "확인 중…");
                await UniTask.Delay(STEP_MIN_MILLIS, cancellationToken: _token);
                if (_token.IsCancellationRequested) return;

                bool ok = await RunStepAsync(IntroBootLogic.STEPS[i], i, _token);
                if (_token.IsCancellationRequested) return;

                // 막는 항목이 죽으면 뒤는 볼 필요가 없다 — 실패로 세지 않고 건너뛴다.
                if (!ok && IntroBootLogic.IsFatal(IntroBootLogic.STEPS[i]))
                {
                    for (int rest = i + 1; rest < m_States.Length; rest++)
                    {
                        Set(rest, IntroStepState.Skipped, "건너뜀");
                    }
                    return;
                }
            }
        }

        private async UniTask<bool> RunStepAsync(IntroStepId _step, int _index, CancellationToken _token)
        {
            switch (_step)
            {
                case IntroStepId.GameData: return CheckGameData(_index);
                case IntroStepId.UserSettings: return CheckUserSettings(_index);
                case IntroStepId.SteamConnect: return CheckSteamConnect(_index);
                case IntroStepId.SteamRelay: return await CheckSteamRelayAsync(_index, _token);
                default: return true;
            }
        }

        private bool CheckGameData(int _index)
        {
            try
            {
                var info = Info.InfoManager.Instance;
                bool ok = info != null && info.Shop != null && info.Shop.Count > 0;
                Set(_index, ok ? IntroStepState.Done : IntroStepState.Failed,
                    ok ? "표 4종 불러옴" : "CSV를 못 읽었다");
                return ok;
            }
            catch (Exception e)
            {
                GameDebug.LogError($"[Intro] 게임 데이터 실패: {e.Message}");
                Set(_index, IntroStepState.Failed, "CSV를 못 읽었다");
                return false;
            }
        }

        private bool CheckUserSettings(int _index)
        {
            try
            {
                // 스트림을 한 번 읽어 PlayerPrefs 로드를 강제한다 — 여기서 터지면 기본값으로 계속한다.
                float sens = UserSettings.MouseSensitivity.Value;
                Set(_index, IntroStepState.Done, $"감도 {sens:0.00}");
                return true;
            }
            catch (Exception e)
            {
                GameDebug.LogWarning($"[Intro] 개인 설정 실패(기본값으로 계속): {e.Message}");
                Set(_index, IntroStepState.Failed, "기본값으로 계속");
                return false;
            }
        }

        /// <summary>
        /// 에디터에는 스팀이 없는 게 정상이라 **검사하지 않는다** — 검사하면 개발 중에 인트로를
        /// 영영 통과할 수 없다(실측: "게임 시작"이 계속 잠겨 있었다).
        /// 개발 빌드도 막지 않는다 — 스팀 없이 로컬 직결로 도는 테스트 경로가 있다.
        /// 배포 빌드만 막는데, 거기서는 스팀이 실행 주체라 사실상 일어나지 않는 경우다.
        /// </summary>
        private bool CheckSteamConnect(int _index)
        {
#if UNITY_EDITOR
            Set(_index, IntroStepState.Skipped, "에디터 — 검사 안 함");
            return true;
#else
            bool ok = SteamBootstrap.TryInit();
            if (!ok)
            {
#if DEVELOPMENT_BUILD
                Set(_index, IntroStepState.Skipped, "스팀 없음 — 개발 빌드라 계속");
                return true;
#else
                Set(_index, IntroStepState.Failed, "스팀이 실행되지 않음");
                return false;
#endif
            }

            string who = "";
            try
            {
                who = Steamworks.SteamClient.Name;
            }
            catch (Exception)
            {
                who = "";
            }

            Set(_index, IntroStepState.Done, string.IsNullOrEmpty(who) ? "연결됨" : who);
            return true;
#endif
        }

        /// <summary>
        /// 릴레이는 예열만 한다. 붙었는지까지는 안 본다 — 여기서 막으면 스팀이 느린 날
        /// 게임을 못 켜는데, 릴레이는 실제로 접속할 때 다시 확인되기 때문이다.
        /// </summary>
        private async UniTask<bool> CheckSteamRelayAsync(int _index, CancellationToken _token)
        {
#if UNITY_EDITOR
            // 스팀을 안 켠 에디터에서는 예열할 것도 없다.
            await UniTask.Yield(_token);
            Set(_index, IntroStepState.Skipped, "에디터 — 검사 안 함");
            return true;
#else
            try
            {
                Steamworks.SteamNetworkingUtils.InitRelayNetworkAccess();
                await UniTask.Delay(RELAY_WARMUP_MILLIS, cancellationToken: _token);
                Set(_index, IntroStepState.Done, "예열 완료");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                GameDebug.LogWarning($"[Intro] 릴레이 예열 실패(계속 진행): {e.Message}");
                Set(_index, IntroStepState.Failed, "예열 실패 — 접속이 느릴 수 있다");
                return false;
            }
#endif
        }

        private void Set(int _index, IntroStepState _state, string _note)
        {
            if (_index < 0 || _index >= m_States.Length) return;
            m_States[_index] = _state;
            m_Notes[_index] = _note;
            m_Changed.OnNext(Unit.Default);
        }
    }
}
