using CTC.Runtime.Core;
using UnityEngine;

namespace CTC.Runtime.Fx
{
    /// <summary>
    /// 기술 이펙트의 단독 입구(2026-09-14) — <c>SfxPlayer.PlayAt</c>과 같은 모양. 이미 전 클라가 도는 `*FxRpc`에 한 줄씩 붙인다.
    ///
    /// **네트워크를 모른다** — 부르는 쪽이 이미 전 클라에서 돌고 있어야 한다. 서버 전용 자리(가시 반사 등)는
    /// 호출부가 Rpc를 하나 더 둔다.
    /// 풀(<see cref="PoolManager"/>)로 돌린다: 기관총은 0.1초마다 쏜다.
    /// </summary>
    public static class FxPlayer
    {
        private static FxCatalog s_Catalog;
        private static bool s_Loaded;

        /// <summary>QA가 "정말 떴나"를 센다 — 화면을 못 보는 스위트용.</summary>
        public static event System.Action<FxId, Vector3> Played;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Catalog = null;
            s_Loaded = false;
            Played = null;
        }

        private static FxCatalog Catalog
        {
            get
            {
                if (!s_Loaded)
                {
                    s_Loaded = true;
                    s_Catalog = Resources.Load<FxCatalog>(FxCatalog.RESOURCE_PATH);
                    if (s_Catalog == null) GameDebug.LogWarning("[FxPlayer] 카탈로그가 없다 — Tools/CTC/Fx/Build Skill Effects");
                }
                return s_Catalog;
            }
        }

        public static void PlayAt(FxId _id, Vector3 _position) => PlayAt(_id, _position, Quaternion.identity, 1f);

        public static void PlayAt(FxId _id, Vector3 _position, Quaternion _rotation, float _scale = 1f)
        {
            Played?.Invoke(_id, _position);

            FxCatalog catalog = Catalog;
            GameObject prefab = catalog != null ? catalog.PrefabOf(_id) : null;
            if (prefab == null || PoolManager.Instance == null) return;

            GameObject go = PoolManager.Instance.Spawn(prefab, _position, _rotation);
            if (go == null) return;

            go.transform.localScale = Vector3.one * _scale;
            var shot = go.GetComponent<FxOneShot>();
            float seconds = 1.5f;
            if (shot != null)
            {
                shot.Restart();
                seconds = shot.Seconds;
            }
            PoolManager.Instance.DespawnAfter(go, seconds);
        }
    }
}
