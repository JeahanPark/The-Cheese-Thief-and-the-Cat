using System.IO;
using CTC.Runtime.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 개발용: 대기실 씬을 **플레이 없이** 찍는다(2026-09-19 — 벽을 두른 뒤 모양 확인용).
    /// 스팀 클라이언트가 떠 있으면 에디터 플레이가 위험하고(CLAUDE.md 함정 10) 빌드는 실행 중이면 못 덮어써서 만들었다.
    /// 대기실 몸 세 마리를 서는 자리에 잠깐 세우고 두 각도로 `%TEMP%/ctc-room-shot-*.png`에 쓴 뒤 치운다.
    /// </summary>
    public static class RoomSceneShot
    {
        private const string ROOM_SCENE = "Assets/Scenes/Room.unity";
        private const string AVATAR = "Assets/Prefabs/RoomMouse.prefab";
        private const float STAND_RADIUS = 5f;
        private const int W = 1280, H = 720;

        [MenuItem("Tools/CTC/Dev/Shoot Room Scene (대기실 사진)")]
        private static void Shoot()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[RoomSceneShot] 플레이 중에는 안 찍는다"); return; }
            if (EditorSceneManager.GetActiveScene().path != ROOM_SCENE)
                EditorSceneManager.OpenScene(ROOM_SCENE, OpenSceneMode.Single);

            var avatar = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR);
            var temp = new System.Collections.Generic.List<GameObject>();
            for (int slot = 0; slot < 3 && avatar != null; slot++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(avatar);
                go.transform.position = RoomLogic.StandPosition(slot, RoomLogic.MAX_PLAYERS, STAND_RADIUS);
                go.transform.rotation = Quaternion.Euler(0f, RoomLogic.StandYaw(slot, RoomLogic.MAX_PLAYERS), 0f);
                temp.Add(go);
            }

            Vector3 me = temp.Count > 0 ? temp[0].transform.position : new Vector3(0f, 0f, -STAND_RADIUS);
            Vector3 toCenter = (-me).normalized;
            // ① 플레이어 시점 — 궤도 카메라처럼 몸 뒤 4.5m·약간 위에서 방 가운데를 본다
            Render(me - toCenter * 4.5f + Vector3.up * 2.4f, me + toCenter * 6f + Vector3.up * 0.6f, 55f, "player");
            // ② 전경 — 모서리 높은 곳에서 방 전체
            Render(new Vector3(-9.5f, 9f, -9.5f), new Vector3(0f, 0f, 0f), 60f, "overview");

            foreach (GameObject go in temp) Object.DestroyImmediate(go);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void Render(Vector3 _pos, Vector3 _lookAt, float _fov, string _tag)
        {
            var camGo = new GameObject("RoomShotCamera");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.transform.position = _pos;
                cam.transform.LookAt(_lookAt);
                cam.fieldOfView = _fov;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.008f, 0.012f, 0.020f);
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string path = Path.Combine(Path.GetTempPath(), $"ctc-room-shot-{_tag}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Debug.Log($"[RoomSceneShot] {path}");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
