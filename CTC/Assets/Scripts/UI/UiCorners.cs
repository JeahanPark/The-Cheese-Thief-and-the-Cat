using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// UI 둥근 모서리를 입히는 Worker(2026-09-23). 씬이 뜰 때마다 모든 캔버스를 한 번 훑어
    /// 「색만 칠한 사각형」 Image에 둥근 9-slice 스프라이트를 씌운다. 판정은 <see cref="UiCornerLogic"/>.
    ///
    /// 생성기(인트로·로비·대기실·게임 캔버스)를 하나도 안 고치고 씬도 다시 굽지 않는다 — 모양을 한 곳에서 준다.
    /// 씬이 뜬 **뒤에** 만들어지는 UI(로딩창 등)는 만든 쪽이 <see cref="ApplyTo"/>를 부른다.
    /// 템플릿에서 복제하는 줄(상점 목록 등)은 템플릿이 이미 둥글어 저절로 따라온다.
    ///
    /// 미니맵은 **각진 채로** 둔다 — 지도가 정사각형이라 틀만 둥글면 모서리가 비친다.
    /// </summary>
    public static class UiCorners
    {
        private const string SPRITE_PATH = "UI/T_RoundRect";   // Resources — Tools/CTC/UI/Build Round Rect Sprite
        private const string KEEP_SQUARE_BRANCH = "Minimap";

        private static Sprite s_Sprite;
        private static bool s_Hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!s_Hooked)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                s_Hooked = true;
            }
            ApplyAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // 도메인 리로드를 끈 에디터에서 구독이 두 번 걸리지 않게
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_Hooked = false;
            s_Sprite = null;
        }

        private static void OnSceneLoaded(Scene _scene, LoadSceneMode _mode) => ApplyAll();

        /// <summary>지금 떠 있는 모든 루트 캔버스를 훑는다. 둥글린 칸 수를 돌려준다.</summary>
        public static int ApplyAll()
        {
            // 레이아웃 그룹이 정하는 칸 크기가 아직 0일 수 있다 — 한 번 계산시키고 잰다
            Canvas.ForceUpdateCanvases();

            int rounded = 0;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.isRootCanvas) rounded += ApplyTo(canvas.transform);
            }
            return rounded;
        }

        /// <summary>그 루트 아래의 Image를 판정대로 둥글린다. 둥글린 칸 수를 돌려준다.</summary>
        public static int ApplyTo(Transform _root)
        {
            if (_root == null) return 0;
            Sprite sprite = LoadSprite();
            if (sprite == null) return 0;

            Vector2 canvasSize = CanvasSizeOf(_root);
            int rounded = 0;
            foreach (Image image in _root.GetComponentsInChildren<Image>(true))
            {
                if (image == null || InKeepSquareBranch(image.transform, _root)) continue;

                Rect rect = image.rectTransform.rect;
                bool round = UiCornerLogic.ShouldRound(image.sprite != null, image.type == Image.Type.Simple,
                    image.color.a, rect.width, rect.height, canvasSize.x, canvasSize.y);
                if (!round) continue;

                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.pixelsPerUnitMultiplier = UiCornerLogic.PixelsPerUnitMultiplier(
                    UiCornerLogic.CornerPx(rect.width, rect.height));
                rounded++;
            }
            return rounded;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA: 지금 둥근 스프라이트를 쓰는 Image 수 · 스프라이트가 로드됐는가.</summary>
        public static string DevDescribe()
        {
            Sprite sprite = LoadSprite();
            int count = 0;
            if (sprite != null)
            {
                foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (image != null && image.sprite == sprite) count++;
                }
            }
            return $"sprite={(sprite != null ? 1 : 0)} rounded={count}";
        }
#endif

        private static Sprite LoadSprite()
        {
            if (s_Sprite == null) s_Sprite = Resources.Load<Sprite>(SPRITE_PATH);
            return s_Sprite;
        }

        private static Vector2 CanvasSizeOf(Transform _root)
        {
            var rt = _root as RectTransform;
            return rt != null ? rt.rect.size : Vector2.zero;
        }

        private static bool InKeepSquareBranch(Transform _t, Transform _root)
        {
            for (Transform t = _t; t != null && t != _root; t = t.parent)
            {
                if (t.name == KEEP_SQUARE_BRANCH) return true;
            }
            return false;
        }
    }
}
