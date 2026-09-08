// TestManager의 일부 — 화면 거리 지도 캡처 (2026-08-17). 시안·화면 효과 검증용.
//
// 어둠 스킬처럼 **화면에 어떻게 보이나**가 전부인 기능은 모델 값 QA로 못 잡는다(빛 실루엣이 그랬다).
// 색(스크린샷)과 "각 픽셀이 내 쥐에서 몇 m인가"를 같이 떠서 파이썬에서 합성하면
// 셰이더를 짜기 전에 결과를 눈으로 고를 수 있다.
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// QA: 메인 카메라에서 `_cols`×`_rows` 격자로 레이를 쏴 각 픽셀이 닿는 지점의
        /// **내 쥐로부터의 거리(m)**를 float32 raw(행 우선, 위→아래)로 `_path.dist`에 쓴다.
        /// 아무것도 안 닿으면 -1. URP RenderGraph에선 깊이 텍스처 전역을 못 읽어 레이로 잰다.
        /// </summary>
        public string TestCaptureDistanceMap(string _path, int _cols, int _rows)
        {
            Camera cam = Camera.main;
            if (cam == null) return "no camera";

            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) return "no local player";
            Vector3 me = local.transform.position;

            var buf = new float[_cols * _rows];
            for (int y = 0; y < _rows; y++)
            {
                for (int x = 0; x < _cols; x++)
                {
                    // 뷰포트 (0,0)=좌하 → 위→아래로 저장하려고 y를 뒤집는다
                    Vector3 vp = new Vector3((x + 0.5f) / _cols, 1f - (y + 0.5f) / _rows, 0f);
                    Ray ray = cam.ViewportPointToRay(vp);
                    float d = -1f;
                    if (Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        d = Vector3.Distance(hit.point, me);
                    }
                    buf[y * _cols + x] = d;
                }
            }

            var bytes = new byte[buf.Length * 4];
            System.Buffer.BlockCopy(buf, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(_path + ".dist", bytes);
            return $"ok {_cols}x{_rows}";
        }

        /// <summary>
        /// QA: 로컬 몸의 **오른눈을 늘 같은 시점에서** 렌더해 PNG로 쓴다(2026-09-08).
        ///
        /// 눈동자는 게임 카메라를 따라가므로 게임 화면으로 찍으면 시점과 시선이 같이 움직여
        /// "눈이 움직였다"를 증명할 수 없다. 몸 기준 고정 시점으로 찍어야 **눈만 움직인 프레임**이 남는다.
        /// </summary>
        public string TestCaptureEyeShot(string _path)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) return "no local player";

            var eyes = local.GetComponent<CTC.Runtime.Player.EyeLook>();
            if (eyes == null) return "no EyeLook";

            Transform body = local.transform;
            Vector3 focus = eyes.DevEyeWorldRight();
            Vector3 pos = focus + body.right * 0.55f + body.up * 0.16f + body.forward * 0.30f;

            var camGo = new GameObject("QaEyeCam");
            var lightGo = new GameObject("QaEyeLight");
            try
            {
                // InvokeBridge가 따옴표째 넘기는 일이 있다 — Mono는 그걸 "Name has invalid chars"로 던진다.
                _path = _path.Trim().Trim('"').Replace('\\', '/');
                Camera cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, body.up));
                cam.orthographic = true;
                cam.orthographicSize = 0.16f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.11f, 0.14f, 1f);
                cam.nearClipPlane = 0.01f;

                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2.2f;
                lightGo.transform.rotation = cam.transform.rotation;

                var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                File.WriteAllBytes(_path, tex.EncodeToPNG());
                cam.targetTexture = null;
                Destroy(tex);
                rt.Release();
                Destroy(rt);
            }
            catch (System.Exception ex)
            {
                return "ERR path=[" + _path + "] " + ex.Message;
            }
            finally
            {
                Destroy(camGo);
                Destroy(lightGo);
            }

            Vector3 g = eyes.DevGazeRight();
            return $"ok gaze={g.x:F3},{g.y:F3},{g.z:F3}";
        }
    }
}
#endif
