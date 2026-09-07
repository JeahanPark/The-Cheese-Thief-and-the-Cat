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
using CTC.Runtime.UI;
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
        // 상점 핫바 슬롯 치수(하단 중앙) — 반줄 힌트가 들어가며 세로 확장(HUD 개편 2026-08-15)
        private const float SLOT_WIDTH = 132f;

        private const float SLOT_HEIGHT = 84f;

        private const float SLOT_GAP = 8f;

        // 능력 칸(핫바 아래 줄, 하단 중앙 — 2026-08-17). 핫바는 이만큼 위로 올라간다.
        // 능력 칸은 핫바 칸과 같은 크기·간격·바탕 — 한 벌로 보이게(2026-08-17)
        private const int ABILITY_SLOTS = 2;
        private const float ABILITY_Y = 28f;
        private const float HOTBAR_Y = ABILITY_Y + SLOT_HEIGHT + 12f;

        // 로비 패널 치수. 안의 것은 전부 내용 폭 안에 들어와야 한다 —
        // 하나라도 이걸 넘으면 패널 밖으로 삐져나온다.
        // 맵 버튼 줄도 이 폭을 나눠 쓰므로 역할 버튼과 좌우가 딱 맞는다.
        private const float PANEL_WIDTH = 480f;
        private const float PANEL_CONTENT_WIDTH = 380f;

        // 로비 UGUI — Canvas + EventSystem(InputSystemUIInputModule) + 패널 + 플레인 UGUI 버튼.
        private static void BuildLobbyCanvas(LobbyController _lobby, MatchManager _match,
            CTC.Runtime.Match.PhaseDirector _phase, CTC.Runtime.Match.MatchSeries _series)
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
            panelRt.sizeDelta = new Vector2(PANEL_WIDTH, 690f); // 맵 선택·봇전 줄이 늘어 세로 확장
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = UiPalette.WithAlpha(UiPalette.Panel, 0.85f);

            TMP_Text title = CreateTmpText("Title", panel.transform, "CANADIAN MOUSE",
                32f, TextAlignmentOptions.Center, new Vector2(0f, -20f), new Vector2(440f, 50f));
            title.fontStyle = FontStyles.Bold;

            TMP_Text playerList = CreateTmpText("PlayerList", panel.transform, string.Empty,
                22f, TextAlignmentOptions.TopLeft, new Vector2(0f, -90f), new Vector2(420f, 180f));

            // 맵 선택 — 방장만 조작. 버튼 수는 MapLayoutPaths와 같다(비어 있으면 LobbyUI가 숨긴다)
            TMP_Text mapLabel = CreateTmpText("MapLabel", panel.transform, "맵",
                22f, TextAlignmentOptions.Center, new Vector2(0f, -286f), new Vector2(420f, 30f));
            // 라벨은 런타임에 LobbyUI가 MapLayout.DisplayName으로 덮어쓴다 — 여기선 자리만 잡는다.
            // **폭은 맵 개수에서 역산한다.** 고정 폭을 쓰면 맵이 늘어난 순간 버튼이 패널
            // 밖으로 삐져나온다(2026-08-11: 3개 × 186 + 간격 = 574 > 내용 폭 420).
            var mapButtons = new Object[MapLayoutPaths.Length];
            const float MAP_BUTTON_GAP = 8f;
            float mapButtonWidth =
                (PANEL_CONTENT_WIDTH - MAP_BUTTON_GAP * (MapLayoutPaths.Length - 1)) / MapLayoutPaths.Length;
            for (int i = 0; i < MapLayoutPaths.Length; i++)
            {
                float x = (i - (MapLayoutPaths.Length - 1) * 0.5f) * (mapButtonWidth + MAP_BUTTON_GAP);
                mapButtons[i] = CreatePanelButton(panel.transform, $"MapButton{i + 1}",
                    "맵 " + (i + 1), new Vector2(x, -330f), mapButtonWidth);
            }

            Button mouseButton = CreatePanelButton(panel.transform, "MouseButton", "쥐로 하기", new Vector2(0f, -400f));
            Button catButton = CreatePanelButton(panel.transform, "CatButton", "고양이로 하기", new Vector2(0f, -470f));
            Button startButton = CreatePanelButton(panel.transform, "StartButton", "게임 시작", new Vector2(0f, -550f));
            // 개발 빌드 전용 — 혼자 봇 상대 2:8. 배포 빌드에선 LobbyUI가 Awake에서 숨긴다.
            Button botMatchButton = CreatePanelButton(panel.transform, "BotMatchButton", "봇전 시작 (개발)", new Vector2(0f, -620f));

            var ui = canvasGo.AddComponent<LobbyUI>();
            SetRef(ui, "m_Lobby", _lobby);
            SetRef(ui, "m_Panel", panel);
            SetRef(ui, "m_PlayerListText", playerList);
            SetRef(ui, "m_MouseButton", mouseButton);
            SetRef(ui, "m_CatButton", catButton);
            SetRef(ui, "m_StartButton", startButton);
            SetRef(ui, "m_BotMatchButton", botMatchButton);
            SetRef(ui, "m_MapLabel", mapLabel);
            SetRefArray(ui, "m_MapButtons", mapButtons);

            // 우상단 상태 클러스터 — 구 WalletText(우상단)·CombatText(좌상단)를 대체(HUD 개편 2026-08-15)
            BuildStatusCluster(canvasGo, _lobby);

            // 상점 핫바 (하단 중앙 상시 표시, 숫자키 즉시 구매 — UX 개정 2026-07-27)
            var hotbarRoot = new GameObject("ShopHotbar", typeof(RectTransform));
            hotbarRoot.transform.SetParent(canvasGo.transform, false);
            var hotbarRt = (RectTransform)hotbarRoot.transform;
            hotbarRt.anchorMin = hotbarRt.anchorMax = new Vector2(0.5f, 0f);
            hotbarRt.pivot = new Vector2(0.5f, 0f);
            hotbarRt.anchoredPosition = new Vector2(0f, HOTBAR_Y);   // 아래 줄은 능력 칸

            // = BalanceInfo.TechSlotCap — 칸 수는 구조 제약이라 씬을 구울 때 고정
            int hotbarSlots = CTC.Runtime.Info.InfoManager.Instance.Balance.TechSlotCap;
            // 0칸으로 구우면 핫바가 통째로 사라지는데 씬은 멀쩡해 보인다 — 반쯤 구운 씬을 남기느니 여기서 끊는다
            if (hotbarSlots < 1)
            {
                throw new System.InvalidOperationException(
                    $"[SetupNetworkingScene] BalanceInfo.TechSlotCap={hotbarSlots} — 핫바를 만들 수 없다. BalanceInfo.csv 확인");
            }
            float totalWidth = SLOT_WIDTH * hotbarSlots + SLOT_GAP * (hotbarSlots - 1);
            hotbarRt.sizeDelta = new Vector2(totalWidth, SLOT_HEIGHT);

            var slotRoots = new Object[hotbarSlots];
            var slotLabels = new Object[hotbarSlots];
            var slotHints = new Object[hotbarSlots];
            var slotCosts = new Object[hotbarSlots];
            for (int i = 0; i < hotbarSlots; i++)
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
                slotBg.color = UiPalette.Slot;

                slotLabels[i] = CreateTmpText($"Name{i + 1}", slot.transform, "-",
                    22f, TextAlignmentOptions.Center, new Vector2(0f, -6f), new Vector2(SLOT_WIDTH - 8f, 26f));
                // 반줄 힌트 — "딱봤을 때 뭔지". 문구·색은 ShopHotbar가 채운다.
                slotHints[i] = CreateTmpText($"Hint{i + 1}", slot.transform, "",
                    12f, TextAlignmentOptions.Center, new Vector2(0f, -34f), new Vector2(SLOT_WIDTH - 8f, 18f));
                slotCosts[i] = CreateTmpText($"Cost{i + 1}", slot.transform, "-",
                    20f, TextAlignmentOptions.Center, new Vector2(0f, -54f), new Vector2(SLOT_WIDTH - 8f, 26f));
                slotRoots[i] = slot;
            }

            // 모드 줄(테크 v3 2026-09-07) — 지금 숫자키가 "산다"인지 "쓴다"인지. 문구는 ShopHotbar가 채운다.
            TMP_Text modeLabel = CreateTmpText("ModeLabel", hotbarRoot.transform, "",
                13f, TextAlignmentOptions.Center, new Vector2(0f, 34f), new Vector2(totalWidth, 18f));
            modeLabel.color = UiPalette.TextDim;

            // 토스트는 모드 줄 위로 — 겹치면 둘 다 안 읽힌다
            TMP_Text toastText = CreateTmpText("ShopToast", hotbarRoot.transform, "",
                26f, TextAlignmentOptions.Center, new Vector2(0f, 86f), new Vector2(640f, 36f));
            toastText.gameObject.SetActive(false);
            hotbarRoot.SetActive(false); // ShopHotbar가 게임 시작 시 켬

            var hotbar = canvasGo.AddComponent<ShopHotbar>();
            SetRef(hotbar, "m_Lobby", _lobby);
            SetRef(hotbar, "m_Root", hotbarRoot);
            SetRefArray(hotbar, "m_Slots", slotRoots);
            SetRefArray(hotbar, "m_SlotLabels", slotLabels);
            SetRefArray(hotbar, "m_SlotHints", slotHints);
            SetRefArray(hotbar, "m_SlotCosts", slotCosts);
            SetRef(hotbar, "m_ModeLabel", modeLabel);
            SetRef(hotbar, "m_Toast", toastText);
            SetRef(hotbar, "m_GhostMaterialSource", CreateGhostMaterialAsset());

            // ESC 단독 소유자 — 배치 취소 / 채팅 닫기 / 옵션 닫기 / 옵션 열기 순으로 하나만 처리한다.
            // (전에는 QuitOnEscape와 ShopHotbar가 같은 프레임에 ESC를 각자 읽어 경합했다)
            var escGo = new GameObject("EscapeRouter");
            var escRouter = escGo.AddComponent<CTC.Runtime.Core.EscapeRouter>();
            SetRef(escRouter, "m_Hotbar", hotbar);

            // 결과 패널 (매치 종료 시 ResultHud가 표시)
            var resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
            resultPanel.transform.SetParent(canvasGo.transform, false);
            var resultRt = (RectTransform)resultPanel.transform;
            resultRt.anchorMin = resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.pivot = new Vector2(0.5f, 0.5f);
            resultRt.sizeDelta = new Vector2(520f, 260f);
            var resultBg = resultPanel.AddComponent<Image>();
            resultBg.color = UiPalette.PanelSolid;

            TMP_Text resultText = CreateTmpText("ResultText", resultPanel.transform, "-",
                44f, TextAlignmentOptions.Center, new Vector2(0f, -40f), new Vector2(480f, 70f));
            resultText.fontStyle = FontStyles.Bold;

            Button restartButton = CreatePanelButton(resultPanel.transform, "RestartButton", "Restart",
                new Vector2(0f, -170f));
            resultPanel.SetActive(false);

            var resultHud = canvasGo.AddComponent<ResultHud>();
            SetRef(resultHud, "m_Match", _match);
            SetRef(resultHud, "m_Panel", resultPanel);
            SetRef(resultHud, "m_Text", resultText);
            SetRef(resultHud, "m_RestartButton", restartButton);

            // 능력 칸 (핫바 아래 줄, 하단 중앙 — 2026-08-17). 구 좌하단 특수키 칩(ControlsHud)을 대체.
            BuildAbilityBar(canvasGo, _lobby);

            // 맵 설명 (좌하단 — 로비에서만)
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
            // 2단계는 시계가 통째로 다르다 — 이 배선이 빠지면 깡패전 4분이 화면에 안 나온다
            SetRef(timerHud, "m_Phase", _phase);
            SetRef(timerHud, "m_Text", timerText);

            // 시계 바로 아래 한 줄 — 3판 시리즈의 판 번호와 점수. 단독 실행에선 안 뜬다.
            var seriesTextGo = new GameObject("SeriesText");
            seriesTextGo.transform.SetParent(canvasGo.transform, false);
            var seriesText = seriesTextGo.AddComponent<TextMeshProUGUI>();
            seriesText.text = "";
            seriesText.fontSize = 20f;
            seriesText.alignment = TextAlignmentOptions.Top;
            seriesText.color = UiPalette.TextDim;
            seriesText.raycastTarget = false;
            var seriesRect = seriesTextGo.GetComponent<RectTransform>();
            seriesRect.anchorMin = new Vector2(0.5f, 1f);
            seriesRect.anchorMax = new Vector2(0.5f, 1f);
            seriesRect.pivot = new Vector2(0.5f, 1f);
            seriesRect.anchoredPosition = new Vector2(0f, -66f);
            seriesRect.sizeDelta = new Vector2(420f, 30f);
            seriesTextGo.SetActive(false);

            var seriesView = canvasGo.AddComponent<MatchSeriesView>();
            SetRef(seriesView, "m_Series", _series);
            SetRef(seriesView, "m_Label", seriesText);

            // 배경음(2026-09-01) — 재생기는 씬을 넘어 하나뿐이다(BgmDirector).
            // 여기서는 "인게임 곡으로 바꿔라"만 말한다. 앞단 세 화면은 로비 곡을 이어서 튼다.
            SceneBgmBuilder.Attach(SceneBgmBuilder.INGAME_BGM);

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
            // 앰비언스는 배경음 쪽 — 계속 깔리는 분위기라 음악을 끄는 사람은 이것도 끄고 싶어 한다
            AddVolumeFollower(ambGo, CTC.Runtime.Audio.AudioVolumeFollower.Category.Bgm);

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
                null,                                    // 11 = DecoyPlace(미끼) 삭제 자리 — enum 값 1:1이라 비워 둔다
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
                LoadClip("Economy/sfx_alcove_hit"),      // AlcoveHit
                LoadClip("Economy/sfx_alcove_break"),    // AlcoveBreak
                // 강아지(v3.8 5번). 짖음이 강아지의 유일한 위치 정보라 제일 멀리 간다.
                LoadClip("Ai/sfx_dog_bark"),             // DogBark
                LoadClip("Ai/sfx_dog_bite"),             // DogBite
                LoadClip("Ai/sfx_dog_hurt"),             // DogHurt
                LoadClip("Ai/sfx_dog_death"),            // DogDeath
                // 벽 안에서 새는 낑낑·긁는 소리. 루프라 원샷 재생에는 안 쓰이고
                // AlcoveIdleSound가 AudioSource에 얹어 튼다.
                LoadClip("Ai/sfx_dog_alcove_idle"),      // DogAlcoveIdle
                LoadClip("Economy/sfx_box_hit"),         // BoxHit
                LoadClip("Economy/sfx_box_break"),       // BoxBreak
                LoadClip("UI/sfx_cat_advance"),          // CatAdvance
                LoadClip("Combat/sfx_cat_skill"),        // CatSkill
                LoadClip("Economy/sfx_shop_buy"),        // JobPickup (임시 — 상점 구매음 재사용)
                LoadClip("UI/sfx_cat_advance"),          // GangRise (임시 — 전직음 재사용)
            });

            var matchSfx = sfxGo.AddComponent<CTC.Runtime.Audio.MatchSfx>();
            SetRef(matchSfx, "m_Lobby", _lobby);
            SetRef(matchSfx, "m_Match", _match);

            // 인게임 디버그 콘솔(yasirkula, MIT) — 빌드에서 로그 확인용. ` · F2 토글.
            // 2026-08-20에 뺐다가 2026-08-24에 복구(사용자 결정 번복) — 스팀 접속 진단에서
            // "화면에서 바로 보는 로그"가 실제로 필요했다. 버블도 2026-08-24에 다시 켰다
            // (Always) — 로그 개수가 상시 보이는 그 작은 원이 테스터의 1차 신호등이다.
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
                    SerializedProperty popup = so.FindProperty("popupVisibility");
                    if (popup != null) popup.enumValueIndex = 0; // Always — 버블 상시 노출
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

            // FPS · 프레임 타임 (좌상단, ConnectionUI 오른쪽) — 개발용 빌드·에디터에서만 그린다
            new GameObject("FrameStatsOverlay").AddComponent<CTC.Runtime.Core.FrameStatsOverlay>();

            // 옵션창은 프리팹이다(2026-09-01) — 씬은 팝업 매니저에 캔버스만 물려준다.
            PopupSceneBinder.Attach(canvasGo.GetComponent<Canvas>());
            BuildCatClassPopup(canvasGo);
            BuildChatHud(canvasGo, escRouter);
            BuildMouseJobHud(canvasGo, _phase);

            // 밸런스 데이터 로거(스펙 v3.3) — 매치 종료 시 persistentDataPath/MatchStats/*.json
            var statsGo = new GameObject("MatchStatsRecorder");
            var stats = statsGo.AddComponent<MatchStatsRecorder>();
            SetRef(stats, "m_Lobby", _lobby);
            SetRef(stats, "m_Match", _match);
        }

        // ===== 옵션 팝업 (2026-08-01 건의) =====
        // 게임을 멈추지 않는다(멀티플레이) — 커서만 풀고 UiFocus.ModalOpen으로 조작을 막는다.
        /// <summary>인트로 씬도 같은 옵션 창을 쓴다(2026-09-01) — 그래서 internal이고 팝업을 돌려준다.</summary>
        internal static OptionsPopup BuildOptionsPopup(GameObject _canvasGo, CTC.Runtime.Core.EscapeRouter _esc)
        {
            var panel = new GameObject("OptionsPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560f, 600f);
            panel.AddComponent<Image>().color = UiPalette.PanelSolid;

            TMP_Text title = CreateTmpText("OptionsTitle", panel.transform, "옵션",
                34f, TextAlignmentOptions.Center, new Vector2(0f, -18f), new Vector2(520f, 46f));
            title.fontStyle = FontStyles.Bold;

            Slider sens = CreateSlider(panel.transform, "SensitivitySlider", "마우스 감도", -84f, out TMP_Text sensValue);
            Slider bgm = CreateSlider(panel.transform, "BgmSlider", "배경음", -150f, out TMP_Text bgmValue);
            Slider sfx = CreateSlider(panel.transform, "SfxSlider", "효과음", -216f, out TMP_Text sfxValue);

            // 화면 — 고를 것이 서너 개뿐이라 드롭다운 대신 ◀ 값 ▶ 한 줄이다(OptionsPopup 주석 참조).
            CreateCyclerRow(panel.transform, "Resolution", "해상도", -288f,
                out TMP_Text resValue, out Button resPrev, out Button resNext);
            CreateCyclerRow(panel.transform, "WindowMode", "창 모드", -348f,
                out TMP_Text modeValue, out Button modePrev, out Button modeNext);

            // 3열 — 기본값 / 취소 / 확인. "닫기"가 아니라 "취소"인 이유: 이제 닫으면 되돌아간다.
            Button reset = CreatePanelButton(panel.transform, "ResetButton", "기본값", new Vector2(-180f, -424f), 168f);
            Button close = CreatePanelButton(panel.transform, "CloseButton", "취소", new Vector2(0f, -424f), 168f);
            Button confirm = CreatePanelButton(panel.transform, "ConfirmButton", "확인", new Vector2(180f, -424f), 168f);
            Button quit = CreatePanelButton(panel.transform, "QuitButton", "게임 종료", new Vector2(0f, -492f), 230f);

            var popup = _canvasGo.AddComponent<OptionsPopup>();
            SetRef(popup, "m_Panel", panel);
            SetRef(popup, "m_SensitivitySlider", sens);
            SetRef(popup, "m_BgmSlider", bgm);
            SetRef(popup, "m_SfxSlider", sfx);
            SetRef(popup, "m_SensitivityValue", sensValue);
            SetRef(popup, "m_BgmValue", bgmValue);
            SetRef(popup, "m_SfxValue", sfxValue);
            SetRef(popup, "m_ResolutionValue", resValue);
            SetRef(popup, "m_ResolutionPrev", resPrev);
            SetRef(popup, "m_ResolutionNext", resNext);
            SetRef(popup, "m_ModeValue", modeValue);
            SetRef(popup, "m_ModePrev", modePrev);
            SetRef(popup, "m_ModeNext", modeNext);
            SetRef(popup, "m_ResetButton", reset);
            SetRef(popup, "m_ConfirmButton", confirm);
            SetRef(popup, "m_CloseButton", close);
            SetRef(popup, "m_QuitButton", quit);

            // 프리팹은 **생성이 곧 열림**이다(2026-09-01) — 숨겨서 구우면 인스턴스는 있는데
            // 화면에 아무것도 안 나온다(실측: IsOpen=True인데 무화면).
            return popup;
        }

        /// <summary>
        /// 전직 선택 창 (v3.9). 강화를 둘 다 끝까지 올린 고양이에게만 뜬다.
        /// 어둠 스킬의 화면 암전과 빛 스킬의 실루엣도 여기서 같이 배선한다 —
        /// 둘 다 **보는 쪽 화면**에만 있는 것이라 캔버스에 붙는다.
        /// </summary>
        private static void BuildCatClassPopup(GameObject _canvasGo)
        {
            var panel = new GameObject("CatClassPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 300f);
            panel.AddComponent<Image>().color = UiPalette.PanelSolid;

            TMP_Text title = CreateTmpText("CatClassTitle", panel.transform, "전직",
                34f, TextAlignmentOptions.Center, new Vector2(0f, -20f), new Vector2(680f, 46f));
            title.fontStyle = FontStyles.Bold;

            // 스킬 이름을 버튼에 적어 둔다 — 되돌릴 수 없는 선택이라 설명이 버튼 안에 있어야 한다
            var labels = new[]
            {
                "불\n덮치기 피해 UP", "바람\n이동 속도 UP",
                "빛\n모든 쥐 투시", "어둠\n쥐 시야 차단",
            };
            var buttons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -255f + i * 170f;
                buttons[i] = CreatePanelButton(panel.transform, $"CatClass{i}", labels[i],
                    new Vector2(x, -160f), 160f);
            }

            var popup = _canvasGo.AddComponent<CTC.Runtime.UI.CatClassPopup>();
            SetRef(popup, "m_Root", panel);
            SetRef(popup, "m_Title", title);
            SetRefArray(popup, "m_Buttons", buttons);

            // 어둠 — 화면 후처리(깊이 기반, 내 쥐로부터 멀수록 검게). 렌더러 에셋에 피처를 한 번 등록한다.
            EnsureDarknessFeature();

            var view = _canvasGo.AddComponent<CTC.Runtime.UI.CatSkillView>();
            SetRef(view, "m_SilhouetteMaterial", CreateSilhouetteMaterial());

            panel.SetActive(false);
        }

        // 어둠 후처리 피처를 PC 렌더러 에셋에 등록한다(없을 때만). URP는 피처 목록을 에셋에 직렬화하므로
        // ScriptableRendererDataEditor.AddComponent과 같은 손질을 코드로 한다 — 리스트 + 로컬ID 맵.
        private const string PC_RENDERER_PATH = "Assets/Settings/PC_Renderer.asset";

        private static void EnsureDarknessFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(PC_RENDERER_PATH);
            if (data == null)
            {
                Debug.LogError($"[SetupNetworkingScene] 렌더러 에셋 없음: {PC_RENDERER_PATH} — 어둠 스킬이 안 그려진다");
                return;
            }

            Material mat = CreateDarknessMaterial();
            CTC.Runtime.Rendering.DarknessFeature existing = null;
            foreach (var f in data.rendererFeatures)
            {
                if (f is CTC.Runtime.Rendering.DarknessFeature d) { existing = d; break; }
            }

            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<CTC.Runtime.Rendering.DarknessFeature>();
                existing.name = "CTCDarkness";
                AssetDatabase.AddObjectToAsset(existing, data);

                var so = new SerializedObject(data);
                SerializedProperty list = so.FindProperty("m_RendererFeatures");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = existing;
                SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(existing, out _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[SetupNetworkingScene] PC_Renderer에 CTCDarkness 피처 등록");
            }

            var fso = new SerializedObject(existing);
            fso.FindProperty("m_Material").objectReferenceValue = mat;
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        private static Material CreateDarknessMaterial()
        {
            const string path = MaterialsDir + "/M_Darkness.mat";
            var shader = Shader.Find("CTC/Darkness");
            if (shader == null)
            {
                Debug.LogError("[SetupNetworkingScene] CTC/Darkness 셰이더 없음 — Assets/Shaders/CTC_Darkness.shader 확인");
                return null;
            }
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader == shader) return existing;

            var mat = new Material(shader) { name = "M_Darkness" };
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 빛 스킬의 실루엣 — 벽 너머로 보여야 하므로 **깊이 검사를 끄고 항상 위에** 그린다.
        // 전용 셰이더(Assets/Shaders/CTC_Silhouette.shader)를 쓴다: URP Unlit에는 _ZTest가 없어
        // SetInt("_ZTest")가 조용히 무시되고 렌더큐도 2000으로 되돌아갔다(2026-08-16 실측 —
        // 실루엣이 벽 뒤에 가려져 "빛 스킬이 안 보였다"). ZTest Always·Queue Overlay는 셰이더에 박혀 있다.
        private static Material CreateSilhouetteMaterial()
        {
            const string path = MaterialsDir + "/M_Silhouette.mat";
            var shader = Shader.Find("CTC/Silhouette");
            if (shader == null)
            {
                Debug.LogError("[SetupNetworkingScene] CTC/Silhouette 셰이더 없음 — Assets/Shaders/CTC_Silhouette.shader 확인");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            var mat = new Material(shader) { name = "M_Silhouette" };
            mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.2f, 1f));

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 프로젝트 첫 Slider — 재사용할 프리팹이 없어 4계층(배경/채움영역/채움/손잡이)을 손으로 조립한다.
        /// <summary>
        /// `라벨   ◀ 값 ▶` 한 줄. 슬라이더와 같은 좌표계를 쓴다(패널 상단 기준, 가운데 정렬).
        /// 화살표는 라벨과 겹치지 않게 오른쪽으로 몰아 둔다.
        /// </summary>
        private static void CreateCyclerRow(
            Transform _parent, string _name, string _label, float _y,
            out TMP_Text _value, out Button _prev, out Button _next)
        {
            CreateTmpText(_name + "Label", _parent, _label, 22f, TextAlignmentOptions.Left,
                new Vector2(-140f, _y), new Vector2(240f, 30f));

            _prev = CreatePanelButton(_parent, _name + "Prev", "◀", new Vector2(30f, _y + 4f), 44f);
            _value = CreateTmpText(_name + "Value", _parent, "-", 22f, TextAlignmentOptions.Center,
                new Vector2(140f, _y), new Vector2(180f, 30f));
            _next = CreatePanelButton(_parent, _name + "Next", "▶", new Vector2(250f, _y + 4f), 44f);
        }

        private static Slider CreateSlider(
            Transform _parent, string _name, string _label, float _y, out TMP_Text _value)
        {
            CreateTmpText(_name + "Label", _parent, _label, 22f, TextAlignmentOptions.Left,
                new Vector2(-140f, _y), new Vector2(240f, 30f));
            _value = CreateTmpText(_name + "Value", _parent, "-", 22f, TextAlignmentOptions.Right,
                new Vector2(190f, _y), new Vector2(130f, 30f));

            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, _y - 34f);
            rt.sizeDelta = new Vector2(460f, 20f);

            GameObject bg = AddStretchedImage(go.transform, "Background", UiPalette.Groove);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)fillArea.transform);
            GameObject fill = AddStretchedImage(fillArea.transform, "Fill", UiPalette.Accent);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)handleArea.transform);
            GameObject handle = AddStretchedImage(handleArea.transform, "Handle", Color.white);
            var handleRt = (RectTransform)handle.transform;
            handleRt.sizeDelta = new Vector2(22f, 0f);

            var slider = go.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = handleRt;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = false;
            bg.GetComponent<Image>().raycastTarget = true;
            return slider;
        }

        private static GameObject AddStretchedImage(Transform _parent, string _name, Color _color)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            StretchFull((RectTransform)go.transform);
            go.AddComponent<Image>().color = _color;
            return go;
        }

        private static void StretchFull(RectTransform _rt)
        {
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }

        // ===== 채팅 (2026-08-01 건의) =====
        // 좌측 중단이 비어 있다 — 좌하단은 MapInfoHud(로비), 하단 중앙은 ShopHotbar·AbilityBar가 쓴다.
        // 내 직업과 지금의 승리 조건. 화면 위쪽 가운데 — "규칙이 바뀌었다"를 알리는 자리다.
        private static void BuildMouseJobHud(GameObject _canvasGo, CTC.Runtime.Match.PhaseDirector _phase)
        {
            TMP_Text text = CreateTmpText("MouseJobText", _canvasGo.transform, "",
                26f, TextAlignmentOptions.Center, new Vector2(0f, -196f), new Vector2(860f, 60f));
            text.fontStyle = FontStyles.Bold;
            text.gameObject.SetActive(false);

            var hud = _canvasGo.AddComponent<CTC.Runtime.UI.MouseJobHud>();
            SetRef(hud, "m_Phase", _phase);
            SetRef(hud, "m_Text", text);
        }

        // ===== 능력 칸 (2026-08-17) — 핫바 아래 줄, 하단 중앙. 좌하단 글자 칩(ControlsHud) 대체 =====
        // 쿨다운 연출: 밝은 타일 + 검은 덮개(Radial360, 12시부터 시계방향으로 걷힘) + 가장자리 선 + 최상단 남은 초.
        private static void BuildAbilityBar(GameObject _canvasGo, LobbyController _lobby)
        {
            var root = new GameObject("AbilityBar", typeof(RectTransform));
            root.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, ABILITY_Y);
            rt.sizeDelta = new Vector2(SLOT_WIDTH * ABILITY_SLOTS + SLOT_GAP * (ABILITY_SLOTS - 1), SLOT_HEIGHT);

            var views = new Object[ABILITY_SLOTS];
            for (int i = 0; i < ABILITY_SLOTS; i++)
            {
                views[i] = BuildAbilitySlot(root.transform, i);
            }
            root.SetActive(false); // AbilityBar가 게임 시작 시 켬

            var bar = _canvasGo.AddComponent<AbilityBar>();
            SetRef(bar, "m_Lobby", _lobby);
            SetRef(bar, "m_Root", root);
            SetRefArray(bar, "m_Slots", views);
        }

        // 핫바 칸(BuildLobbyCanvas의 Slot 생성부)과 같은 크기·바탕·세 줄 배치. 글자 자리도 같은 y다.
        private static AbilitySlotView BuildAbilitySlot(Transform _parent, int _index)
        {
            // 위치는 AbilityBar가 쓰는 칸 수에 맞춰 가운데 정렬로 다시 놓는다 — 여기선 자리만
            var go = new GameObject($"Ability{_index + 1}", typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2((_index - (ABILITY_SLOTS - 1) * 0.5f) * (SLOT_WIDTH + SLOT_GAP), 0f);
            rt.sizeDelta = new Vector2(SLOT_WIDTH, SLOT_HEIGHT);

            var tile = go.AddComponent<Image>();
            tile.color = UiPalette.Slot;   // 핫바 slotBg와 같은 색
            tile.raycastTarget = false;

            TMP_Text name = CreateTmpText("Name", go.transform, "", 22f, TextAlignmentOptions.Center,
                new Vector2(0f, -6f), new Vector2(SLOT_WIDTH - 8f, 26f));
            TMP_Text hint = CreateTmpText("Hint", go.transform, "", 12f, TextAlignmentOptions.Center,
                new Vector2(0f, -34f), new Vector2(SLOT_WIDTH - 8f, 18f));
            hint.color = UiPalette.WithAlpha(UiPalette.TextDim, 0.9f);   // ShopHotbar.COLOR_HINT와 같은 값
            TMP_Text status = CreateTmpText("Status", go.transform, "", 20f, TextAlignmentOptions.Center,
                new Vector2(0f, -54f), new Vector2(SLOT_WIDTH - 8f, 26f));

            Image cover = FullRectImage(go.transform, "Cover", 0f, UiPalette.Veil);
            // **스프라이트가 없으면 Filled가 무시된다**(Image.OnPopulateMesh가 통짜 사각형으로 떨어진다) —
            // 내장 UISprite를 꽂아야 fillAmount가 먹는다(2026-08-17 실측: 덮개가 늘 꽉 차 보였다).
            cover.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            cover.type = Image.Type.Filled;
            cover.fillMethod = Image.FillMethod.Radial360;
            cover.fillOrigin = (int)Image.Origin360.Top;
            cover.fillClockwise = false;   // 걷히는 가장자리가 시계방향으로 돌게(AbilitySlotView 참조)
            cover.fillAmount = 0f;

            // 걷히는 가장자리 선 — 중심에서 위쪽으로 뻗는 막대, 중심 기준으로 회전(칸이 직사각형이라 모서리는 짧게 닿는다)
            var edgeGo = new GameObject("Edge", typeof(RectTransform));
            edgeGo.transform.SetParent(go.transform, false);
            var edgeRt = (RectTransform)edgeGo.transform;
            edgeRt.anchorMin = edgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            edgeRt.pivot = new Vector2(0.5f, 0f);
            edgeRt.anchoredPosition = Vector2.zero;
            edgeRt.sizeDelta = new Vector2(2f, SLOT_HEIGHT * 0.5f - 2f);
            var edge = edgeGo.AddComponent<Image>();
            edge.color = UiPalette.WithAlpha(UiPalette.AccentSoft, 0.9f);
            edge.raycastTarget = false;
            edgeGo.SetActive(false);

            // 남은 초 — 덮개 위(최상단)
            TMP_Text seconds = CreateTmpText("Seconds", go.transform, "", 40f, TextAlignmentOptions.Center,
                new Vector2(0f, -14f), new Vector2(SLOT_WIDTH, 56f));
            seconds.fontStyle = FontStyles.Bold;

            // 지속 잔여 바 — 맨 아래
            var barGo = new GameObject("ActiveBar", typeof(RectTransform));
            barGo.transform.SetParent(go.transform, false);
            var barRt = (RectTransform)barGo.transform;
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 2f);
            barRt.sizeDelta = new Vector2(-8f, 3f);
            var bar = barGo.AddComponent<Image>();
            bar.color = UiPalette.Accent;
            bar.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = 0;
            bar.raycastTarget = false;
            barGo.SetActive(false);

            var view = go.AddComponent<AbilitySlotView>();
            SetRef(view, "m_Tile", tile);
            SetRef(view, "m_Cover", cover);
            SetRef(view, "m_Edge", edgeRt);
            SetRef(view, "m_ActiveBar", bar);
            SetRef(view, "m_NameText", name);
            SetRef(view, "m_HintText", hint);
            SetRef(view, "m_StatusText", status);
            SetRef(view, "m_SecondsText", seconds);
            return view;
        }

        private static Image FullRectImage(Transform _parent, string _name, float _inset, Color _color)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(_inset, _inset);
            rt.offsetMax = new Vector2(-_inset, -_inset);
            var img = go.AddComponent<Image>();
            img.color = _color;
            img.raycastTarget = false;
            return img;
        }

        // ===== 우상단 상태 클러스터 (HUD 개편 2026-08-15) =====
        // 내 HP 바 + 재화 + 상시 배지 — StatusHud가 표시 제어. 고양이 HP는 쥐에게 안 보여준다.
        private static void BuildStatusCluster(GameObject _canvasGo, LobbyController _lobby)
        {
            var root = new GameObject("StatusCluster", typeof(RectTransform));
            root.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
            rt.sizeDelta = new Vector2(320f, 90f);

            (GameObject _, Image myFill, TMP_Text myText) = CreateStatusBar(
                root.transform, "MyHpBar", 0f, 26f, 16f, UiPalette.WithAlpha(UiPalette.Good, 0.9f));

            TMP_Text wallet = CreateTmpText("WalletText", root.transform, "-",
                24f, TextAlignmentOptions.Right, new Vector2(0f, -32f), new Vector2(320f, 32f));
            TMP_Text badge = CreateTmpText("BadgeText", root.transform, "",
                16f, TextAlignmentOptions.Right, new Vector2(0f, -66f), new Vector2(320f, 24f));
            badge.color = UiPalette.WithAlpha(UiPalette.TextOn, 0.9f);

            root.SetActive(false); // StatusHud가 게임 시작 시 켬

            var hud = _canvasGo.AddComponent<CTC.Runtime.UI.StatusHud>();
            SetRef(hud, "m_Lobby", _lobby);
            SetRef(hud, "m_Root", root);
            SetRef(hud, "m_MyHpFill", myFill);
            SetRef(hud, "m_MyHpText", myText);
            SetRef(hud, "m_WalletText", wallet);
            SetRef(hud, "m_BadgeText", badge);
        }

        // 게이지 한 줄(배경 + 채움 + 겹침 라벨). 채움은 StatusHud가 anchorMax.x 비율로 그린다.
        private static (GameObject bar, Image fill, TMP_Text label) CreateStatusBar(
            Transform _parent, string _name, float _y, float _height, float _fontSize, Color _fillColor)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, _y);
            rt.sizeDelta = new Vector2(0f, _height);
            var bg = go.AddComponent<Image>();
            bg.color = UiPalette.Slot;
            bg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGo.AddComponent<Image>();
            fill.color = _fillColor;
            fill.raycastTarget = false;

            TMP_Text label = CreateTmpText(_name + "Text", go.transform, "-", _fontSize,
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(0f, _height));
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            return (go, fill, label);
        }

        private static void BuildChatHud(GameObject _canvasGo, CTC.Runtime.Core.EscapeRouter _esc)
        {
            var root = new GameObject("ChatRoot", typeof(RectTransform));
            root.transform.SetParent(_canvasGo.transform, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0f, 0f);
            rootRt.pivot = new Vector2(0f, 0f);
            rootRt.anchoredPosition = new Vector2(24f, 300f);
            rootRt.sizeDelta = new Vector2(560f, 200f);

            var logGo = new GameObject("ChatLog", typeof(RectTransform));
            logGo.transform.SetParent(root.transform, false);
            var logRt = (RectTransform)logGo.transform;
            logRt.anchorMin = new Vector2(0f, 0f);
            logRt.anchorMax = new Vector2(1f, 1f);
            logRt.offsetMin = new Vector2(0f, 40f);
            logRt.offsetMax = Vector2.zero;
            var log = logGo.AddComponent<TextMeshProUGUI>();
            log.text = "";
            log.fontSize = 20f;
            log.alignment = TextAlignmentOptions.BottomLeft;
            log.color = Color.white;
            log.raycastTarget = false;

            GameObject input = CreateChatInput(root.transform, out TMP_InputField field);
            input.SetActive(false);

            // `/` 예약어 힌트 — 입력창 바로 아래 한 장(2026-08-20). 목록·표시는 ChatHud가 켠다.
            var hintGo = new GameObject("CommandHint", typeof(RectTransform));
            hintGo.transform.SetParent(root.transform, false);
            var hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0f, 1f);
            hintRt.offsetMin = new Vector2(0f, -44f);
            hintRt.offsetMax = new Vector2(0f, -6f);
            hintGo.AddComponent<Image>().color = UiPalette.Panel;

            var hintTextGo = new GameObject("Text", typeof(RectTransform));
            hintTextGo.transform.SetParent(hintGo.transform, false);
            var hintTextRt = (RectTransform)hintTextGo.transform;
            hintTextRt.anchorMin = Vector2.zero;
            hintTextRt.anchorMax = Vector2.one;
            hintTextRt.offsetMin = new Vector2(10f, 2f);
            hintTextRt.offsetMax = new Vector2(-10f, -2f);
            var hintText = hintTextGo.AddComponent<TextMeshProUGUI>();
            hintText.fontSize = 18f;
            hintText.alignment = TextAlignmentOptions.Left;
            hintText.color = UiPalette.TextOn;
            hintText.raycastTarget = false;
            hintGo.SetActive(false);

            var chat = _canvasGo.AddComponent<ChatHud>();
            SetRef(chat, "m_Root", root);
            SetRef(chat, "m_Log", log);
            SetRef(chat, "m_Input", field);
            SetRef(chat, "m_HintRoot", hintGo);
            SetRef(chat, "m_HintText", hintText);
            SetRef(_esc, "m_Chat", chat);

            root.SetActive(false);
        }

        // TMP_InputField는 Text Area(RectMask2D) + Text + Placeholder 자식 구조를 직접 만들어야 한다.
        private static GameObject CreateChatInput(Transform _parent, out TMP_InputField _field)
        {
            var go = new GameObject("ChatInput", typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, 34f);
            go.AddComponent<Image>().color = UiPalette.Panel;

            var area = new GameObject("Text Area", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            var areaRt = (RectTransform)area.transform;
            areaRt.anchorMin = Vector2.zero;
            areaRt.anchorMax = Vector2.one;
            areaRt.offsetMin = new Vector2(10f, 4f);
            areaRt.offsetMax = new Vector2(-10f, -4f);
            area.AddComponent<RectMask2D>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(area.transform, false);
            StretchFull((RectTransform)textGo.transform);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(area.transform, false);
            StretchFull((RectTransform)placeholderGo.transform);
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text = "Enter로 전송 · ESC로 취소";
            placeholder.fontSize = 20f;
            placeholder.color = UiPalette.WithAlpha(UiPalette.TextDim, 0.8f);

            _field = go.AddComponent<TMP_InputField>();
            _field.textViewport = areaRt;
            _field.textComponent = text;
            _field.placeholder = placeholder;
            _field.lineType = TMP_InputField.LineType.SingleLine;
            _field.characterLimit = ChatManager.MAX_MESSAGE_LENGTH;
            _field.targetGraphic = go.GetComponent<Image>();
            return go;
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
        private static Button CreatePanelButton(
            Transform _parent, string _name, string _label, Vector2 _anchoredPos,
            float _width = PANEL_CONTENT_WIDTH)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = _anchoredPos;
            rt.sizeDelta = new Vector2(_width, 56f);

            var bg = go.AddComponent<Image>();
            // 흰 바탕 + 검은 글씨였다. 인트로·로비·대기실은 전부 어두운 패널에 밝은 글씨라
            // 인게임 버튼만 반대로 튀었다 — 같은 게임으로 안 읽힌다(2026-08-30).
            bg.color = UiPalette.Slot;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = UiPalette.Line;
            outline.effectDistance = new Vector2(1f, -1f);

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
            tmp.color = UiPalette.TextOn;
            tmp.raycastTarget = false;

            var button = go.AddComponent<Button>();
            button.targetGraphic = bg; // 기본 ColorTint 전환 사용
            go.AddComponent<CTC.Runtime.Audio.UiClickSfx>(); // 공통 클릭음(2D)
            return button;
        }
    }
}
