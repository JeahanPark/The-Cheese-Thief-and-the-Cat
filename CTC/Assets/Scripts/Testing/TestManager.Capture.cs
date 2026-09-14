// TestManager의 일부 — 화면 거리 지도 캡처 (2026-08-17). 시안·화면 효과 검증용.
//
// 어둠 스킬처럼 **화면에 어떻게 보이나**가 전부인 기능은 모델 값 QA로 못 잡는다(빛 실루엣이 그랬다).
// 색(스크린샷)과 "각 픽셀이 내 쥐에서 몇 m인가"를 같이 떠서 파이썬에서 합성하면
// 셰이더를 짜기 전에 결과를 눈으로 고를 수 있다.
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using Cysharp.Threading.Tasks;
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

        /// <summary>
        /// QA: **아무 자리·방향에서** 게임 화면처럼(후보정 포함) 찍는다(2026-09-14 천장 검수).
        /// 게임 카메라는 몸 뒤 3m에 묶여 있어 천장 채광창처럼 위에 있는 것을 정면으로 못 잡는다.
        /// 몸·카메라는 건드리지 않는다 — 임시 카메라를 세워 한 장 찍고 지운다.
        /// </summary>
        public string TestCaptureFreeView(string _path, float _x, float _y, float _z, float _yaw, float _pitch, float _fov)
        {
            var camGo = new GameObject("QaFreeCam");
            const int W = 1600, H = 900;
            try
            {
                _path = _path.Trim().Trim('"').Replace('\\', '/');
                Camera cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(new Vector3(_x, _y, _z), Quaternion.Euler(_pitch, _yaw, 0f));
                cam.fieldOfView = _fov > 1f ? _fov : 60f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 300f;
                var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
                if (data != null) data.renderPostProcessing = true;

                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
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
            }
            return "ok";
        }

        /// <summary>
        /// QA: **두 눈을 한 프레임에** 크게 찍는다(2026-09-10).
        ///
        /// <see cref="TestCaptureEyeShot"/>은 오른눈 하나를 옆에서 본다 — 「두 눈동자가 같은 쪽을
        /// 보는가」는 그걸로 증명할 수가 없다. <see cref="TestCaptureHeadShot"/>은 플레이 거리라
        /// 얼굴이 너무 작아 눈동자 자리를 못 읽는다. 그래서 **두 흰자 중심의 가운데**를 정면에서
        /// 직교 투영으로 잡아 좌·우 눈이 같은 배율로 나란히 들어오게 한다.
        /// </summary>
        public string TestCaptureEyePairShot(string _path, int _size, string _role = "")
        {
            GameObject local = string.IsNullOrEmpty(_role) ? LocalBody() : NearestOtherBody(_role);
            if (local == null) return "no body";
            var eyes = local.GetComponent<CTC.Runtime.Player.EyeLook>();
            if (eyes == null) return "no EyeLook";

            Transform body = local.transform;
            Vector3 eyeR = eyes.DevEyeWorldRight();
            Vector3 eyeL = eyes.DevEyeWorldLeft();
            Vector3 focus = (eyeR + eyeL) * 0.5f;
            float span = Mathf.Max(0.01f, Vector3.Distance(eyeR, eyeL));

            Vector3 pos = focus + body.forward * (span * 3f);
            int size = _size > 0 ? _size : 512;
            var camGo = new GameObject("QaEyePairCam");
            var lightGo = new GameObject("QaEyePairLight");
            try
            {
                _path = _path.Trim().Trim('"').Replace('\\', '/');
                Camera cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, body.up));
                cam.orthographic = true;
                cam.orthographicSize = span * 0.75f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
                cam.nearClipPlane = 0.005f;

                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2.0f;
                lightGo.transform.rotation = cam.transform.rotation;

                var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
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
            return "ok span=" + span.ToString("F4");
        }

        /// <summary>
        /// QA: 얼굴 정면 한 장 — **인게임 3인칭 카메라와 같은 거리·같은 화각**으로 앞에서 찍는다.
        ///
        /// 게임 화면으로는 뒤통수만 나와 눈도 귀도 안 보인다. 그렇다고 근접 렌더로 찍으면
        /// 「플레이 거리에서 보이나」를 증명하지 못한다 — 그래서 거리(PlayerInfo.CamDistance)와
        /// 화각(PlayerInfo.Fov)은 게임 그대로 두고 **자리만 얼굴 앞으로** 옮긴다.
        /// 머리를 화면 중앙에 두므로 가운데를 잘라 내면 그게 곧 게임뷰 크롭이다.
        /// </summary>
        public string TestCaptureFaceShot(string _path, int _size)
        {
            return TestCaptureHeadShot(_path, _size, 0f, 0f);
        }

        /// <summary>
        /// QA: 같은 거리·같은 화각으로 **머리 둘레를 돌며** 찍는다(2026-09-08).
        /// 눈이 얼굴 밖으로 솟는지는 **옆에서만 보인다** — 정면 컷만으로는 실루엣을 증명할 수 없다.
        /// `_yaw` 0=정면 · 90=옆 · `_pitch` 양수면 위에서 내려다본다.
        /// </summary>
        public string TestCaptureHeadShot(string _path, int _size, float _yaw, float _pitch)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            return CaptureHeadShot(_path, _size, _yaw, _pitch, local != null ? local.gameObject : null);
        }

        /// <summary>
        /// QA: **남의 몸** 얼굴을 같은 거리·같은 화각으로 찍는다(2026-09-10).
        ///
        /// 시선 복제의 값어치는 「남이 볼 때」 나오는데 <see cref="TestCaptureHeadShot"/>은
        /// 제 몸만 찍는다 — 그걸로는 남의 눈이 도는 것을 **증명할 수가 없다**.
        /// 가장 가까운 남의 몸(봇 포함)을 잡아 그 얼굴 앞에서 찍는다.
        /// </summary>
        public string TestCaptureOtherHeadShot(string _path, int _size, float _yaw, float _pitch,
            string _role = "")
        {
            GameObject body = NearestOtherBody(_role);
            if (body == null) return "no other body";
            return CaptureHeadShot(_path, _size, _yaw, _pitch, body);
        }

        private string CaptureHeadShot(string _path, int _size, float _yaw, float _pitch, GameObject _body)
        {
            if (_body == null) return "no local player";
            GameObject local = _body;

            var tag = local.GetComponent<CTC.Runtime.Combat.RoleTag>();
            CTC.Runtime.Lobby.RoleId role = tag != null ? tag.Role : CTC.Runtime.Lobby.RoleId.Mouse;
            var info = CTC.Runtime.Info.InfoManager.Instance.PlayerOf(role);

            Transform body = local.transform;
            Transform head = FindDeep(body, "head");
            Vector3 focus = head != null ? head.position : body.position + Vector3.up * 0.4f;
            Vector3 dir = Quaternion.AngleAxis(_yaw, body.up) * body.forward;
            dir = Quaternion.AngleAxis(-_pitch, Vector3.Cross(body.up, dir)) * dir;
            Vector3 pos = focus + dir * info.CamDistance + body.up * (Mathf.Abs(_pitch) > 0.01f ? 0f : 0.25f);

            // 창고 맵은 상자·통이 빽빽해 얼굴 앞 3 m가 자주 막힌다 — 그대로 찍으면 나무 상자만 남는다.
            // 막혔으면 벽 **앞까지만** 당긴다(최소 0.7 m). 게임 카메라의 벽 회피와 같은 취지다.
            Vector3 back = pos - focus;
            float wanted = back.magnitude;
            if (wanted > 0.01f &&
                Physics.Raycast(focus, back / wanted, out RaycastHit hit, wanted,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                pos = focus + (back / wanted) * Mathf.Max(0.7f, hit.distance - 0.15f);
            }

            int size = _size > 0 ? _size : 1024;
            var camGo = new GameObject("QaFaceCam");
            var lightGo = new GameObject("QaFaceLight");
            try
            {
                _path = _path.Trim().Trim('"').Replace('\\', '/');
                Camera cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, Vector3.up));
                cam.fieldOfView = info.Fov;
                cam.nearClipPlane = 0.05f;

                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.6f;
                lightGo.transform.rotation = Quaternion.LookRotation(focus - pos + Vector3.down * 0.5f);

                var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
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

            var ears = local.GetComponent<CTC.Runtime.Player.EarPerk>();
            var eyes = local.GetComponent<CTC.Runtime.Player.EyeLook>();
            var aim = local.GetComponent<CTC.Runtime.Player.GazeAim>();
            Vector3 gaze = eyes != null ? eyes.DevGazeRight() : Vector3.zero;
            Vector2 angles = aim != null ? aim.DevAngles : Vector2.zero;
            return $"ok role={role} mine={(eyes != null && eyes.DevMine())} " +
                   $"packed={(aim != null ? aim.DevPacked : (short)0)} " +
                   $"aimYaw={angles.x:F1} aimPitch={angles.y:F1} " +
                   $"dist={info.CamDistance:F1} fov={info.Fov:F0} " +
                   $"turn={(ears != null ? ears.DevTurnDegrees : 0f):F2} " +
                   $"perk={(ears != null ? ears.DevPerk : 0f):F3} " +
                   $"gaze={gaze.x:F3},{gaze.y:F3},{gaze.z:F3}";
        }

        /// <summary>
        /// QA: **쫑긋의 정점 프레임**을 잡아 정면·옆면 두 장을 쓴다(2026-09-10).
        ///
        /// 귀가 걸음 단위로 튕기게 바뀐 뒤로 정점은 0.1초쯤만 산다 — 파이썬이 폴링해서 찍으면
        /// 왕복 지연에 놓쳐 「안 움직였다」와 구별이 안 된다. 그래서 **유니티 안에서 기다렸다가**
        /// 그 프레임에 렌더한다. 결과는 <see cref="TestGetEarPeakShotResult"/>로 받는다.
        ///
        /// 파일은 `_path` + "_front.png" / "_side.png"로 쓴다.
        /// </summary>
        public string TestCaptureEarPeakShot(string _path, int _size, float _threshold, float _timeoutSeconds)
        {
            m_EarPeakShotResult = "";
            EarPeakShotAsync(_path, _size, _threshold, _timeoutSeconds, destroyCancellationToken).Forget();
            return "armed";
        }

        /// <summary>QA: <see cref="TestCaptureEarPeakShot"/>의 결과. 빈 문자열이면 아직 기다리는 중.</summary>
        public string TestGetEarPeakShotResult() => m_EarPeakShotResult;

        private string m_EarPeakShotResult = "";

        private async UniTaskVoid EarPeakShotAsync(
            string _path, int _size, float _threshold, float _timeoutSeconds,
            System.Threading.CancellationToken _token)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var ears = local != null ? local.GetComponent<CTC.Runtime.Player.EarPerk>() : null;
            if (ears == null) { m_EarPeakShotResult = "no EarPerk"; return; }

            float deadline = Time.time + Mathf.Max(0.5f, _timeoutSeconds);
            float best = 0f;
            while (Time.time < deadline)
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, _token);
                best = Mathf.Max(best, ears.DevPerk);
                if (ears.DevPerk < _threshold) continue;

                // 정점에서 두 장 — 옆면을 같이 찍는다. 꺾임은 정면에서 안 보인다.
                GameObject body = local.gameObject;
                string front = CaptureHeadShot(_path + "_front.png", _size, 0f, 6f, body);
                string side = CaptureHeadShot(_path + "_side.png", _size, 88f, 6f, body);
                m_EarPeakShotResult =
                    $"ok perk={ears.DevPerk:F3} turnR={ears.DevTurnRight:F2} turnL={ears.DevTurnLeft:F2} " +
                    $"peaks={ears.DevPerkPeaks} | {front} | {side}";
                return;
            }
            m_EarPeakShotResult = $"timeout bestPerk={best:F3} peaks={ears.DevPerkPeaks}";
        }

        private static Transform FindDeep(Transform _root, string _name)
        {
            if (_root.name == _name) return _root;
            for (int i = 0; i < _root.childCount; i++)
            {
                Transform found = FindDeep(_root.GetChild(i), _name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
