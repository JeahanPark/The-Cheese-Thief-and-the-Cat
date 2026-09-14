using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵 분위기 재료 한 벌(2026-09-14) — 전등·달빛 프리팹, 밤 하늘, 후보정 프로필.
    /// 원본은 `Resources/Map/MapAtmosphere.asset`이고 에디터 메뉴 `Tools/CTC/Map/Build Atmosphere`가 굽는다.
    ///
    /// **Resources에 두는 이유**: 씬(`Game.unity`)을 다시 굽지 않고 붙이려고. 씬 재생성은 NGO 해시가 바뀌어
    /// MPPM 클론까지 다시 띄워야 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CTC/Map Atmosphere Profile")]
    public class MapAtmosphereProfile : ScriptableObject
    {
        public const string RESOURCE_PATH = "Map/MapAtmosphere";

        [SerializeField] private GameObject m_LampPrefab;
        [SerializeField] private GameObject m_MoonShaftPrefab;
        [SerializeField] private Material m_NightSkybox;
        [SerializeField] private VolumeProfile m_PostProfile;

        [Header("빛웅덩이 세기 — 전등이 생기면서 광원을 조금 올린다(빛 자리·반경은 그대로)")]
        [SerializeField] private float m_LampLightIntensity = 14f;
        [SerializeField] private float m_LampHeight = 4.2f;

        [Header("달빛·환경광 — 씬에 구운 값(0.35 · 0.055)은 ACES 후보정 아래에서 캄캄했다. 판을 짓을 때 덮는다")]
        [SerializeField] private float m_MoonIntensity = 0.7f;
        [SerializeField] private Color m_Ambient = new Color(0.15f, 0.17f, 0.23f);

        [Header("깜빡임 — 전등 몇 개 중 하나꼴")]
        [SerializeField] private int m_FlickerOneIn = 3;

        public GameObject LampPrefab => m_LampPrefab;
        public GameObject MoonShaftPrefab => m_MoonShaftPrefab;
        public Material NightSkybox => m_NightSkybox;
        public VolumeProfile PostProfile => m_PostProfile;
        public float LampLightIntensity => m_LampLightIntensity;
        public float LampHeight => m_LampHeight;
        public int FlickerOneIn => m_FlickerOneIn;
        public float MoonIntensity => m_MoonIntensity;
        public Color Ambient => m_Ambient;
    }
}
