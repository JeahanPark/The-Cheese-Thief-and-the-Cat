using System.Collections.Generic;
using CTC.Runtime.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CTC.Runtime.UI.Popup
{
    /// <summary>
    /// 팝업의 단독 진입점 (2026-09-01, WES `PopupManager` 이식).
    ///
    /// **주소 = 클래스명**이다(`PopupStackLogic.AddressOf`). Addressables 그룹 `Popup`에
    /// 그 이름으로 등록돼 있어야 하고, 어긋나면 컴파일은 되고 런타임에 못 연다 —
    /// 그래서 실패를 반드시 에러로 남긴다(WES는 조용히 실패했다).
    ///
    /// 씬마다 캔버스가 다르므로 각 씬이 <see cref="InitializeForScene"/>로 자기 캔버스를 물려준다.
    /// 매니저 자신은 <c>DontDestroyOnLoad</c>로 씬을 넘어 산다.
    ///
    /// 스택이 비고 차는 것을 <see cref="UiFocus"/>에 그대로 알린다 — 안 하면 팝업 뒤에서
    /// 캐릭터가 움직인다(WES는 클릭만 막았다).
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        private const string POPUP_ROOT_NAME = "PopupRoot";

        public static PopupManager Instance { get; private set; }

        private readonly List<BasePopup> m_Opened = new List<BasePopup>();
        private readonly Dictionary<BasePopup, AsyncOperationHandle<GameObject>> m_Handles =
            new Dictionary<BasePopup, AsyncOperationHandle<GameObject>>();

        private Transform m_Root;

        public int OpenedCount => m_Opened.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 씬 진입 시 그 씬 캔버스를 물려준다. 넘어온 팝업이 남아 있으면 **여기서 닫는다** —
        /// NGO 씬 전환은 유니티 씬 언로드에 안 기대는 편이 안전하다(인스턴스가 살아남는다).
        /// </summary>
        public void InitializeForScene(Canvas _canvas)
        {
            CloseAll();

            if (_canvas == null)
            {
                GameDebug.LogWarning("[Popup] 캔버스가 없다 — 이 씬에서는 팝업을 못 연다");
                m_Root = null;
                return;
            }

            Transform existing = _canvas.transform.Find(POPUP_ROOT_NAME);
            if (existing != null)
            {
                m_Root = existing;
                return;
            }

            var go = new GameObject(POPUP_ROOT_NAME, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            m_Root = rect;
        }

        /// <summary>이미 떠 있으면 그것을 준다(중복 오픈 금지).</summary>
        public T FindOpen<T>() where T : BasePopup
        {
            for (int i = 0; i < m_Opened.Count; i++)
            {
                if (m_Opened[i] is T typed) return typed;
            }
            return null;
        }

        /// <summary>
        /// 연다. 주소는 클래스명. 실패하면 null이고 **에러가 남는다**.
        /// 데이터 주입은 돌려받은 뒤 메서드로 한다 — `Open&lt;T&gt;()`에 인자를 얹지 않는다(WES 규약).
        /// </summary>
        public T Open<T>() where T : BasePopup
        {
            T already = FindOpen<T>();
            if (!PopupStackLogic.CanOpen(already != null)) return already;

            if (m_Root == null)
            {
                GameDebug.LogError("[Popup] InitializeForScene을 안 했다 — 캔버스를 모른다");
                return null;
            }

            string address = PopupStackLogic.AddressOf(typeof(T));
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(address);
            GameObject prefab = handle.WaitForCompletion();
            if (prefab == null)
            {
                GameDebug.LogError($"[Popup] 프리팹을 못 읽었다: 주소 '{address}' — " +
                                   "Addressables 그룹 Popup에 클래스명과 같은 주소로 등록돼 있나?");
                Addressables.Release(handle);
                return null;
            }

            // **worldPositionStays: false** — UI는 이걸 빼면 RectTransform이 월드 기준으로
            // 재계산돼 앵커·크기가 틀어진다(2026-09-01 실측: 딤이 화면을 안 덮었다).
            GameObject go = Instantiate(prefab, m_Root, false);

            // 루트는 화면 전체를 덮는다 — 딤이 이 사각형을 채우기 때문이다.
            var rootRect = go.transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.localScale = Vector3.one;
            }
            var popup = go.GetComponent<T>();
            if (popup == null)
            {
                GameDebug.LogError($"[Popup] 프리팹에 {address} 컴포넌트가 없다");
                Destroy(go);
                Addressables.Release(handle);
                return null;
            }

            if (popup.Policy == BasePopup.OpenPolicy.CloseAllAndOpen) CloseAllExcept(popup);

            m_Opened.Add(popup);
            m_Handles[popup] = handle;
            go.transform.SetAsLastSibling();

            popup.NotifyOpened();
            UiFocus.SetModalOpen(PopupStackLogic.ModalOpen(m_Opened.Count));
            return popup;
        }

        public void Close(BasePopup _popup)
        {
            if (_popup == null) return;
            if (!m_Opened.Remove(_popup)) return;

            _popup.NotifyClosing();
            Destroy(_popup.gameObject);

            if (m_Handles.TryGetValue(_popup, out AsyncOperationHandle<GameObject> handle))
            {
                m_Handles.Remove(_popup);
                Addressables.Release(handle);   // WES는 안 놓아 줬다 — 여기서 놓는다
            }

            UiFocus.SetModalOpen(PopupStackLogic.ModalOpen(m_Opened.Count));
        }

        /// <summary>맨 위 하나를 닫는다 — ESC가 부른다. 비어 있으면 아무 일도 안 한다.</summary>
        public void CloseTop()
        {
            int top = PopupStackLogic.TopIndex(m_Opened.Count);
            if (top < 0) return;
            Close(m_Opened[top]);
        }

        public void CloseAll()
        {
            for (int i = m_Opened.Count - 1; i >= 0; i--) Close(m_Opened[i]);
        }

        private void CloseAllExcept(BasePopup _keep)
        {
            for (int i = m_Opened.Count - 1; i >= 0; i--)
            {
                if (m_Opened[i] != _keep) Close(m_Opened[i]);
            }
        }
    }
}
