using System;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 좌상단 아래쪽: 자기 HP + 고양이 HP(공동 목표 게이지) — MVC의 View.
    ///
    /// 개편 3b(2026-07-31): Update 폴링 → R3 구독.
    /// 고양이를 매 프레임 `FindObjectsByType`으로 찾던 것을 매치 시작 시 한 번만 찾도록 바꿨다.
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private const float CAT_BIND_RETRY_SECONDS = 0.5f;
        private const int CAT_BIND_RETRY_COUNT = 20;

        private readonly SerialDisposable m_MineSub = new SerialDisposable();
        private readonly SerialDisposable m_CatRetry = new SerialDisposable();
        private readonly SerialDisposable m_CatSub = new SerialDisposable();

        private string m_MineText = "SPECTATING";
        private string m_CatText = "CAT -";

        private void Start()
        {
            m_Text.gameObject.SetActive(false);
            m_MineSub.AddTo(this);
            m_CatSub.AddTo(this);
            m_CatRetry.AddTo(this);

            m_Lobby.GameStartedChanged.Subscribe(OnGameStartedChanged).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnGameStartedChanged(bool _started)
        {
            m_Text.gameObject.SetActive(_started);
            if (_started) RetryBindCat();
            else
            {
                m_CatSub.Disposable = null;
                m_CatRetry.Disposable = null;
            }
        }

        // 시작 신호가 스폰보다 먼저 오는 경우가 있어(실측: 시작 직후 CAT - 표시) 잡힐 때까지 재시도한다.
        // 폴링으로 되돌아간 게 아니라 **찾을 때까지만** 도는 한시적 스트림이다.
        private void RetryBindCat()
        {
            if (BindCat()) return;

            m_CatRetry.Disposable = Observable
                .Interval(TimeSpan.FromSeconds(CAT_BIND_RETRY_SECONDS), UnityTimeProvider.Update)
                .Take(CAT_BIND_RETRY_COUNT)
                .Subscribe(_ =>
                {
                    if (BindCat()) m_CatRetry.Disposable = null;
                });
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            var health = _player != null ? _player.GetComponent<Health>() : null;
            if (health == null)
            {
                m_MineSub.Disposable = null;
                m_MineText = "SPECTATING";
                Redraw();
                return;
            }

            m_MineSub.Disposable = health.HpChangedRx.Subscribe(hp =>
            {
                m_MineText = $"HP {hp}/{health.MaxHp}";
                Redraw();
            });
        }

        // 고양이는 매치당 한 마리 — 찾으면 붙이고 true를 돌려준다
        private bool BindCat()
        {
            foreach (Health h in FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                RoleTag tag = h.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat) continue;

                m_CatSub.Disposable = h.HpChangedRx.Subscribe(hp =>
                {
                    m_CatText = $"CAT {hp}/{h.MaxHp}";
                    Redraw();
                });
                return true;
            }
            m_CatText = "CAT -";
            Redraw();
            return false;
        }

        private void Redraw()
        {
            m_Text.text = $"{m_MineText}   {m_CatText}";
        }
    }
}
