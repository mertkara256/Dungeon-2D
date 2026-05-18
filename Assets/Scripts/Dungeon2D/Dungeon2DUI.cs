using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Dungeon2D
{
    public sealed class Dungeon2DUI : MonoBehaviour
    {
        private Dungeon2DGame game;
        private Dungeon2DArtLibrary art;
        private Dungeon2DUITheme theme;
        private Font font;
        private Canvas canvas;
        private GameObject mainMenuRoot;
        private GameObject howToPlayRoot;
        private GameObject hudRoot;
        private GameObject perkDraftRoot;
        private Text healthText;
        private Image healthFill;
        private RectTransform healthFillRect;
        private Text scoreText;
        private Text roomText;
        private Text potionText;
        private Text boostText;
        private Text keyText;
        private Text notificationText;
        private CanvasGroup notificationGroup;
        private Text stateText;
        private CanvasGroup stateGroup;
        private Text interactionText;
        private CanvasGroup interactionGroup;
        private Text bossText;
        private Image bossFill;
        private RectTransform bossFillRect;
        private GameObject bossPanelRoot;
        private Text draftTitleText;
        private Text draftSubtitleText;
        private readonly List<Button> draftButtons = new List<Button>();
        private Coroutine notificationRoutine;

        public void Build(Dungeon2DGame owner, Dungeon2DArtLibrary artLibrary)
        {
            game = owner;
            art = artLibrary;
            theme = Dungeon2DUITheme.Load();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            EnsureEventSystem();
            BuildCanvas();
            BuildMainMenu();
            BuildHud();
            BuildPerkDraft();
        }

        public void ShowMainMenu()
        {
            if (mainMenuRoot != null)
            {
                mainMenuRoot.SetActive(true);
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
        }

        public void HideMainMenu()
        {
            if (mainMenuRoot != null)
            {
                mainMenuRoot.SetActive(false);
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(true);
            }
        }

        public void RefreshHud(
            DungeonPlayer2D player,
            PlayerStats2D stats,
            PlayerInventory2D inventory,
            int score,
            int currentRoom,
            int totalRooms,
            DungeonBoss2D boss)
        {
            if (healthText != null && stats != null)
            {
                float current = player != null ? player.CurrentHealth : stats.MaxHealth;
                healthText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(stats.MaxHealth);
                if (healthFill != null)
                {
                    float ratio = stats.MaxHealth <= 0.01f ? 0f : Mathf.Clamp01(current / stats.MaxHealth);
                    SetBarFill(healthFillRect, ratio);
                    healthFill.color = ratio <= 0.3f ? new Color(1f, 0.28f, 0.2f, 1f) : new Color(0.82f, 0.12f, 0.16f, 1f);
                }
            }

            if (scoreText != null)
            {
                scoreText.text = "SCORE " + score;
            }

            if (roomText != null)
            {
                roomText.text = totalRooms <= 0 ? "ROOM --" : "ROOM " + currentRoom + " / " + totalRooms;
            }

            if (inventory != null)
            {
                if (potionText != null)
                {
                    potionText.text = inventory.Potions.ToString();
                }

                if (boostText != null)
                {
                    boostText.text = inventory.SpeedBoosts.ToString();
                }

                if (keyText != null)
                {
                    keyText.text = inventory.Keys.ToString();
                }
            }

            bool bossVisible = boss != null && !boss.IsDead;
            if (bossPanelRoot != null)
            {
                bossPanelRoot.SetActive(bossVisible);
            }

            if (bossVisible)
            {
                if (bossText != null)
                {
                    bossText.text = "CORRUPTED GUARDIAN";
                }

                if (bossFill != null)
                {
                    SetBarFill(bossFillRect, boss.HealthRatio);
                }
            }
        }

        public void Notify(string message)
        {
            if (notificationText == null || notificationGroup == null)
            {
                return;
            }

            if (notificationRoutine != null)
            {
                StopCoroutine(notificationRoutine);
            }

            notificationRoutine = StartCoroutine(NotificationRoutine(message));
        }

        public void SetState(DungeonGameState state)
        {
            if (stateGroup == null || stateText == null)
            {
                return;
            }

            bool visible = state != DungeonGameState.Playing && state != DungeonGameState.MainMenu;
            stateGroup.alpha = visible ? 1f : 0f;
            stateGroup.blocksRaycasts = false;
            stateText.text = visible ? StateLabel(state) : string.Empty;
        }

        public void SetInteractionPrompt(string label)
        {
            if (interactionGroup == null || interactionText == null)
            {
                return;
            }

            bool visible = !string.IsNullOrWhiteSpace(label);
            interactionGroup.alpha = visible ? 1f : 0f;
            interactionText.text = visible ? "E  " + label : string.Empty;
        }

        public void ShowPerkDraft(string title, string subtitle, IReadOnlyList<PerkChoice2D> choices, Action<int> onSelected)
        {
            if (perkDraftRoot == null || choices == null)
            {
                return;
            }

            perkDraftRoot.SetActive(true);
            if (draftTitleText != null)
            {
                draftTitleText.text = title;
            }

            if (draftSubtitleText != null)
            {
                draftSubtitleText.text = subtitle + "\nClick a card or press 1, 2, or 3.";
            }

            for (int i = 0; i < draftButtons.Count; i++)
            {
                int index = i;
                Button button = draftButtons[i];
                bool hasChoice = i < choices.Count;
                button.gameObject.SetActive(hasChoice);
                button.onClick.RemoveAllListeners();
                if (!hasChoice)
                {
                    continue;
                }

                if (onSelected != null)
                {
                    button.onClick.AddListener(delegate { onSelected(index); });
                }
                PerkChoice2D choice = choices[i];
                Image background = button.GetComponent<Image>();
                Sprite cardSprite = theme != null ? theme.GetPerkCard(choice.Rarity) : null;
                if (background != null && cardSprite != null)
                {
                    ApplySprite(background, cardSprite, Image.Type.Sliced);
                }
                else if (background != null)
                {
                    background.sprite = null;
                    background.color = CardColor(choice.Rarity);
                }

                Transform iconTransform = button.transform.Find("UpgradeIcon");
                if (iconTransform != null)
                {
                    Image iconImage = iconTransform.GetComponent<Image>();
                    if (iconImage != null)
                    {
                        iconImage.sprite = theme != null ? theme.GetUpgradeIcon(choice.UpgradeType) : null;
                        iconImage.enabled = iconImage.sprite != null;
                    }
                }

                SetChildText(button.transform, "Title", choice.Title + "\n" + choice.Rarity);
                SetChildText(button.transform, "Description", choice.Description);
                SetChildText(button.transform, "Hotkey", "[" + (i + 1) + "] Pick");
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && draftButtons.Count > 0)
            {
                eventSystem.SetSelectedGameObject(draftButtons[0].gameObject);
            }
        }

        public void HidePerkDraft()
        {
            if (perkDraftRoot != null)
            {
                perkDraftRoot.SetActive(false);
            }
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildMainMenu()
        {
            mainMenuRoot = CreateRect("MainMenu", canvas.transform).gameObject;
            Stretch(mainMenuRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image background = CreateImage("MenuBackground", mainMenuRoot.transform, Color.white);
            if (!ApplySprite(background, theme != null ? theme.MenuBackground : null, Image.Type.Simple))
            {
                background.color = new Color(0.025f, 0.028f, 0.035f, 0.96f);
            }
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image shade = CreateImage("MenuShade", mainMenuRoot.transform, new Color(0f, 0f, 0f, 0.2f));
            Stretch(shade.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image hero = CreateImage("MenuHeroPanel", mainMenuRoot.transform, Color.white);
            if (!ApplySprite(hero, theme != null ? theme.TitlePlaque : null, Image.Type.Sliced))
            {
                hero.color = new Color(0.08f, 0.075f, 0.09f, 0.95f);
                hero.sprite = art.GetEnvironmentSprite(EnvironmentSpriteId.Panel);
            }

            SetFixed(hero.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(860f, 214f), Vector2.zero);

            Text title = CreateText("Title", hero.transform, "ECHO DUNGEON 2D", 48, new Color(0.93f, 0.91f, 0.85f, 1f), TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);

            Text subtitle = CreateText("Subtitle", hero.transform, "Clear rooms. Claim rewards. Keep moving.", 20, new Color(0.62f, 0.66f, 0.72f, 1f), TextAnchor.MiddleCenter);
            Stretch(subtitle.rectTransform, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.44f), Vector2.zero, Vector2.zero);

            GameObject buttonPanel = CreateRect("MenuButtons", mainMenuRoot.transform).gameObject;
            SetFixed(buttonPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.42f), new Vector2(420f, 240f), Vector2.zero);
            VerticalLayoutGroup layout = buttonPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            Button start = CreateButton(buttonPanel.transform, "START GAME", new Color(1f, 0.73f, 0.24f, 1f));
            start.onClick.AddListener(game.StartRun);

            Button howTo = CreateButton(buttonPanel.transform, "HOW TO PLAY", new Color(0.42f, 0.86f, 0.96f, 1f));
            howTo.onClick.AddListener(delegate { howToPlayRoot.SetActive(true); });

            Button quit = CreateButton(buttonPanel.transform, "QUIT", new Color(0.62f, 0.66f, 0.72f, 1f));
            quit.onClick.AddListener(Application.Quit);

            Image footerPanel = CreatePanel("MenuFooterPanel", mainMenuRoot.transform);
            SetFixed(footerPanel.rectTransform, new Vector2(0.5f, 0.08f), new Vector2(780f, 46f), Vector2.zero);
            ApplySprite(footerPanel, theme != null ? theme.Notification : null, Image.Type.Sliced);

            Text footer = CreateText("MenuFooter", footerPanel.transform, "Enter/Space starts. WASD move, mouse aim, LMB shoot, RMB melee.", 16, new Color(0.86f, 0.88f, 0.88f, 1f), TextAnchor.MiddleCenter);
            Stretch(footer.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), Vector2.zero, Vector2.zero);

            BuildHowToPlay();
        }

        private void BuildHowToPlay()
        {
            howToPlayRoot = CreateRect("HowToPlayOverlay", mainMenuRoot.transform).gameObject;
            Stretch(howToPlayRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image blocker = CreateImage("Blocker", howToPlayRoot.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(blocker.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image panel = CreatePanel("HowToPlayPanel", howToPlayRoot.transform);
            SetFixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(760f, 454f), Vector2.zero);

            Text title = CreateText("HowTitle", panel.transform, "HOW TO PLAY", 30, Color.white, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);

            Text body = CreateText(
                "HowBody",
                panel.transform,
                "Move room by room through the dungeon.\nClear enemies before pushing deeper.\nUse upgrade rooms, treasure caches, potions, keys, and speed boosts to survive.\n\nControls\nWASD / Arrows  Move\nMouse          Aim\nLMB            Shoot\nRMB            Melee\nShift          Dash\nQ              Potion\n1              Speed Boost\nE              Interact\nEsc            Pause\nR              Restart after defeat/victory",
                20,
                new Color(0.82f, 0.84f, 0.88f, 1f),
                TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(body.rectTransform, new Vector2(0.12f, 0.2f), new Vector2(0.88f, 0.8f), Vector2.zero, Vector2.zero);

            Button close = CreateButton(panel.transform, "CLOSE", new Color(0.42f, 0.86f, 0.96f, 1f));
            SetFixed(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0.09f), new Vector2(180f, 48f), Vector2.zero);
            close.onClick.AddListener(delegate { howToPlayRoot.SetActive(false); });
            howToPlayRoot.SetActive(false);
        }

        private void BuildHud()
        {
            hudRoot = CreateRect("HUD", canvas.transform).gameObject;
            Stretch(hudRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hudRoot.SetActive(false);

            BuildVitalsPanel();
            BuildRoomPanel();
            BuildBossPanel();
            BuildNotification();
            BuildInteractionPrompt();
            BuildStateChip();
            BuildControlsBar();
        }

        private void BuildVitalsPanel()
        {
            Image panel = CreatePanel("VitalsPanel", hudRoot.transform);
            SetFixed(panel.rectTransform, new Vector2(0.14f, 0.88f), new Vector2(360f, 176f), Vector2.zero);

            Text label = CreateText("VitalsLabel", panel.transform, "PLAYER STATUS", 13, new Color(0.62f, 0.66f, 0.72f, 1f), TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, new Vector2(0.06f, 0.78f), new Vector2(0.54f, 0.95f), Vector2.zero, Vector2.zero);

            scoreText = CreateText("Score", panel.transform, "SCORE 0", 18, Color.white, TextAnchor.MiddleRight);
            Stretch(scoreText.rectTransform, new Vector2(0.45f, 0.78f), new Vector2(0.94f, 0.95f), Vector2.zero, Vector2.zero);

            Image healthTrack = CreateImage("HealthTrack", panel.transform, new Color(0f, 0f, 0f, 0.42f));
            ApplySprite(healthTrack, theme != null ? theme.BarTrack : null, Image.Type.Sliced);
            Stretch(healthTrack.rectTransform, new Vector2(0.06f, 0.54f), new Vector2(0.94f, 0.72f), Vector2.zero, Vector2.zero);
            healthFill = CreateImage("HealthFill", healthTrack.transform, new Color(0.82f, 0.12f, 0.16f, 1f));
            ApplySprite(healthFill, theme != null ? theme.HealthFill : null, Image.Type.Sliced);
            healthFillRect = healthFill.rectTransform;
            Stretch(healthFillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            healthText = CreateText("HealthText", healthTrack.transform, "100 / 100", 16, Color.white, TextAnchor.MiddleCenter);
            Stretch(healthText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            potionText = CreateItemBadge(panel.transform, "P", art.GetEnvironmentSprite(EnvironmentSpriteId.HealthPotion), new Vector2(0.18f, 0.25f));
            boostText = CreateItemBadge(panel.transform, "B", art.GetEnvironmentSprite(EnvironmentSpriteId.SpeedPotion), new Vector2(0.5f, 0.25f));
            keyText = CreateItemBadge(panel.transform, "K", art.GetEnvironmentSprite(EnvironmentSpriteId.Key), new Vector2(0.82f, 0.25f));
        }

        private Text CreateItemBadge(Transform parent, string label, Sprite icon, Vector2 anchor)
        {
            Image badge = CreateImage("Item_" + label, parent, new Color(1f, 1f, 1f, 0.07f));
            ApplySprite(badge, theme != null ? theme.ItemBadge : null, Image.Type.Sliced);
            SetFixed(badge.rectTransform, anchor, new Vector2(84f, 64f), Vector2.zero);

            Image iconImage = CreateImage("Icon", badge.transform, Color.white);
            iconImage.sprite = icon;
            Stretch(iconImage.rectTransform, new Vector2(0.1f, 0.18f), new Vector2(0.54f, 0.82f), Vector2.zero, Vector2.zero);

            Text count = CreateText("Count", badge.transform, "0", 20, Color.white, TextAnchor.MiddleRight);
            Stretch(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.9f, 1f), Vector2.zero, Vector2.zero);
            return count;
        }

        private void BuildRoomPanel()
        {
            Image panel = CreatePanel("RoomPanel", hudRoot.transform);
            SetFixed(panel.rectTransform, new Vector2(0.5f, 0.92f), new Vector2(360f, 66f), Vector2.zero);

            Text label = CreateText("RouteLabel", panel.transform, "DUNGEON ROUTE", 12, new Color(0.62f, 0.66f, 0.72f, 1f), TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, new Vector2(0f, 0.58f), Vector2.one, Vector2.zero, Vector2.zero);

            roomText = CreateText("RoomText", panel.transform, "ROOM --", 21, Color.white, TextAnchor.MiddleCenter);
            Stretch(roomText.rectTransform, Vector2.zero, new Vector2(1f, 0.62f), Vector2.zero, Vector2.zero);
        }

        private void BuildBossPanel()
        {
            Image panel = CreatePanel("BossPanel", hudRoot.transform);
            bossPanelRoot = panel.gameObject;
            SetFixed(panel.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(520f, 52f), Vector2.zero);

            bossText = CreateText("BossName", panel.transform, "CORRUPTED GUARDIAN", 15, Color.white, TextAnchor.MiddleCenter);
            Stretch(bossText.rectTransform, new Vector2(0.04f, 0.48f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

            Image track = CreateImage("BossTrack", panel.transform, new Color(0f, 0f, 0f, 0.44f));
            ApplySprite(track, theme != null ? theme.BarTrack : null, Image.Type.Sliced);
            Stretch(track.rectTransform, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.42f), Vector2.zero, Vector2.zero);
            bossFill = CreateImage("BossFill", track.transform, new Color(0.75f, 0.12f, 0.85f, 1f));
            ApplySprite(bossFill, theme != null ? theme.BossFill : null, Image.Type.Sliced);
            bossFillRect = bossFill.rectTransform;
            Stretch(bossFillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bossPanelRoot.SetActive(false);
        }

        private void BuildNotification()
        {
            Image panel = CreatePanel("Notification", hudRoot.transform);
            SetFixed(panel.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(560f, 46f), Vector2.zero);
            ApplySprite(panel, theme != null ? theme.Notification : null, Image.Type.Sliced);
            notificationGroup = panel.gameObject.AddComponent<CanvasGroup>();
            notificationGroup.alpha = 0f;
            notificationGroup.blocksRaycasts = false;

            notificationText = CreateText("NotificationText", panel.transform, "", 18, Color.white, TextAnchor.MiddleCenter);
            Stretch(notificationText.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), Vector2.zero, Vector2.zero);
        }

        private void BuildInteractionPrompt()
        {
            Image panel = CreatePanel("InteractionPrompt", hudRoot.transform);
            SetFixed(panel.rectTransform, new Vector2(0.5f, 0.18f), new Vector2(360f, 48f), Vector2.zero);
            ApplySprite(panel, theme != null ? theme.Notification : null, Image.Type.Sliced);
            interactionGroup = panel.gameObject.AddComponent<CanvasGroup>();
            interactionGroup.alpha = 0f;
            interactionGroup.blocksRaycasts = false;

            interactionText = CreateText("InteractionText", panel.transform, "", 18, new Color(1f, 0.82f, 0.28f, 1f), TextAnchor.MiddleCenter);
            Stretch(interactionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void BuildStateChip()
        {
            Image panel = CreatePanel("StateChip", hudRoot.transform);
            SetFixed(panel.rectTransform, new Vector2(0.88f, 0.92f), new Vector2(200f, 44f), Vector2.zero);
            stateGroup = panel.gameObject.AddComponent<CanvasGroup>();
            stateGroup.alpha = 0f;
            stateGroup.blocksRaycasts = false;

            stateText = CreateText("StateText", panel.transform, "", 19, Color.white, TextAnchor.MiddleCenter);
            Stretch(stateText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void BuildControlsBar()
        {
            Image bar = CreatePanel("ControlsBar", hudRoot.transform);
            SetFixed(bar.rectTransform, new Vector2(0.5f, 0.045f), new Vector2(880f, 40f), Vector2.zero);
            ApplySprite(bar, theme != null ? theme.Notification : null, Image.Type.Sliced);

            Text text = CreateText("ControlsText", bar.transform, "WASD Move   Mouse Aim   LMB Shoot   RMB Melee   Shift Dash   Q Potion   1 Boost   E Interact   Esc Pause", 15, new Color(0.9f, 0.9f, 0.86f, 1f), TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        }

        private void BuildPerkDraft()
        {
            perkDraftRoot = CreateRect("PerkDraft", canvas.transform).gameObject;
            Stretch(perkDraftRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image background = CreateImage("DraftBackground", perkDraftRoot.transform, Color.white);
            if (!ApplySprite(background, theme != null ? theme.UpgradeBackground : null, Image.Type.Simple))
            {
                background.color = new Color(0.02f, 0.024f, 0.032f, 1f);
            }
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image overlay = CreateImage("Overlay", perkDraftRoot.transform, new Color(0.02f, 0.024f, 0.032f, 0.58f));
            Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image panel = CreatePanel("DraftPanel", perkDraftRoot.transform);
            SetFixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1080f, 620f), Vector2.zero);

            draftTitleText = CreateText("DraftTitle", panel.transform, "Choose a Perk", 34, Color.white, TextAnchor.MiddleCenter);
            Stretch(draftTitleText.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);

            draftSubtitleText = CreateText("DraftSubtitle", panel.transform, "", 18, new Color(0.7f, 0.74f, 0.8f, 1f), TextAnchor.MiddleCenter);
            Stretch(draftSubtitleText.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);

            float[] anchors = { 0.21f, 0.5f, 0.79f };
            for (int i = 0; i < 3; i++)
            {
                Button card = CreatePerkCard(panel.transform, i + 1);
                SetFixed(card.GetComponent<RectTransform>(), new Vector2(anchors[i], 0.38f), new Vector2(270f, 330f), Vector2.zero);
                draftButtons.Add(card);
            }

            perkDraftRoot.SetActive(false);
        }

        private Button CreatePerkCard(Transform parent, int index)
        {
            Image image = CreateImage("PerkCard_" + index, parent, Color.white);
            Sprite cardSprite = theme != null ? theme.GetPerkCard(PerkRarity2D.Common) : null;
            if (!ApplySprite(image, cardSprite, Image.Type.Sliced))
            {
                image.color = new Color(0.13f, 0.14f, 0.18f, 0.96f);
            }

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Image icon = CreateImage("UpgradeIcon", image.transform, Color.white);
            icon.preserveAspect = true;
            SetFixed(icon.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(82f, 82f), Vector2.zero);

            Text title = CreateText("Title", image.transform, "", 22, Color.white, TextAnchor.UpperCenter);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(title.rectTransform, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.72f), Vector2.zero, Vector2.zero);

            Text description = CreateText("Description", image.transform, "", 18, new Color(0.76f, 0.79f, 0.84f, 1f), TextAnchor.UpperCenter);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(description.rectTransform, new Vector2(0.1f, 0.22f), new Vector2(0.9f, 0.52f), Vector2.zero, Vector2.zero);

            Text hotkey = CreateText("Hotkey", image.transform, "", 17, new Color(1f, 0.82f, 0.28f, 1f), TextAnchor.MiddleCenter);
            Stretch(hotkey.rectTransform, new Vector2(0.18f, 0.06f), new Vector2(0.82f, 0.18f), Vector2.zero, Vector2.zero);
            return button;
        }

        private static void SetChildText(Transform root, string childName, string value)
        {
            Transform child = root != null ? root.Find(childName) : null;
            Text text = child != null ? child.GetComponent<Text>() : null;
            if (text != null)
            {
                text.text = value;
            }
        }

        private IEnumerator NotificationRoutine(string message)
        {
            notificationText.text = message;
            yield return Fade(notificationGroup, 1f, 0.12f);
            yield return new WaitForSecondsRealtime(2.1f);
            yield return Fade(notificationGroup, 0f, 0.16f);
            notificationText.text = string.Empty;
            notificationRoutine = null;
        }

        private IEnumerator Fade(CanvasGroup group, float target, float duration)
        {
            float start = group.alpha;
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(timer / Mathf.Max(0.01f, duration)));
                yield return null;
            }

            group.alpha = target;
        }

        private Image CreatePanel(string name, Transform parent)
        {
            Image image = CreateImage(name, parent, new Color(0.045f, 0.052f, 0.065f, 0.86f));
            if (!ApplySprite(image, theme != null ? theme.Panel : null, Image.Type.Sliced))
            {
                image.sprite = art.GetEnvironmentSprite(EnvironmentSpriteId.Panel);
                image.type = Image.Type.Simple;
            }

            return image;
        }

        private Button CreateButton(Transform parent, string label, Color accent)
        {
            Image image = CreateImage("Button_" + label, parent, new Color(0.095f, 0.102f, 0.13f, 0.96f));
            ApplySprite(image, theme != null ? theme.Button : null, Image.Type.Sliced);
            LayoutElement element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 54f;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonSprites(button);

            Image accentImage = CreateImage("Accent", image.transform, accent);
            accentImage.raycastTarget = false;
            Stretch(accentImage.rectTransform, new Vector2(0.018f, 0.18f), new Vector2(0.032f, 0.82f), Vector2.zero, Vector2.zero);

            Text text = CreateText("Label", image.transform, label, 18, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            return button;
        }

        private void ConfigureButtonSprites(Button button)
        {
            if (button == null || theme == null || theme.ButtonHighlighted == null)
            {
                return;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = theme.ButtonHighlighted;
            state.pressedSprite = theme.ButtonHighlighted;
            state.selectedSprite = theme.ButtonHighlighted;
            button.spriteState = state;
        }

        private static bool ApplySprite(Image image, Sprite sprite, Image.Type type)
        {
            if (image == null || sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.type = type;
            image.color = Color.white;
            return true;
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor anchor)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Text text = obj.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetFixed(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private static void SetBarFill(RectTransform rect, float ratio)
        {
            if (rect == null)
            {
                return;
            }

            float safeRatio = Mathf.Clamp01(ratio);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(safeRatio, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string StateLabel(DungeonGameState state)
        {
            switch (state)
            {
                case DungeonGameState.Paused:
                    return "PAUSED";
                case DungeonGameState.RewardSelection:
                    return "CHOOSE REWARD";
                case DungeonGameState.GameOver:
                    return "GAME OVER";
                case DungeonGameState.Win:
                    return "VICTORY";
                default:
                    return string.Empty;
            }
        }

        private static Color CardColor(PerkRarity2D rarity)
        {
            switch (rarity)
            {
                case PerkRarity2D.Rare:
                    return new Color(0.29f, 0.17f, 0.08f, 0.96f);
                case PerkRarity2D.Uncommon:
                    return new Color(0.1f, 0.2f, 0.24f, 0.96f);
                default:
                    return new Color(0.13f, 0.14f, 0.18f, 0.96f);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventSystem.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
