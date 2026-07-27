using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 앱 전역 초기 설정 — 씬 로드 전에 실행되므로 씬 배선·컴포넌트 순서에 의존하지 않는다.
    /// runInBackground: 창이 포커스를 잃어도 계속 갱신 (미설정 시 알탭한 클라가 얼어
    /// NGO 하트비트가 끊기고 세션에서 튕긴다 — 2026-07-26 실측).
    /// </summary>
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Application.runInBackground = true;
        }
    }
}
