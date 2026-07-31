using System.Linq;
using System.Reflection;
using CTC.Runtime.Cameras;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Managers;
using CTC.Runtime.Map;
using CTC.Runtime.Match;
using CTC.Runtime.Networking;
using CTC.Runtime.Player;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CTC.Editor
{
    // SetupNetworkingScene의 일부 — 로비 캔버스·HUD·핫바 조립 (개편 4단계 분할, 2026-07-31)
    public static partial class SetupNetworkingScene
    {
        // 상점 핫바 슬롯 치수(하단 중앙)
        private const float SLOT_WIDTH = 132f;

        private const float SLOT_HEIGHT = 68f;

        private const float SLOT_GAP = 8f;

        // 로비 UGUI — Canvas + EventSystem(InputSystemUIInputModule) + 패널 + 플레인 UGUI 버튼.
        private static void BuildLobbyCanvas(LobbyController _lobby, MatchManager _match)
        {
            var canvasGo = new GameObject("LobbyCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand: 기준 해상도 레이아웃이 어떤 종횡비에서도 화면 밖으로 안 나가게 축소 매칭
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>(); // 레거시 StandaloneInputModule 금지

            // 중앙 패널 (반투명 다크)
            var panel = new GameObject("LobbyPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(480f, 620f); // 맵 선택 줄이 늘어 세로 확장
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);

            TMP_Text title = CreateTmpText("Title", panel.transform, "Cheese Thief Lobby",
                32f, TextAlignmentOptions.Center, new Vector2(0f, -20f), new Vector2(440f, 50f));
            title.fontStyle = FontStyles.Bold;

            TMP_Text playerList = CreateTmpText("PlayerList", panel.transform, string.Empty,
                22f, TextAlignmentOptions.TopLeft, new Vector2(0f, -90f), new Vector2(420f, 180f));

            // 맵 선택 — 방장만 조작. 버튼 수는 MapLayoutPaths와 같다(비어 있으면 LobbyUI가 숨긴다)
            TMP_Text mapLabel = CreateTmpText("MapLabel", panel.transform, "맵",
                22f, TextAlignmentOptions.Center, new Vector2(0f, -286f), new Vector2(420f, 30f));
            // 라벨은 런타임에 LobbyUI가 MapLayout.DisplayName으로 덮어쓴다 — 여기선 자리만 잡는다.
            // 폭(186)+간격(8)에 맞춰 배치해야 두 버튼이 겹치지 않는다.
            var mapButtons = new Object[MapLayoutPaths.Length];
            const float MAP_BUTTON_WIDTH = 186f;
            for (int i = 0; i < MapLayoutPaths.Length; i++)
            {
                float x = (i - (MapLayoutPaths.Length - 1) * 0.5f) * (MAP_BUTTON_WIDTH + 8f);
                mapButtons[i] = CreateMuipButton(panel.transform, $"MapButton{i + 1}",
                    "-", new Vector2(x, -330f), MAP_BUTTON_WIDTH);
            }

            Button mouseButton = CreateMuipButton(panel.transform, "MouseButton", "Play as Mouse", new Vector2(0f, -400f));
            Button catButton = CreateMuipButton(panel.transform, "CatButton", "Play as Cat", new Vector2(0f, -470f));
            Button startButton = CreateMuipButton(panel.transform, "StartButton", "Start Game", new Vector2(0f, -550f));

            var ui = canvasGo.AddComponent<LobbyUI>();
            SetRef(ui, "m_Lobby", _lobby);
            SetRef(ui, "m_Panel", panel);
            SetRef(ui, "m_PlayerListText", playerList);
            SetRef(ui, "m_MouseButton", mouseButton);
            SetRef(ui, "m_CatButton", catButton);
            SetRef(ui, "m_StartButton", startButton);
            SetRef(ui, "m_MapLabel", mapLabel);
            SetRefArray(ui, "m_MapButtons", mapButtons);

            // 치즈 잔액 HUD (우상단, 게임 시작 후 WalletHud가 표시 제어)
            var hudTextGo = new GameObject("WalletText");
            hudTextGo.transform.SetParent(canvasGo.transform, false);
            var hudText = hudTextGo.AddComponent<TextMeshProUGUI>();
            hudText.text = "Cheese: 0";
            hudText.fontSize = 32f;
            hudText.alignment = TextAlignmentOptions.TopRight;
            var hudRect = hudTextGo.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(1f, 1f);
            hudRect.anchorMax = new Vector2(1f, 1f);
            hudRect.pivot = new Vector2(1f, 1f);
            hudRect.anchoredPosition = new Vector2(-24f, -24f);
            hudRect.sizeDelta = new Vector2(320f, 48f);
            hudTextGo.SetActive(false); // WalletHud가 게임 시작 시 켬

            var walletHud = canvasGo.AddComponent<WalletHud>();
            SetRef(walletHud, "m_Text", hudText); // 로컬 플레이어는 LocalPlayerBinder 스트림에서 받는다

            // 전투 HUD (좌상단 아래쪽, CombatHud가 게임 시작 시 켬)
            var combatTextGo = new GameObject("CombatText");
            combatTextGo.transform.SetParent(canvasGo.transform, false);
            var combatText = combatTextGo.AddComponent<TextMeshProUGUI>();
            combatText.text = "HP -   CAT -";
            combatText.fontSize = 32f;
            combatText.alignment = TextAlignmentOptions.TopLeft;
            var combatRect = combatTextGo.GetComponent<RectTransform>();
            combatRect.anchorMin = new Vector2(0f, 1f);
            combatRect.anchorMax = new Vector2(0f, 1f);
            combatRect.pivot = new Vector2(0f, 1f);
            combatRect.anchoredPosition = new Vector2(24f, -110f); // ConnectionUI(IMGUI) 아래
            combatRect.sizeDelta = new Vector2(480f, 48f);
            combatTextGo.SetActive(false);

            var combatHud = canvasGo.AddComponent<CombatHud>();
            SetRef(combatHud, "m_Lobby", _lobby);
            SetRef(combatHud, "m_Text", combatText);

            // 상점 핫바 (하단 중앙 상시 표시, 숫자키 즉시 구매 — UX 개정 2026-07-27)
            var hotbarRoot = new GameObject("ShopHotbar", typeof(RectTransform));
            hotbarRoot.transform.SetParent(canvasGo.transform, false);
            var hotbarRt = (RectTransform)hotbarRoot.transform;
            hotbarRt.anchorMin = hotbarRt.anchorMax = new Vector2(0.5f, 0f);
            hotbarRt.pivot = new Vector2(0.5f, 0f);
            hotbarRt.anchoredPosition = new Vector2(0f, 28f);
            hotbarRt.sizeDelta = new Vector2(SLOT_WIDTH * ShopLogic.HOTBAR_SLOTS + SLOT_GAP * 4f, SLOT_HEIGHT);

            var slotRoots = new Object[ShopLogic.HOTBAR_SLOTS];
            var slotLabels = new Object[ShopLogic.HOTBAR_SLOTS];
            var slotCosts = new Object[ShopLogic.HOTBAR_SLOTS];
            float totalWidth = SLOT_WIDTH * ShopLogic.HOTBAR_SLOTS + SLOT_GAP * (ShopLogic.HOTBAR_SLOTS - 1);
            for (int i = 0; i < ShopLogic.HOTBAR_SLOTS; i++)
            {
                float x = -totalWidth * 0.5f + SLOT_WIDTH * 0.5f + i * (SLOT_WIDTH + SLOT_GAP);
                var slot = new GameObject($"Slot{i + 1}", typeof(RectTransform));
                slot.transform.SetParent(hotbarRoot.transform, false);
                var slotRt = (RectTransform)slot.transform;
                slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = new Vector2(x, 0f);
                slotRt.sizeDelta = new Vector2(SLOT_WIDTH, SLOT_HEIGHT);
                var slotBg = slot.AddComponent<Image>();
                slotBg.color = new Color(0.07f, 0.055f, 0.04f, 0.82f);

                slotLabels[i] = CreateTmpText($"Name{i + 1}", slot.transform, "-",
                    22f, TextAlignmentOptions.Center, new Vector2(0f, -6f), new Vector2(SLOT_WIDTH - 8f, 28f));
                slotCosts[i] = CreateTmpText($"Cost{i + 1}", slot.transform, "-",
                    20f, TextAlignmentOptions.Center, new Vector2(0f, -34f), new Vector2(SLOT_WIDTH - 8f, 26f));
                slotRoots[i] = slot;
            }

            TMP_Text toastText = CreateTmpText("ShopToast", hotbarRoot.transform, "",
                26f, TextAlignmentOptions.Center, new Vector2(0f, 52f), new Vector2(640f, 36f));
            toastText.gameObject.SetActive(false);
            hotbarRoot.SetActive(false); // ShopHotbar가 게임 시작 시 켬

            var hotbar = canvasGo.AddComponent<ShopHotbar>();
            SetRef(hotbar, "m_Lobby", _lobby);
            SetRef(hotbar, "m_Root", hotbarRoot);
            SetRefArray(hotbar, "m_Slots", slotRoots);
            SetRefArray(hotbar, "m_SlotLabels", slotLabels);
            SetRefArray(hotbar, "m_SlotCosts", slotCosts);
            SetRef(hotbar, "m_Toast", toastText);
            SetRef(hotbar, "m_GhostMaterialSource", CreateGhostMaterialAsset());

            // ESC 종료 — 빌드에는 창을 닫는 것 말고 나가는 방법이 없었다.
            // 배치 중일 땐 ESC가 배치 취소라 핫바를 참조해 구분한다.
            var quitGo = new GameObject("QuitOnEscape");
            var quit = quitGo.AddComponent<CTC.Runtime.Core.QuitOnEscape>();
            SetRef(quit, "m_Hotbar", hotbar);

            // 결과 패널 (매치 종료 시 ResultHud가 표시)
            var resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
            resultPanel.transform.SetParent(canvasGo.transform, false);
            var resultRt = (RectTransform)resultPanel.transform;
            resultRt.anchorMin = resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.pivot = new Vector2(0.5f, 0.5f);
            resultRt.sizeDelta = new Vector2(520f, 260f);
            var resultBg = resultPanel.AddComponent<Image>();
            resultBg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            TMP_Text resultText = CreateTmpText("ResultText", resultPanel.transform, "-",
                44f, TextAlignmentOptions.Center, new Vector2(0f, -40f), new Vector2(480f, 70f));
            resultText.fontStyle = FontStyles.Bold;

            Button restartButton = CreateMuipButton(resultPanel.transform, "RestartButton", "Restart",
                new Vector2(0f, -170f));
            resultPanel.SetActive(false);

            var resultHud = canvasGo.AddComponent<ResultHud>();
            SetRef(resultHud, "m_Match", _match);
            SetRef(resultHud, "m_Panel", resultPanel);
            SetRef(resultHud, "m_Text", resultText);
            SetRef(resultHud, "m_RestartButton", restartButton);

            // 조작키 + 핫바 항목 설명 (좌하단 — 역할별, ControlsHud가 게임 중 표시).
            // 쥐는 6항목이라 세로가 길다 — 높이를 넉넉히 잡는다.
            var controlsTextGo = new GameObject("ControlsText");
            controlsTextGo.transform.SetParent(canvasGo.transform, false);
            var controlsText = controlsTextGo.AddComponent<TextMeshProUGUI>();
            controlsText.text = string.Empty;
            controlsText.fontSize = 19f;
            controlsText.lineSpacing = 12f;
            controlsText.alignment = TextAlignmentOptions.BottomLeft;
            controlsText.color = new Color(1f, 1f, 1f, 0.85f);
            var controlsRect = controlsTextGo.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(0f, 0f);
            controlsRect.pivot = new Vector2(0f, 0f);
            controlsRect.anchoredPosition = new Vector2(24f, 20f);
            controlsRect.sizeDelta = new Vector2(900f, 220f);
            controlsTextGo.SetActive(false);

            var controlsHud = canvasGo.AddComponent<ControlsHud>();
            SetRef(controlsHud, "m_Lobby", _lobby);
            SetRef(controlsHud, "m_Text", controlsText);

            // 맵 설명 (좌하단 — 로비에서만. 게임이 시작되면 같은 자리를 조작키가 채운다)
            var mapInfoGo = new GameObject("MapInfoText");
            mapInfoGo.transform.SetParent(canvasGo.transform, false);
            var mapInfoText = mapInfoGo.AddComponent<TextMeshProUGUI>();
            mapInfoText.text = string.Empty;
            mapInfoText.fontSize = 20f;
            mapInfoText.alignment = TextAlignmentOptions.BottomLeft;
            mapInfoText.lineSpacing = 8f;
            var mapInfoRect = mapInfoGo.GetComponent<RectTransform>();
            mapInfoRect.anchorMin = mapInfoRect.anchorMax = new Vector2(0f, 0f);
            mapInfoRect.pivot = new Vector2(0f, 0f);
            mapInfoRect.anchoredPosition = new Vector2(24f, 20f);
            mapInfoRect.sizeDelta = new Vector2(560f, 130f);
            mapInfoGo.SetActive(false);

            var mapInfoHud = canvasGo.AddComponent<MapInfoHud>();
            SetRef(mapInfoHud, "m_Lobby", _lobby);
            SetRef(mapInfoHud, "m_Text", mapInfoText);

            // 매치 타이머 (상단 중앙 — v3 안전 타이머)
            var timerTextGo = new GameObject("TimerText");
            timerTextGo.transform.SetParent(canvasGo.transform, false);
            var timerText = timerTextGo.AddComponent<TextMeshProUGUI>();
            timerText.text = "10:00";
            timerText.fontSize = 40f;
            timerText.alignment = TextAlignmentOptions.Top;
            var timerRect = timerTextGo.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0f, -16f);
            timerRect.sizeDelta = new Vector2(200f, 52f);
            timerTextGo.SetActive(false);

            var timerHud = canvasGo.AddComponent<TimerHud>();
            SetRef(timerHud, "m_Lobby", _lobby);
            SetRef(timerHud, "m_Match", _match);
            SetRef(timerHud, "m_Text", timerText);

            // 인게임 BGM (게임 시작 시 루프 재생 — BgmPlayer가 제어)
            var bgmGo = new GameObject("BgmPlayer");
            var bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/bgm_ingame.ogg");
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = 0.35f;
            bgmSource.spatialBlend = 0f; // 2D — 거리 감쇠 없음
            var bgm = bgmGo.AddComponent<CTC.Runtime.Audio.BgmPlayer>();
            SetRef(bgm, "m_Lobby", _lobby);
            SetRef(bgm, "m_Source", bgmSource);

            // 로비 BGM — 게임이 안 도는 동안(접속 전·로비) 루프
            var lobbyBgmGo = new GameObject("LobbyBgmPlayer");
            var lobbyBgmSource = lobbyBgmGo.AddComponent<AudioSource>();
            lobbyBgmSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/bgm_lobby.ogg");
            lobbyBgmSource.loop = true;
            lobbyBgmSource.playOnAwake = false;
            lobbyBgmSource.volume = 0.35f;
            lobbyBgmSource.spatialBlend = 0f;
            var lobbyBgm = lobbyBgmGo.AddComponent<CTC.Runtime.Audio.BgmPlayer>();
            SetRef(lobbyBgm, "m_Lobby", _lobby);
            SetRef(lobbyBgm, "m_Source", lobbyBgmSource);
            SetInt(lobbyBgm, "m_PlayWhenGameStopped", 1);

            // 부엌 앰비언스 — BGM과 동일 조건(게임 중 루프), 볼륨만 낮게
            var ambGo = new GameObject("AmbiencePlayer");
            var ambSource = ambGo.AddComponent<AudioSource>();
            ambSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/amb_kitchen.ogg");
            ambSource.loop = true;
            ambSource.playOnAwake = false;
            ambSource.volume = 0.15f;
            ambSource.spatialBlend = 0f;
            var amb = ambGo.AddComponent<CTC.Runtime.Audio.BgmPlayer>();
            SetRef(amb, "m_Lobby", _lobby);
            SetRef(amb, "m_Source", ambSource);

            // 효과음 허브 — m_Clips는 SfxId enum 순서와 1:1
            var sfxGo = new GameObject("SfxPlayer");
            var sfxPlayer = sfxGo.AddComponent<CTC.Runtime.Audio.SfxPlayer>();
            SetRefArray(sfxPlayer, "m_Clips", new Object[]
            {
                LoadClip("Combat/sfx_popgun_fire"),      // PopgunFire
                LoadClip("Combat/sfx_slingshot_fire"),   // SlingshotFire
                LoadClip("Combat/sfx_machinegun_fire"),  // MachinegunFire
                LoadClip("Combat/sfx_pounce_swing"),     // PounceSwing
                LoadClip("Combat/sfx_mouse_hurt"),       // MouseHurt
                LoadClip("Combat/sfx_cat_hurt"),         // CatHurt
                LoadClip("Combat/sfx_mouse_death"),      // MouseDeath
                LoadClip("Combat/sfx_cat_death"),        // CatDeath
                LoadClip("Economy/sfx_fortress_hit"),    // FortressHit
                LoadClip("Economy/sfx_fortress_break"),  // FortressBreak
                LoadClip("Economy/sfx_fortress_place"),  // FortressPlace
                LoadClip("Economy/sfx_decoy_place"),     // DecoyPlace
                LoadClip("UI/sfx_match_start"),          // MatchStart
                LoadClip("UI/jingle_mouse_win"),         // MouseWin
                LoadClip("UI/jingle_cat_win"),           // CatWin
                LoadClip("Movement/sfx_mouse_dash"),     // MouseDash
                LoadClip("Economy/sfx_cheese_tick"),     // CheeseTick
                LoadClip("Economy/sfx_heal_tick"),       // HealTick
                LoadClip("Economy/sfx_shop_buy"),        // ShopBuy
                LoadClip("Economy/sfx_shop_deny"),       // ShopDeny
                LoadClip("UI/sfx_ui_click"),             // UiClick
                LoadClip("UI/sfx_timer_warn"),           // TimerWarn
                // 파밍 짤랑음과 구분되는 별도 음원 — 나무 쿵 + 짧은 동전 챙(생산 완료감).
                // 고양이가 소리만 듣고 "쥐가 파밍 중"과 "창고가 돌고 있다"를 구별해야 한다.
                LoadClip("Economy/sfx_warehouse_tick"),  // WarehouseTick
                LoadClip("Economy/sfx_cheese_depleted"), // CheeseDepleted
            });

            var matchSfx = sfxGo.AddComponent<CTC.Runtime.Audio.MatchSfx>();
            SetRef(matchSfx, "m_Lobby", _lobby);
            SetRef(matchSfx, "m_Match", _match);

            // 인게임 디버그 콘솔(yasirkula, MIT) — 빌드에서 로그 확인용. 기본 최소화, ` 키 토글.
            // 원격 테스터가 [SteamLobby]/[Lobby] 로그를 화면에서 바로 볼 수 있어야 원인 특정이 된다.
            var consolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.yasirkula.ingamedebugconsole/Plugins/IngameDebugConsole/IngameDebugConsole.prefab");
            if (consolePrefab != null)
            {
                var console = (GameObject)PrefabUtility.InstantiatePrefab(consolePrefab);
                console.name = "IngameDebugConsole";
                foreach (MonoBehaviour mb in console.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType().Name != "DebugLogManager") continue;
                    var so = new SerializedObject(mb);
                    SerializedProperty toggle = so.FindProperty("toggleWithKey");
                    if (toggle != null) toggle.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // 개발용 빌드에서만 켠다 — GameDebug(DEVELOPMENT_BUILD)와 같은 기준
                var gateGo = new GameObject("DebugConsoleGate");
                var gate = gateGo.AddComponent<CTC.Runtime.Core.DebugConsoleGate>();
                SetRef(gate, "m_Console", console);
            }
            else
            {
                Debug.LogWarning("[Setup] IngameDebugConsole 프리팹 없음 — 패키지 확인");
            }

            // 밸런스 데이터 로거(스펙 v3.3) — 매치 종료 시 persistentDataPath/MatchStats/*.json
            var statsGo = new GameObject("MatchStatsRecorder");
            var stats = statsGo.AddComponent<MatchStatsRecorder>();
            SetRef(stats, "m_Lobby", _lobby);
            SetRef(stats, "m_Match", _match);
        }

        private static TMP_Text CreateTmpText(
            string _name, Transform _parent, string _text,
            float _fontSize, TextAlignmentOptions _alignment, Vector2 _anchoredPos, Vector2 _size)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = _anchoredPos;
            rt.sizeDelta = _size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = _text;
            tmp.fontSize = _fontSize;
            tmp.alignment = _alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        // 표준 UGUI 버튼(흰 배경 + TMP 라벨). MUIP 버튼은 런타임 상태 초기화가 세션에 따라
        // 깨지는 문제(비가시 버튼)와 한글 글리프·다중 라벨 이슈가 반복돼 그레이박스 단계에선
        // 플레인 UGUI로 통일 — 본 UI 패스에서 디자인 버튼으로 교체 전제.
        private static Button CreateMuipButton(
            Transform _parent, string _name, string _label, Vector2 _anchoredPos, float _width = 380f)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = _anchoredPos;
            rt.sizeDelta = new Vector2(_width, 56f);

            var bg = go.AddComponent<Image>();
            bg.color = Color.white;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = _label;
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.12f, 0.13f, 0.16f);
            tmp.raycastTarget = false;

            var button = go.AddComponent<Button>();
            button.targetGraphic = bg; // 기본 ColorTint 전환 사용
            go.AddComponent<CTC.Runtime.Audio.UiClickSfx>(); // 공통 클릭음(2D)
            return button;
        }
    }
}
