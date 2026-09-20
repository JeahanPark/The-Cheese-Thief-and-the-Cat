using System.Threading;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 고양이가 다가오면 배경음이 추격곡으로 넘어간다(2026-09-20 사용자 요청) — **쥐에게만**, 살아 있는 동안만.
    ///
    /// 가장 가까운 산 고양이(봇 포함)가 CSV `ChaseBgmMeters`(살금살금이면 `ChaseBgmSneakMeters`) 안에 들면 켜고,
    /// 벗어나서 <see cref="ChaseBgmLogic.HOLD_SECONDS"/>를 버티면 인게임 곡으로 돌아간다. 판정은 0.2초마다
    /// (상점 철문 `ShopShutter`와 같은 주기·같은 방법), 섞기는 매 프레임이다 — 곡이 끊겨 들리면 안 된다.
    ///
    /// **볼륨을 직접 안 쓴다.** 두 곡 다 `AudioVolumeFollower.SetWeight`로 섞는 비율만 준다 — 볼륨의 주인은
    /// 여전히 팔로워라 옵션 슬라이더를 움직여도 비율이 유지된다. 인게임 곡(`BgmDirector`)은 멈추지 않고 비율만
    /// 0으로 내려, 추격이 끝나면 하던 자리에서 이어진다. 추격곡은 켤 때마다 처음부터 튼다(도입부가 「왔다!」다).
    ///
    /// 로컬 연출이라 네트워크와 무관하다 — 고양이 위치·잠행은 이미 복제된 값을 읽는다.
    /// 추격곡이 아직 없으면(음원 대기) 판정만 돌고 소리는 안 바꾼다.
    /// </summary>
    public class ChaseBgm : MonoBehaviour
    {
        private const int CHECK_MILLIS = 200;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private AudioSource m_Source;            // 추격곡 루프
        [SerializeField] private AudioVolumeFollower m_Follower;  // 추격곡 볼륨의 주인(Bgm)

        private NetworkObject m_Me;
        private bool m_Chasing;
        private float m_LastInRangeAt = float.NegativeInfinity;
        private float m_Weight;
        private float m_NearestCatMeters = -1f;
        private float m_TriggerMeters = -1f;
        private AudioVolumeFollower m_MainFollower;

        /// <summary>QA: 지금 추격 중인가 · 추격곡 비율 · 가장 가까운 산 고양이 거리 · 그 고양이의 반경(없으면 -1).</summary>
        public bool Chasing => m_Chasing;
        public float Weight => m_Weight;
        public float NearestCatMeters => m_NearestCatMeters;
        public float TriggerMeters => m_TriggerMeters;
        public bool HasClip => m_Source != null && m_Source.clip != null;

        private void Start()
        {
            ApplyWeights(0f);
            LocalPlayerBinder.LocalPlayer.Subscribe(p => m_Me = p).AddTo(this);
            EvaluateLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnDisable()
        {
            // 인게임 곡은 씬을 넘어 산다(BgmDirector) — 추격 중에 씬이 내려가면 로비 곡이 비율 0에 묶여 조용해진다
            m_Chasing = false;
            m_Weight = 0f;
            ApplyWeights(0f);
            if (m_Source != null && m_Source.isPlaying) m_Source.Stop();
        }

        private async UniTaskVoid EvaluateLoopAsync(CancellationToken _token)
        {
            while (true)
            {
                Evaluate();
                bool cancelled = await UniTask.Delay(CHECK_MILLIS, cancellationToken: _token).SuppressCancellationThrow();
                if (cancelled) return;
            }
        }

        // 쥐인 내가 살아 있고 판이 도는 동안만 — 고양이·관전자·결과 화면은 인게임 곡 그대로다
        private void Evaluate()
        {
            m_NearestCatMeters = -1f;
            m_TriggerMeters = -1f;
            bool inRange = false;

            if (IsLiveMouse(m_Me) && m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted)
            {
                Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
                Vector3 me = m_Me.transform.position;
                foreach (RoleTag tag in FindObjectsByType<RoleTag>())
                {
                    if (tag == null || tag.Role != RoleId.Cat) continue;
                    var health = tag.GetComponent<Health>();
                    if (health != null && health.IsDead) continue;

                    var sneak = tag.GetComponent<Player.Sneak>();
                    float trigger = ChaseBgmLogic.TriggerMeters(sneak != null && sneak.Sneaking,
                        b.ChaseBgmMeters, b.ChaseBgmSneakMeters);
                    float meters = ChaseBgmLogic.FlatDistance(me, tag.transform.position);
                    if (m_NearestCatMeters < 0f || meters < m_NearestCatMeters)
                    {
                        m_NearestCatMeters = meters;
                        m_TriggerMeters = trigger;
                    }
                    inRange |= meters <= trigger;
                }
            }

            if (inRange) m_LastInRangeAt = Time.unscaledTime;
            m_Chasing = ChaseBgmLogic.NextChasing(m_Chasing, inRange, Time.unscaledTime - m_LastInRangeAt);
        }

        private static bool IsLiveMouse(NetworkObject _player)
        {
            if (_player == null) return false;
            var tag = _player.GetComponent<RoleTag>();
            if (tag == null || tag.Role != RoleId.Mouse) return false;
            var health = _player.GetComponent<Health>();
            return health == null || !health.IsDead;
        }

        private void Update()
        {
            if (!HasClip) return;   // 추격곡 음원 대기 — 인게임 곡을 건드리지 않는다

            m_Weight = ChaseBgmLogic.StepWeight(m_Weight, m_Chasing, Time.unscaledDeltaTime);
            ApplyWeights(m_Weight);

            if (m_Weight > 0f && !m_Source.isPlaying) m_Source.Play();
            else if (m_Weight <= 0f && m_Source.isPlaying) m_Source.Stop();
        }

        private void ApplyWeights(float _chaseWeight)
        {
            if (m_Follower != null) m_Follower.SetWeight(_chaseWeight);
            AudioVolumeFollower main = MainFollower();
            if (main != null) main.SetWeight(1f - _chaseWeight);
        }

        // 인게임 곡의 팔로워 — BgmDirector는 씬을 넘어 살고 팔로워는 나중에 붙을 수 있어 그때그때 찾는다
        private AudioVolumeFollower MainFollower()
        {
            if (m_MainFollower != null) return m_MainFollower;
            BgmDirector director = BgmDirector.Instance;
            if (director != null) m_MainFollower = director.GetComponent<AudioVolumeFollower>();
            return m_MainFollower;
        }
    }
}
