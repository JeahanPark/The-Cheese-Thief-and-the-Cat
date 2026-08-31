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
    }
}
#endif
