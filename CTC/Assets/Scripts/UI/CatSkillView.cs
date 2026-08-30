using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using CTC.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 빛·어둠 스킬이 **화면에 보이는 부분** (v3.9). 서버는 화면을 그릴 수 없으므로
    /// 보는 쪽이 각자 그린다 — `CatSkill.Active`와 `CatClass`는 복제되니 판단 근거는 같다.
    ///
    /// - **빛**: 내가 빛 고양이면, 살아 있는 쥐를 벽 너머로 비춰 준다(항상 위에 그리는 실루엣).
    /// - **어둠**: 어둠 고양이의 스킬이 켜져 있으면, **쥐 화면만** 어두워진다 — 균일 암전이 아니라
    ///   내 쥐로부터 멀수록 검어지는 깊이 기반 후처리(`Rendering/DarknessFeature`, 2026-08-17 개편).
    ///
    /// 불·바람과 달리 여기 있는 이유: 저 둘은 서버가 계산하는 수치고, 이 둘은 **시야**라서
    /// 각 클라이언트의 카메라 앞에서만 의미가 있다.
    ///
    /// 매 프레임 씬을 뒤지지 않는다 — 스킬이 켜지고 꺼질 때만 목록을 새로 만든다.
    /// </summary>
    public class CatSkillView : MonoBehaviour
    {
        // 실루엣이 원본보다 이만큼 크다. 너무 키우면 벽을 뚫고 나온 것처럼 보인다.
        private const float SILHOUETTE_SCALE = 1.06f;

        [SerializeField] private Material m_SilhouetteMaterial;

        // 어둠 페이드 — 켜질 때 0.5초에 걸쳐 어두워지고 꺼질 때 다시 밝아진다(서서히)
        private const float DARK_FADE_SECONDS = 0.5f;
        // 어둠 중엔 햇빛·환경광도 죽인다(2026-08-17) — 후처리만으로는 코앞이 대낮이라 "밤"이 안 된다.
        // 대신 내 쥐에 랜턴을 하나 붙여 제 주변만 따뜻하게. 전부 **내 화면의 조명**이라 남에겐 안 보인다.
        private const float DARK_SUN_FACTOR = 0.12f;
        private const float DARK_AMBIENT_FACTOR = 0.2f;
        private const float LANTERN_RANGE = 5f;
        private const float LANTERN_INTENSITY = 2.2f;
        private static readonly Color LANTERN_COLOR = UiPalette.AccentSoft;   // 조명색 — 강조색 계열로 맞춘다
        private float m_DarkStrength;
        private Light m_Sun;
        private float m_SunIntensity;
        private float m_AmbientIntensity;
        private Light m_Lantern;

        private readonly List<GameObject> m_Silhouettes = new List<GameObject>();
        private bool m_LightOn;
        private float m_Rescan;

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;

            NetworkObject me = nm.SpawnManager.GetLocalPlayerObject();
            ApplyLight(me);
            ApplyDark(me);
        }

        // ── 빛 ── 내가 빛 고양이일 때만. 쥐가 탈락하면 목록이 바뀌므로 주기적으로 다시 만든다.
        private void ApplyLight(NetworkObject _me)
        {
            bool on = _me != null
                      && _me.GetComponent<CatClass>() is { } cls && cls.Value == CatClassId.Light
                      && _me.GetComponent<CatSkill>() is { } skill && skill.Active;

            m_Rescan -= Time.deltaTime;
            if (on && (!m_LightOn || m_Rescan <= 0f))
            {
                m_Rescan = 0.5f;
                ClearSilhouettes();
                BuildSilhouettes();
            }
            else if (!on && m_LightOn)
            {
                ClearSilhouettes();
            }
            m_LightOn = on;
        }

        private void BuildSilhouettes()
        {
            if (m_SilhouetteMaterial == null) return;

            foreach (RoleTag tag in FindObjectsByType<RoleTag>(FindObjectsSortMode.None))
            {
                if (tag.Role != RoleId.Mouse) continue;

                var no = tag.GetComponent<NetworkObject>();
                if (no == null || !no.IsPlayerObject) continue;      // 요새·미끼는 비추지 않는다

                var health = tag.GetComponent<Health>();
                if (health != null && health.IsDead) continue;

                foreach (Renderer src in tag.GetComponentsInChildren<Renderer>(false))
                {
                    if (src is not (MeshRenderer or SkinnedMeshRenderer)) continue;

                    var ghost = new GameObject("Silhouette");
                    ghost.transform.SetParent(src.transform, false);
                    ghost.transform.localScale = Vector3.one * SILHOUETTE_SCALE;

                    CopyMesh(src, ghost);
                    m_Silhouettes.Add(ghost);
                }
            }
        }

        // 스킨드 메시는 매 프레임 자세가 바뀌므로 그대로 베끼면 T포즈가 된다 —
        // 구운 메시를 한 장 떠서 쓴다(0.5초마다 다시 만드는 이유이기도 하다).
        private void CopyMesh(Renderer _src, GameObject _ghost)
        {
            Mesh mesh = null;
            if (_src is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh();
                skinned.BakeMesh(mesh, true);
            }
            else
            {
                var filter = _src.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh == null) return;

            _ghost.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = _ghost.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = m_SilhouetteMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void ClearSilhouettes()
        {
            foreach (GameObject go in m_Silhouettes)
            {
                if (go != null) Destroy(go);
            }
            m_Silhouettes.Clear();
        }

        // ── 어둠 ── 쥐 화면만 어두워진다. 고양이 자신은 멀쩡해야 사냥이 성립한다.
        // 내 쥐 위치를 중심으로 멀수록 검게 — 반경·먼 곳 밝기는 CSV(CatDarkNear/FarMeters·CatDarkStrength).
        private void ApplyDark(NetworkObject _me)
        {
            bool amMouse = _me != null
                           && _me.GetComponent<RoleTag>() is { } tag && tag.Role == RoleId.Mouse;

            float target = amMouse && AnyDarkSkillActive() ? 1f : 0f;
            m_DarkStrength = Mathf.MoveTowards(m_DarkStrength, target, Time.deltaTime / DARK_FADE_SECONDS);

            Vector3 center = _me != null ? _me.transform.position : Vector3.zero;
            Rendering.DarknessFeature.Apply(center,
                CatClassLogic.DarkNearMeters, CatClassLogic.DarkFarMeters,
                1f - CatClassLogic.DarkStrength, m_DarkStrength);
            ApplyDarkLighting(_me, m_DarkStrength);
        }

        // 햇빛·환경광을 강도만큼 죽이고, 내 쥐에 랜턴을 켠다. 강도 0이면 전부 원상복구.
        private void ApplyDarkLighting(NetworkObject _me, float _strength)
        {
            if (m_Sun == null && _strength > 0f)
            {
                m_Sun = RenderSettings.sun;
                if (m_Sun == null)
                {
                    foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (l.type == LightType.Directional) { m_Sun = l; break; }
                    }
                }
                if (m_Sun != null) m_SunIntensity = m_Sun.intensity;
                m_AmbientIntensity = RenderSettings.ambientIntensity;
            }
            if (m_Sun == null) return;

            m_Sun.intensity = m_SunIntensity * Mathf.Lerp(1f, DARK_SUN_FACTOR, _strength);
            RenderSettings.ambientIntensity = m_AmbientIntensity * Mathf.Lerp(1f, DARK_AMBIENT_FACTOR, _strength);

            bool wantLantern = _strength > 0.001f && _me != null;
            if (wantLantern && m_Lantern == null)
            {
                var go = new GameObject("DarkLantern");
                m_Lantern = go.AddComponent<Light>();
                m_Lantern.type = LightType.Point;
                m_Lantern.range = LANTERN_RANGE;
                m_Lantern.color = LANTERN_COLOR;
                m_Lantern.shadows = LightShadows.None;
            }
            if (m_Lantern != null)
            {
                if (!wantLantern)
                {
                    Destroy(m_Lantern.gameObject);
                    m_Lantern = null;
                    return;
                }
                m_Lantern.transform.position = _me.transform.position + Vector3.up * 1.2f;
                m_Lantern.intensity = LANTERN_INTENSITY * _strength;
            }
        }

        private static bool AnyDarkSkillActive()
        {
            foreach (CatSkill skill in FindObjectsByType<CatSkill>(FindObjectsSortMode.None))
            {
                if (!skill.Active) continue;

                var cls = skill.GetComponent<CatClass>();
                if (cls != null && cls.Value == CatClassId.Dark) return true;
            }
            return false;
        }
    }
}
