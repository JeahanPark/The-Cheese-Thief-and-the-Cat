using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>재생 가능한 효과음 목록 — SfxPlayer.m_Clips 배열 인덱스와 1:1 (씬 생성기가 순서대로 배선).</summary>
    public enum SfxId
    {
        PopgunFire = 0,
        SlingshotFire = 1,
        MachinegunFire = 2,
        PounceSwing = 3,
        MouseHurt = 4,
        CatHurt = 5,
        MouseDeath = 6,
        CatDeath = 7,
        FortressHit = 8,
        FortressBreak = 9,
        FortressPlace = 10,
        // 11 = DecoyPlace(미끼) — 2026-08-15 삭제. m_Clips는 enum 값 순서와 1:1이라 번호는 비워 둔다.
        MatchStart = 12,
        MouseWin = 13,
        CatWin = 14,
        MouseDash = 15,
        CheeseTick = 16,
        HealTick = 17,
        ShopBuy = 18,
        ShopDeny = 19,
        UiClick = 20,
        TimerWarn = 21,
        WarehouseTick = 22,
        CheeseDepleted = 23,
        // 벽감(v3.8) — 요새 소리를 재사용하지 않는 이유: 쥐에게 요새 파괴음은 "도망"이고
        // 벽감 파괴음은 "달려와"라 정반대 행동을 요구한다. 소리가 정보인 게임이라 겹치면 안 된다.
        AlcoveHit = 24,
        AlcoveBreak = 25,
        // 강아지(v3.8 5번). 짖음이 강아지의 **유일한 위치 정보**다 — 미니맵에도 팀에도 없다.
        DogBark = 26,
        DogBite = 27,
        DogHurt = 28,
        DogDeath = 29,
        /// <summary>벽 안에서 새는 소리. 이것만 루프고, 치즈 벽감은 조용하다.</summary>
        DogAlcoveIdle = 30,
        // 상자(v3.9) — 벽감과 소리를 나눈다. 벽감 파괴음은 '달려와'(25m)고
        // 상자 파괴음은 '찾은 사람이 조용히 먹는다'(8m)라 뜻이 정반대다.
        BoxHit = 31,
        BoxBreak = 32,
        // 고양이 전직(v3.9). 둘 다 **쥐도 들어야 한다** — 큰 효과에 소리를 붙이는 게
        // 스킬을 액티브로 만든 이유다. 모르면 대응할 수 없다.
        CatAdvance = 33,
        CatSkill = 34,
        // 쥐 직업(v3.9). 깡패 등장은 판의 전환점이라 멀리 간다 — 다른 쥐가 대비해야 한다.
        JobPickup = 35,
        GangRise = 36,
    }

    /// <summary>
    /// 효과음 재생 허브(씬 오브젝트 1개). 월드 소리는 3D(거리 감쇠), UI·알림은 2D.
    /// 원샷은 임시 GameObject로 재생 — 발신자가 despawn돼도 소리는 끝까지 남는다(사망음).
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        private const float WORLD_MIN_DISTANCE = 1.5f; // 이 안에서는 최대 볼륨
        private const float WORLD_MAX_DISTANCE = 20f;  // 이 밖에서는 무음 (Linear 감쇠)

        [SerializeField] private AudioClip[] m_Clips; // SfxId 순서와 1:1

        private static SfxPlayer s_Instance;

        private void Awake()
        {
            s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        /// <summary>
        /// 3D 단발음이 났다 — 무엇이·어디서·몇 m까지. **듣는 쪽이 구독한다**(2026-08-11).
        ///
        /// 밸런스 측정용 봇이 "들었다"를 판단하는 유일한 근거다. 이 한 줄이 있어서
        /// 벽감·상자·강아지·스킬 쪽 코드를 하나도 안 건드리고 소리를 관찰할 수 있다.
        /// 구독자가 없으면(배포 빌드) 아무 일도 일어나지 않는다.
        /// </summary>
        public static event System.Action<SfxId, Vector3, float> Played;

        /// <summary>원샷 소리 껍데기를 담는 풀의 이름표 — 3D·2D가 같은 껍데기를 나눠 쓴다.</summary>
        private const string POOL_KEY = "sfx.oneshot";

        /// <summary>풀이 비었을 때만 불린다. AudioSource 하나만 얹은 빈 껍데기다.</summary>
        private static GameObject CreateVoice()
        {
            var go = new GameObject("SfxVoice");
            go.AddComponent<AudioSource>().playOnAwake = false;
            return go;
        }

        /// <summary>월드 위치 3D 원샷 — 리스너(자기 카메라) 거리에 따라 감쇠. maxDistance로 가청 반경 조절.</summary>
        public static void PlayAt(SfxId _id, Vector3 _position, float _volume = 1f, float _maxDistance = WORLD_MAX_DISTANCE)
        {
            Played?.Invoke(_id, _position, _maxDistance);

            AudioClip clip = ClipOf(_id);
            if (clip == null) return;

            // 발소리는 판 내내 쏟아진다 — 한 발마다 GameObject를 만들고 부수지 않고 껍데기를 돌려 쓴다.
            GameObject go = Core.PoolManager.Instance.SpawnFrom(POOL_KEY, CreateVoice);
            if (go == null) return;

            go.transform.SetParent(null, false);
            go.transform.position = _position;

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null) return;

            source.clip = clip;
            source.volume = _volume * Core.UserSettings.SfxVolume.Value;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = WORLD_MIN_DISTANCE;
            source.maxDistance = _maxDistance;
            source.Play();

            Core.PoolManager.Instance.DespawnAfter(go, clip.length + 0.1f);
        }

        /// <summary>2D 원샷 — UI·매치 알림 등 거리 무관 소리.</summary>
        public static void PlayUi(SfxId _id, float _volume = 1f)
        {
            AudioClip clip = ClipOf(_id);
            if (clip == null) return;

            GameObject go = Core.PoolManager.Instance.SpawnFrom(POOL_KEY, CreateVoice);
            if (go == null) return;

            go.transform.SetParent(null, false);

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null) return;

            source.clip = clip;
            source.volume = _volume * Core.UserSettings.SfxVolume.Value;
            source.spatialBlend = 0f;
            source.Play();

            Core.PoolManager.Instance.DespawnAfter(go, clip.length + 0.1f);
        }

        /// <summary>QA용 — 해당 id에 배선된 클립 이름(미배선이면 빈 문자열). 무음 회귀를 잡는다.</summary>
        public static string ClipNameOf(SfxId _id)
        {
            AudioClip clip = ClipOf(_id);
            return clip != null ? clip.name : string.Empty;
        }

        /// <summary>
        /// 루프로 직접 틀 클립 — 원샷이 아니라 `AudioSource`에 얹어야 하는 소리가 쓴다
        /// (벽감이 흘리는 강아지 소리). 배선을 두 군데로 갈라 놓지 않으려고 여기서 꺼내 준다.
        /// </summary>
        public static AudioClip ClipFor(SfxId _id) => ClipOf(_id);

        private static AudioClip ClipOf(SfxId _id)
        {
            if (s_Instance == null || s_Instance.m_Clips == null) return null;
            int index = (int)_id;
            if (index < 0 || index >= s_Instance.m_Clips.Length) return null;
            return s_Instance.m_Clips[index];
        }
    }
}
