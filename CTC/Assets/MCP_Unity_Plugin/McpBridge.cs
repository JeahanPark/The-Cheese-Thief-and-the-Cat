// McpBridge.cs
// Unity Editor Named Pipe 브릿지 코어 - 파이프 서버 생명주기, 요청 라우팅, 공용 헬퍼.
// 기능별 핸들러는 McpBridge/ 하위 partial 파일에 분리되어 있다.
// [InitializeOnLoad]로 자동 시작되며, Domain Reload 후에도 자동 재연결된다.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static partial class McpBridge
{
    private const string PIPE_NAME = "mcp-unity-bridge";

    // ---- 생명주기 상태 ----
    // Stopped → Starting → Running → Stopping → Stopped 순환.
    // Interlocked.CompareExchange로만 전이해 논리 상태와 자원 상태를 일치시킨다.
    private static class S
    {
        public const int Stopped  = 0;
        public const int Starting = 1;
        public const int Running  = 2;
        public const int Stopping = 3;
    }
    private static int m_State = S.Stopped;

    private static Thread m_ListenThread;
    // WaitForConnection 중인 파이프 참조. Stop()에서 더미 연결 후 Dispose에 사용.
    private static volatile NamedPipeServerStream m_CurrentPipe;
    private static readonly ConcurrentQueue<PendingRequest> m_PendingQueue = new ConcurrentQueue<PendingRequest>();

    // ---- 도메인 리로드 후 자동 재시작 ----

    static McpBridge()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;          // Editor 종료 시에도 반드시 Stop
        EditorApplication.delayCall += () => Start();
    }

    // ---- Tools 메뉴 ----

    [MenuItem("Tools/McpBridge/▶ 시작", priority = 1)]
    public static void MenuStart() => Start();

    [MenuItem("Tools/McpBridge/▶ 시작", isValidateFunction: true)]
    private static bool MenuStartValidate() => m_State == S.Stopped;

    [MenuItem("Tools/McpBridge/■ 중지", priority = 2)]
    public static void MenuStop() => Stop();

    [MenuItem("Tools/McpBridge/■ 중지", isValidateFunction: true)]
    private static bool MenuStopValidate() => m_State == S.Running;

    // ---- 서버 생명주기 ----

    private static void Start()
    {
        // MPPM 가상 플레이어(클론)는 메인 에디터와 같은 파이프 이름을 두고 경쟁한다.
        // 클론은 MCP 브릿지가 필요 없으므로 띄우지 않는다. (파이프 인스턴스 고갈·연결 경쟁 방지)
        if (IsCloneEditor()) return;

        // Stopped → Starting: 이 CAS가 성공해야만 기동 진행.
        if (Interlocked.CompareExchange(ref m_State, S.Starting, S.Stopped) != S.Stopped) return;

        try
        {
            // 중복 등록 원천 차단: -= 먼저 호출 후 +=
            EditorApplication.update -= ProcessQueue;
            EditorApplication.update += ProcessQueue;

            // [Fix #1] Running 상태를 스레드 시작 전에 설정하여 ListenLoop 레이스 방지
            Interlocked.Exchange(ref m_State, S.Running);

            m_ListenThread = new Thread(ListenLoop) { IsBackground = true, Name = "McpBridge" };
            m_ListenThread.Start();

            Debug.Log($"[McpBridge] Named Pipe 서버 시작됨 ({PIPE_NAME})");
        }
        catch (Exception e)
        {
            // [Fix #4] 기동 실패 시에도 CleanupResources 호출하여 부분 생성된 자원 정리
            CleanupResources();
            Interlocked.Exchange(ref m_State, S.Stopped);
            Debug.LogError($"[McpBridge] 서버 시작 실패: {e}");
        }
    }

    // MPPM(Multiplayer Play Mode) 가상 플레이어(클론) 여부를 판정한다.
    // 1차: 프로젝트 경로 — 클론은 항상 <원본>/Library/VP/ 하위 복제 프로젝트에서 실행된다 (MPPM 1.x/2.x 공통, 에디터 타임 안전).
    // 2차: MPPM 1.x 내부 타입 리플렉션 fallback.
    private static bool IsCloneEditor()
    {
        try
        {
            string dataPath = UnityEngine.Application.dataPath.Replace('\\', '/');
            if (dataPath.Contains("/Library/VP/")) return true;

            Type t = Type.GetType("Unity.Multiplayer.Playmode.VirtualProjects.Editor.VirtualProjectsEditor, Unity.Multiplayer.Playmode.VirtualProjects.Editor");
            if (t == null) return false; // MPPM 미설치 → 클론 개념 없음

            PropertyInfo prop = t.GetProperty("IsClone", BindingFlags.Public | BindingFlags.Static);
            if (prop == null) return false;

            return (bool)prop.GetValue(null);
        }
        catch
        {
            return false; // 판정 실패 시 안전하게 메인 취급(기존 동작 유지)
        }
    }

    private static void Stop()
    {
        // [Fix #2] Running 또는 Starting 상태 모두 정지 대상으로 처리
        int prev = Interlocked.CompareExchange(ref m_State, S.Stopping, S.Running);
        if (prev != S.Running)
        {
            prev = Interlocked.CompareExchange(ref m_State, S.Stopping, S.Starting);
            if (prev != S.Starting) return;
        }

        // WaitForConnection() 블로킹 해제: Dispose()만으로는 Mono에서 깨어나지 않음.
        // 더미 클라이언트를 연결해 WaitForConnection이 반환되도록 한 뒤 Dispose.
        try
        {
            using var dummy = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
            dummy.Connect(200);
        }
        catch { /* 파이프 없거나 연결 실패 — 무시하고 진행 */ }

        CleanupResources();

        // 자원 정리 완료 → Stopped
        Interlocked.Exchange(ref m_State, S.Stopped);
        Debug.Log("[McpBridge] Named Pipe 서버 중지됨");
    }

    // Stop()과 Start() 실패 시 공통 자원 정리
    private static void CleanupResources()
    {
        EditorApplication.update -= ProcessQueue;

        // [Fix #3] m_CurrentPipe를 로컬로 캡처 후 null 처리하여 참조 일관성 보장
        var pipe = m_CurrentPipe;
        m_CurrentPipe = null;
        try { pipe?.Dispose(); } catch { }

        // ListenThread가 루프를 빠져나올 때까지 최대 500ms 대기
        m_ListenThread?.Join(500);
        m_ListenThread = null;
    }

    private static void ListenLoop()
    {
        while (m_State == S.Running)
        {
            NamedPipeServerStream server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PIPE_NAME,
                    PipeDirection.InOut,
                    4,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None);

                // WaitForConnection 중인 파이프를 Stop()이 참조할 수 있도록 보관
                m_CurrentPipe = server;
                server.WaitForConnection();

                if (m_State != S.Running)
                {
                    server.Dispose();
                    break;
                }

                // [Fix #3] HandleClient에 넘긴 후 m_CurrentPipe 참조 해제
                // (다음 루프에서 새 파이프가 할당됨. Stop()이 이 파이프를 Dispose하지 않도록)
                m_CurrentPipe = null;
                var capturedServer = server;
                var t = new Thread(() => HandleClient(capturedServer)) { IsBackground = true };
                t.Start();
            }
            catch (ObjectDisposedException)
            {
                // Stop()이 Dispose한 경우 — 정상 종료 경로, 루프 탈출
                server = null; // 중복 Dispose 방지
                if (m_State != S.Running) break;
            }
            catch (Exception e)
            {
                // 파이프 생성/연결 중 예상치 못한 오류 — 로그 남기고 루프 유지
                if (m_State == S.Running)
                    Debug.LogWarning($"[McpBridge] ListenLoop 오류 (재시도): {e.GetType().Name}: {e.Message}");
                server?.Dispose();
                if (m_State != S.Running) break;

                // [Fix] 파이프 인스턴스 고갈("모든 파이프 인스턴스가 사용 중") 등 반복 오류 시
                // 백오프 없이 즉시 재시도하면 초당 수백 회 경고가 폭주한다. 1초 대기 후 재시도.
                Thread.Sleep(1000);
            }
        }
    }

    // ---- 클라이언트 처리 ----

    private static void HandleClient(NamedPipeServerStream _pipe)
    {
        using (_pipe)
        {
            try
            {
                using var reader = new StreamReader(_pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(_pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };

                string json = reader.ReadLine();
                if (string.IsNullOrEmpty(json))
                    return;

                var pending = new PendingRequest(json);
                m_PendingQueue.Enqueue(pending);
                pending.WaitForCompletion(10000);

                writer.WriteLine(pending.Response ?? BuildError("timeout"));
                writer.Flush();

                try { _pipe.WaitForPipeDrain(); } catch { }
            }
            catch (ThreadAbortException)
            {
                // 도메인 리로드 시 스레드 중단 — 정상 종료 경로
            }
            catch (Exception e)
            {
                if (m_State == S.Running)
                    Debug.LogError($"[McpBridge] 클라이언트 처리 오류: {e}");
            }
        }
    }

    // EditorApplication.update에서 메인 스레드로 커맨드 처리
    private static void ProcessQueue()
    {
        while (m_PendingQueue.TryDequeue(out var req))
        {
            try
            {
                req.Response = HandleRequest(req.Json);
            }
            catch (Exception e)
            {
                req.Response = BuildError(e.Message);
            }
            finally
            {
                req.Signal();
            }
        }
    }

    // ---- 요청 라우팅 ----

    private static string HandleRequest(string _json)
    {
        var req = JsonUtility.FromJson<BridgeRequest>(_json);
        if (req == null)
            return BuildError("Invalid JSON");
        if (string.IsNullOrEmpty(req.action))
            return BuildError("'action' is required");

        return req.action.ToLowerInvariant() switch
        {
            // Editor tools
            "u_editor_component"      => RouteComponent(req),       // McpBridgeComponents.cs
            "u_editor_gameobject"     => RouteGameObject(req),      // McpBridgeGameObject.cs
            "u_editor_set_transform"  => EditorSetTransform(req),   // McpBridgeRuntime.cs
            "u_editor_query"          => RouteQuery(req),           // McpBridgeGameObjectQuery.cs
            "u_editor_reference"      => RouteReference(req),       // McpBridgeReferences.cs
            "u_editor_prefab"         => InstantiatePrefab(req),    // McpBridgeInstantiate.cs
            "u_editor_ui_build"       => RouteUiBuild(req),         // McpBridgeUiBuild.cs
            "u_editor_scene"          => RouteScene(req),           // McpBridgeScene.cs
            "u_editor_asset"          => RouteAsset(req),           // McpBridgeAsset.cs
            "u_editor_tag"            => RouteTag(req),             // McpBridgeTagLayer.cs
            "u_editor_input"          => RouteInput(req),            // McpBridgeInputAction.cs
            "u_editor_layer"          => RouteLayer(req),           // McpBridgeTagLayer.cs
            // Play tools
            "u_play"                  => RoutePlay(req),            // McpBridgeRuntime.cs
            "u_play_control"          => PlayModeControl(req),      // McpBridgePlayMode.cs (backward compat)
            "u_play_set_transform"    => PlaySetTransform(req),     // McpBridgeRuntime.cs
            "u_play_click"            => ClickUi(req),              // McpBridgeRuntime.cs (backward compat)
            "u_play_invoke"           => InvokeRuntime(req),        // McpBridgeRuntime.cs (backward compat)
            // Common tools
            "u_console"               => ReadConsole(req),          // McpBridgeConsole.cs
            "u_screenshot"            => Screenshot(req),           // McpBridgeScreenshot.cs
            "u_editor_sceneview"      => RouteSceneView(req),       // McpBridgeSceneView.cs
            "u_editor_menu"           => ExecuteMenu(req),          // McpBridgeMenu.cs
            // MPPM QA tools
            "mppm_collect"            => RouteMppmQa(req),          // McpBridgeMppmQa.cs
            _                         => BuildError($"Unknown action: '{req.action}'")
        };
    }

    // ---- 공용 헬퍼 ----

    private static (GameObject go, GameObject prefabRoot) FindTarget(BridgeRequest _req)
    {
        bool isPath = !string.IsNullOrEmpty(_req.target) && _req.target.Contains('/');

        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_req.prefabPath);
            if (prefab == null)
                return (null, null);

            if (string.IsNullOrEmpty(_req.target) || _req.target == prefab.name)
                return (prefab, prefab);

            var found = isPath
                ? FindByPath(prefab.transform, _req.target)
                : FindInHierarchy(prefab.transform, _req.target);
            return (found, prefab);
        }

        foreach (var root in GetAllSceneRoots())
        {
            if (isPath)
            {
                var found = FindByPath(root.transform, _req.target);
                if (found != null) return (found, null);
                continue;
            }

            if (root.name == _req.target)
                return (root, null);

            var found2 = FindInHierarchy(root.transform, _req.target);
            if (found2 != null)
                return (found2, null);
        }

        return (null, null);
    }

    // '/' 구분 경로로 GameObject를 탐색한다. 예: "Parent/Child/Text"
    private static GameObject FindByPath(Transform _root, string _path)
    {
        var parts = _path.Split('/');
        Transform current = _root;

        // 첫 번째 파트가 루트 이름이면 건너뜀
        int startIndex = (current.name == parts[0]) ? 1 : 0;

        for (int i = startIndex; i < parts.Length; i++)
        {
            Transform next = null;
            foreach (Transform child in current)
            {
                if (child.name == parts[i]) { next = child; break; }
            }
            if (next == null) return null;
            current = next;
        }
        return current == _root && startIndex == 0 ? null : current.gameObject;
    }

    private static GameObject FindInHierarchy(Transform _parent, string _name)
    {
        foreach (Transform child in _parent)
        {
            if (child.name == _name)
                return child.gameObject;

            var found = FindInHierarchy(child, _name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static GameObject[] GetAllSceneRoots()
    {
        var result = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                result.AddRange(scene.GetRootGameObjects());
        }
        return result.ToArray();
    }

    private static void SaveTarget(GameObject _go, GameObject _prefabRoot)
    {
        if (_prefabRoot != null)
        {
            EditorUtility.SetDirty(_prefabRoot);
            PrefabUtility.SavePrefabAsset(_prefabRoot);
            AssetDatabase.SaveAssets();
        }
        else
        {
            EditorUtility.SetDirty(_go);
            EditorSceneManager.MarkSceneDirty(_go.scene);
        }
    }

    // 특정 프리팹이 Prefab Mode로 열려 있으면 메인 스테이지로 복귀해 저장 충돌을 방지한다.
    private static void ClosePrefabModeIfOpen(string _prefabPath)
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null) return;
        if (!string.IsNullOrEmpty(_prefabPath) &&
            !stage.assetPath.Equals(_prefabPath, System.StringComparison.OrdinalIgnoreCase)) return;
        UnityEditor.SceneManagement.StageUtility.GoToMainStage();
    }

    // 상속 체인(제네릭 베이스 포함)을 순회하여 필드를 찾는다.
    private static FieldInfo FindFieldInHierarchy(Type _type, string _fieldName)
    {
        var t = _type;
        while (t != null && t != typeof(object))
        {
            var field = t.GetField(_fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null)
                return field;
            t = t.BaseType;
        }
        return null;
    }

    // 상속 체인(제네릭 베이스 포함)을 순회하여 프로퍼티를 찾는다.
    private static PropertyInfo FindPropertyInHierarchy(Type _type, string _propertyName)
    {
        var t = _type;
        while (t != null && t != typeof(object))
        {
            var prop = t.GetProperty(_propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop != null)
                return prop;
            t = t.BaseType;
        }
        return null;
    }

    private static Type FindComponentType(string _typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = assembly.GetTypes().FirstOrDefault(x =>
                (x.Name == _typeName || x.FullName == _typeName) &&
                typeof(Component).IsAssignableFrom(x));
            if (t != null)
                return t;
        }
        return null;
    }

    private static string BuildSuccess(string _message) =>
        $"{{\"success\":true,\"message\":\"{Escape(_message)}\"}}";

    private static string BuildError(string _message) =>
        $"{{\"success\":false,\"message\":\"{Escape(_message)}\"}}";

    private static string Escape(string _s) =>
        _s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";

    // ---- DTO ----

    [Serializable]
    private class BridgeRequest
    {
        public string action;
        public string subAction;        // 통합 도구의 하위 액션 (예: "add", "delete", "find")
        public string target;
        public string prefabPath;
        public string parentPrefabPath;
        public string componentType;
        public string propertyName;
        public string propertyValue;
        public string mappingsJson;
        public string gameObjectName;
        public string newParent;           // set_parent 대상 새 부모 이름
        public string listenerTarget;
        public string listenerComponent;
        public string methodName;
        public string assetPath;
        public string logType;          // u_console 필터: "error" | "warning" | "log" | "all"
        public int    maxCount;         // u_console / u_editor_query 최대 항목 수
        public string playModeAction;   // u_play_control: "enter" | "exit" | "status"
        public float  x;               // (레거시 호환)
        public float  y;
        public float  z;
        // set_transform 필드 (nullable은 Unity JsonUtility 미지원이므로 flag 방식 사용)
        public float  posX, posY, posZ;
        public float  rotX, rotY, rotZ;
        public float  scaleX, scaleY, scaleZ;
        public bool   hasPosX, hasPosY, hasPosZ;
        public bool   hasRotX, hasRotY, hasRotZ;
        public bool   hasScaleX, hasScaleY, hasScaleZ;
        public string args;            // u_play_invoke 메서드 인자 (쉼표 구분)
        public string scenePath;       // u_editor_scene 씬 경로
        public string searchQuery;     // u_editor_query / u_editor_asset 검색어
        public string filter;          // u_editor_asset AssetDatabase 필터
        public string folder;          // u_editor_asset 검색 폴더
        public string tagName;         // u_editor_tag
        public string layerName;       // u_editor_layer
        public int    layerIndex;      // u_editor_layer 레이어 인덱스 (8-31)
        public string screenshotPath;  // u_screenshot 저장 경로
        public string actionMap;       // u_editor_input 액션맵 이름
        public string actionName;      // u_editor_input 액션 이름
        public string actionType;      // u_editor_input 액션 타입 (Button, Value, PassThrough)
        public string bindingPath;     // u_editor_input 바인딩 경로 (예: "<Keyboard>/1")
        public string view;            // u_editor_sceneview preset 시점 (top/front/side/persp)
        public string angle;           // u_editor_sceneview focus 각도 (top/front/side/persp)
        public float  size;            // u_editor_sceneview preset size 명시값
        public bool   hasSize;         // u_editor_sceneview size 명시 여부
        public string menuPath;        // u_editor_menu 메뉴 경로 (예: "Tools/Map Generator/Bake NavMesh")
        // u_editor_ui_build (layout JSON → 프리팹 빌드)
        public string layoutJson;        // UI 계층 layout JSON 트리
        public string outputPrefabPath;  // 출력 프리팹 경로 (Assets/ 하위)
        public int    referenceWidth;    // 기준 해상도 가로 (0 = 1920 기본)
        public int    referenceHeight;   // 기준 해상도 세로 (0 = 1080 기본)
    }

    private class PendingRequest
    {
        private readonly ManualResetEventSlim m_Done = new ManualResetEventSlim(false);

        public string Json     { get; }
        public string Response { get; set; }

        public PendingRequest(string _json) { Json = _json; }

        public void Signal()                        => m_Done.Set();
        public void WaitForCompletion(int _timeout) => m_Done.Wait(_timeout);
    }
}
