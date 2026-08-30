using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 정해진 시간이 지나면 자기를 풀로 돌려보내는 시계.
    ///
    /// 코루틴이 아니라 UniTask다(CTC 규칙 1). 꺼질 때 토큰을 끊으므로,
    /// 누가 먼저 <see cref="PoolManager.Despawn"/>을 불러 접어 버려도 시계가 뒤늦게
    /// 두 번 돌려주는 일이 없다 — 그게 풀이 같은 물건을 두 번 내주는 고전적 사고다.
    /// </summary>
    public class PooledAutoRelease : MonoBehaviour
    {
        private CancellationTokenSource m_Cts;

        public void Begin(float _seconds)
        {
            Stop();

            if (_seconds <= 0f)
            {
                PoolManager.Instance.Despawn(gameObject);
                return;
            }

            m_Cts = new CancellationTokenSource();
            RunAsync(_seconds, m_Cts.Token).Forget();
        }

        private void OnDisable() => Stop();

        private void OnDestroy() => Stop();

        private void Stop()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
        }

        private async UniTask RunAsync(float _seconds, CancellationToken _token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken: _token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_token.IsCancellationRequested) return;
            if (this == null) return;

            PoolManager.Instance.Despawn(gameObject);
        }
    }
}
