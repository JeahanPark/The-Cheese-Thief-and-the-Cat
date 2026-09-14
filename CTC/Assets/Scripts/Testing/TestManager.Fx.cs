using System.Collections.Generic;
using CTC.Runtime.Fx;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// 기술 이펙트 QA 훅(2026-09-14). 화면을 못 보는 스위트는 <see cref="TestGetFxCounts"/>로 "정말 떴나"를 세고,
    /// 사람은 <see cref="TestPlayFxInFront"/>로 한 개씩 띄워 스크린샷을 찍는다.
    /// </summary>
    public partial class TestManager
    {
        private readonly Dictionary<FxId, int> m_FxCounts = new Dictionary<FxId, int>();
        private bool m_FxCounting;

        /// <summary>이펙트를 이름으로 내 몸 앞(_meters)에 띄운다. 모르는 이름이면 false.</summary>
        public bool TestPlayFxInFront(string _fxName, float _meters)
        {
            if (!System.Enum.TryParse(_fxName, out FxId id)) return false;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            Transform at = local != null ? local.transform : (Camera.main != null ? Camera.main.transform : null);
            if (at == null) return false;

            Vector3 forward = new Vector3(at.forward.x, 0f, at.forward.z).normalized;
            FxPlayer.PlayAt(id, at.position + forward * _meters, Quaternion.LookRotation(forward.sqrMagnitude > 0f ? forward : Vector3.forward));
            return true;
        }

        /// <summary>
        /// 게임 뷰를 그대로 PNG로 — 이펙트는 0.3초면 사라져 MCP 스크린샷 왕복으로는 못 잡는다.
        /// 띄운 직후 같은 스크립트에서 부른다(프레임 끝에 쓰인다). HUD까지 찍힌다.
        /// </summary>
        public void TestCaptureGameView(string _path)
        {
            string dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(_path);
        }

        /// <summary>이 인스턴스에서 뜬 이펙트 수를 센다 — 부른 순간부터. 다시 부르면 0부터.</summary>
        public void TestResetFxCounts()
        {
            m_FxCounts.Clear();
            if (m_FxCounting) return;
            FxPlayer.Played += OnFxPlayed;
            m_FxCounting = true;
        }

        /// <summary>"MuzzleFlash=3,BulletImpact=3" 꼴.</summary>
        public string TestGetFxCounts()
        {
            var parts = new List<string>();
            foreach (KeyValuePair<FxId, int> kv in m_FxCounts) parts.Add($"{kv.Key}={kv.Value}");
            parts.Sort(System.StringComparer.Ordinal);
            return string.Join(",", parts);
        }

        private void OnFxPlayed(FxId _id, Vector3 _at)
        {
            m_FxCounts.TryGetValue(_id, out int n);
            m_FxCounts[_id] = n + 1;
        }
    }
}
