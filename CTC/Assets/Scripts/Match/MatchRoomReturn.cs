using CTC.Runtime.Core;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// **단판제**(2026-09-15, 사용자 결정 — 3판 시리즈를 걷었다). `LobbyController` GO에 동승한다.
    ///
    /// 판이 끝나면 결과를 잠깐 보여 주고 **대기실로 돌아간다.** 다음 판은 대기실에서 다시 시작한다 —
    /// 고양이는 대기실이 매번 새로 뽑으므로(직전 고양이는 `MatchHandoff.PreviousCat`으로 빠진다)
    /// 여기서 판을 넘기며 상태를 이어 붙일 일이 없다. 시리즈가 겪던 판 넘김 버그(봇 증발·단계 잔존)가
    /// 구조째로 사라진다. 방 설정(봇 수·맵)은 `RoomReturnMemo`가 대기실로 되가져간다(2026-09-19).
    ///
    /// **대기실에서 온 판에서만 돈다.** `Game.unity`를 혼자 열었을 때(wesqa·MPPM)는
    /// 아무 일도 안 한다 — 그쪽은 지금까지처럼 사람이 재시작을 누르는 흐름이다.
    /// </summary>
    public class MatchRoomReturn : NetworkBehaviour
    {
        /// <summary>판이 끝나고 대기실로 돌아가기 전에 결과를 보여 주는 시간(초).</summary>
        private const float RESULT_SECONDS = 7f;

        [SerializeField] private MatchManager m_Match;
        [SerializeField] private string m_RoomSceneName = "Room";

        /// <summary>이 판이 대기실에서 왔는가 — 결과 패널이 재시작 버튼을 숨기는 근거다.</summary>
        private readonly NetworkVariable<bool> m_Active = new NetworkVariable<bool>();

        private bool m_Handling;

        public bool Active => m_Active.Value;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // 대기실에서 온 판만 돌려보낸다. 쪽지가 비어 있으면(단독 실행) 손대지 않는다.
            m_Active.Value = MatchHandoff.FromRoom;
            if (!m_Active.Value || m_Match == null) return;

            m_Match.ResultChanged
                .Where(r => r != MatchResult.None)
                .Subscribe(OnMatchEnded)
                .AddTo(this);
        }

        private void OnMatchEnded(MatchResult _result)
        {
            if (!IsServer || m_Handling) return;
            m_Handling = true;

            GameDebug.Log($"[RoomReturn] 판 종료: {_result} — {RESULT_SECONDS}초 뒤 대기실로");
            ReturnAfterResult().Forget();
        }

        private async UniTask ReturnAfterResult()
        {
            var cancel = this.GetCancellationTokenOnDestroy();
            // 결과를 읽을 시간을 준다.
            await UniTask.Delay((int)(RESULT_SECONDS * 1000f), cancellationToken: cancel);
            if (!IsServer) return;

            ReturnToRoom();
        }

        /// <summary>
        /// 대기실로 돌아간다. **NGO의 SceneManager로만** 넘긴다 —
        /// 유니티 쪽 로드는 클라이언트가 안 따라온다.
        /// </summary>
        private void ReturnToRoom()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            // 역할은 다 쓰였다. 직전 고양이는 남겨 다음 판의 재추첨에서 뺀다.
            MatchHandoff.ConsumeRoles();

            var status = nm.SceneManager.LoadScene(
                m_RoomSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                GameDebug.LogError($"[RoomReturn] 대기실 복귀 실패: {status}");
                m_Handling = false;
                return;
            }

            // 돌아간 대기실이 떠나기 전 방 설정(봇 수·맵)을 되살린다(2026-09-19).
            Room.RoomReturnMemo.MarkReturning();
        }
    }
}
