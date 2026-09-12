using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CTC.Editor
{
    /// <summary>
    /// **이름 없는 씬의 「수정됨」 표시만** 지운다. 저장하지 않는다.
    ///
    /// 왜 있는가(2026-09-12 실측, 하루에 네 번 물렸다): 유니티는 여러 경로에서 「수정된 씬을
    /// 저장하라」고 요구한다 — 테스트 러너, 씬 열기(`McpBridgeScene.cs:32`), 플레이 진입
    /// (`McpBridgePlayMode.cs:23`의 `SaveOpenScenes`), 프리팹 인스턴스화
    /// (`McpBridgeInstantiate.cs:77`). 열린 씬에 **경로가 없으면** 유니티가 「어디에 저장할까」를
    /// 묻는 **네이티브 파일 대화상자**를 띄우고, 그 대화상자가 **에디터 업데이트 루프를 멈춘다.**
    /// 그러면 MCP 호출이 전부 타임아웃해서 「브리지가 죽었다」와 증상이 똑같아 보이고,
    /// 사람이 손으로 닫기 전까지 아무것도 못 한다.
    ///
    /// **경로가 있는 씬은 건드리지 않는다.** 남의 미저장 작업을 말없이 버리면 안 되기 때문이다.
    /// 경로가 없는 씬은 아직 아무 데도 없는 임시 씬이라 버릴 것이 없다.
    /// </summary>
    public static class SceneDirtGuard
    {
        /// <summary>
        /// 경로 없는 활성 씬의 더티 플래그를 지운다. 지웠으면 true.
        ///
        /// `ClearSceneDirtiness`는 6000.5에서 public이 아니라 리플렉션으로 부른다. 못 찾으면
        /// 경고만 남기고 넘어간다 — 이건 편의 장치이고, 대화상자가 떠도
        /// `tools/dismiss-save-scene.ps1`이 밖에서 닫는다. 하던 일을 막을 이유는 없다.
        /// </summary>
        public static bool DropUntitledDirtiness()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.isDirty) return false;
            if (!string.IsNullOrEmpty(active.path)) return false;

            MethodInfo clear = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Scene) }, null);

            if (clear == null)
            {
                Debug.LogWarning(
                    "[SceneDirtGuard] ClearSceneDirtiness를 못 찾았다 — 저장 대화상자가 뜰 수 있다. " +
                    "뜨면 tools/dismiss-save-scene.ps1이 닫는다.");
                return false;
            }

            clear.Invoke(null, new object[] { active });
            Debug.Log("[SceneDirtGuard] 이름 없는 씬의 수정 표시를 지웠다 — 저장 대화상자가 뜨지 않게.");
            return true;
        }

        /// <summary>
        /// 손으로 부르는 입구. **씬을 열거나 플레이를 누르기 직전에** 한 번 친다.
        /// MCP로는 `u_editor_scene`·`u_play`가 저장을 요구하므로 그 앞에 이걸 먼저 부른다.
        /// </summary>
        [MenuItem("Tools/CTC/Dev/이름 없는 씬 더티 해제")]
        public static void DropMenu()
        {
            if (!DropUntitledDirtiness())
                Debug.Log("[SceneDirtGuard] 지울 것이 없다 — 씬이 깨끗하거나 경로가 있다.");
        }
    }
}
