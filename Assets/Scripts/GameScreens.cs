using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Phần giao diện của GameManager: HUD trong game và các màn hình
// (Trang chủ, Adventure, Settings, More Settings, Game Over, You Win, So Close).
public partial class GameManager
{
    // Thanh tiến độ điểm (Adventure)
    class ProgressBar
    {
        public RectTransform fill;
        public TMP_Text label;
        public float width;

        public void Set(int value, int target)
        {
            float p = target > 0 ? Mathf.Clamp01((float)value / target) : 0f;
            fill.gameObject.SetActive(p > 0f);
            fill.sizeDelta = new Vector2(Mathf.Lerp(52f, width - 12f, p), -12f);
            label.text = $"{value} / {target}";
        }
    }

    class ToggleRow
    {
        public Image image;
        public System.Func<bool> get;
    }

    // HUD
    RectTransform hud, classicHud, adventureHud;
    TMP_Text bestText, levelTitle, hintText;
    GameObject hintBox;
    ProgressBar levelBar;
    Image bombButton, hammerButton;
    TMP_Text bombCount, hammerCount;
    Coroutine hintRoutine;

    // Màn hình
    GameObject homePanel, gameOverPanel, mapPanel, winPanel, losePanel, settingsPanel, morePanel, infoPanel;
    readonly List<GameObject> screens = new List<GameObject>();

    TMP_Text gameOverScoreText, gameOverBestText;
    TMP_Text winLevelText, winScoreText, winTargetText;
    TMP_Text loseScoreText, loseTipText;
    ProgressBar loseBar;
    TMP_Text infoTitle, infoBody;
    GameObject replayButton, homeButton;
    readonly List<ToggleRow> toggles = new List<ToggleRow>();

    // Adventure map
    ScrollRect mapScroll;
    RectTransform mapContent;
    Image[] cardBg;
    Image[] cardStatus;
    TMP_Text playLevelLabel;
    int selectedLevel = 1;
    const float CardHeight = 200f, CardGap = 28f;

    void BuildUI()
    {
        BuildHud();
        BuildHome();
        BuildGameOver();
        BuildAdventureMap();
        BuildWin();
        BuildLose();
        BuildSettings();
        BuildMoreSettings();
        BuildInfo();
        HideAllScreens();
    }

    // ================= HUD =================

    void BuildHud()
    {
        hud = UI.Stretch(UI.Rect(canvasRect, "HUD"));

        UI.Place(UI.IconButton(hud, "SettingsButton", "icon_gear", Vector2.one * 110f, () => OpenSettings(true)).transform as RectTransform,
                 new Vector2(1f, 1f), new Vector2(-85f, -85f), Vector2.one * 110f);

        // Classic: vương miện + điểm cao nhất
        classicHud = UI.Stretch(UI.Rect(hud, "Classic"));
        UI.Place(UI.Image(classicHud, "Crown", "icon_crown", Vector2.zero).rectTransform,
                 new Vector2(0f, 1f), new Vector2(85f, -85f), Vector2.one * 96f);
        bestText = UI.Text(classicHud, "BestText", "0", 56, UI.Gold, Vector2.zero);
        bestText.alignment = TextAlignmentOptions.Left;
        UI.Outline(bestText, 0.15f, new Color32(60, 30, 0, 200));
        UI.Place(bestText.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -88f), new Vector2(300f, 90f));

        // Adventure: tên màn + thanh tiến độ tới điểm mục tiêu
        adventureHud = UI.Stretch(UI.Rect(hud, "Adventure"));
        levelTitle = UI.Outline(UI.Text(adventureHud, "LevelTitle", "Level 1", 66, Color.white, Vector2.zero), 0.2f, new Color32(10, 20, 60, 255));
        UI.Place(levelTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(600f, 100f));
        levelBar = BuildBar(adventureHud, new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(640f, 60f));
        var target = UI.Text(adventureHud, "TargetLabel", "Target", 34, UI.Cyan, Vector2.zero);
        UI.Place(target.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(400f, 50f));

        // Booster: Boom (nổ 3x3) bên trái, Hammer (phá 1 khối) bên phải
        bombButton = BuildBooster("BoomButton", "icon_bomb", -400f, out bombCount, () => SelectBooster(Booster.Bomb));
        hammerButton = BuildBooster("HammerButton", "icon_hammer", 400f, out hammerCount, () => SelectBooster(Booster.Hammer));

        // Dòng gợi ý ở giữa 2 nút booster
        var box = UI.Image(hud, "Hint", "row", Vector2.zero, true);
        UI.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -378f), new Vector2(560f, 96f));
        hintBox = box.gameObject;
        hintText = UI.Text(box.transform, "Text", "", 38, Color.white, Vector2.zero);
        hintText.textWrappingMode = TextWrappingModes.Normal;
        hintText.enableAutoSizing = true;
        hintText.fontSizeMin = 24f;
        hintText.fontSizeMax = 38f;
        UI.Stretch(hintText.rectTransform, 20, 6, 20, 6);
        hintBox.SetActive(false);
    }

    Image BuildBooster(string name, string icon, float x, out TMP_Text countText, UnityEngine.Events.UnityAction onClick)
    {
        var btn = UI.Button(hud, name, "row", Vector2.one * 140f, null, 0, onClick, icon);
        UI.Place((RectTransform)btn.transform, new Vector2(0.5f, 0.5f), new Vector2(x, -378f), Vector2.one * 140f);
        var badge = UI.Image(btn.transform, "Badge", "btn_red", Vector2.zero, true);
        UI.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(64f, 60f));
        countText = UI.Text(badge.transform, "Count", "0", 36, Color.white, Vector2.zero);
        UI.Stretch(countText.rectTransform, 0, 6, 0, 0);
        return btn.GetComponent<Image>();
    }

    ProgressBar BuildBar(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var bg = UI.Image(parent, "Bar", "progress_bg", Vector2.zero, true);
        UI.Place(bg.rectTransform, anchor, pos, size);
        var fill = UI.Image(bg.transform, "Fill", "progress_fill", Vector2.zero, true).rectTransform;
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = new Vector2(6f, 0f);
        var label = UI.Outline(UI.Text(bg.transform, "Label", "0 / 0", size.y * 0.55f, Color.white, Vector2.zero), 0.25f, new Color32(10, 20, 60, 255));
        UI.Stretch(label.rectTransform);
        return new ProgressBar { fill = fill, label = label, width = size.x };
    }

    void SetupHud()
    {
        bool adventure = mode == GameMode.Adventure;
        classicHud.gameObject.SetActive(!adventure);
        adventureHud.gameObject.SetActive(adventure);
        scoreText.gameObject.SetActive(!adventure);
        levelTitle.text = $"Level {level}";
        HideHint();
        UpdateHudTexts();
    }

    void UpdateHudTexts()
    {
        bestText.text = best.ToString();
        UpdateBoosterUI();
    }

    void UpdateBoosterUI()
    {
        StyleBooster(bombButton, bombCount, bombs, booster == Booster.Bomb);
        StyleBooster(hammerButton, hammerCount, hammers, booster == Booster.Hammer);
    }

    static void StyleBooster(Image button, TMP_Text count, int n, bool selected)
    {
        button.sprite = UI.S(selected ? "panel" : "row");
        button.color = n > 0 || selected ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        count.text = n.ToString();
    }

    // duration = 0: hiện đến khi bị tắt
    void ShowHint(string message, float duration)
    {
        hintText.text = message;
        hintBox.SetActive(true);
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = duration > 0f ? StartCoroutine(HideHintLater(duration)) : null;
    }

    IEnumerator HideHintLater(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        hintRoutine = null;
        // Nếu đang chọn booster thì quay lại câu hướng dẫn của booster
        if (booster == Booster.Bomb) hintText.text = "Tap the board to drop the Boom";
        else if (booster == Booster.Hammer) hintText.text = "Tap a block to destroy it";
        else hintBox.SetActive(false);
    }

    void HideHint()
    {
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = null;
        if (hintBox != null) hintBox.SetActive(false);
    }

    // ================= quản lý màn hình =================

    GameObject AddScreen(RectTransform rt)
    {
        screens.Add(rt.gameObject);
        return rt.gameObject;
    }

    void HideAllScreens()
    {
        foreach (var s in screens) s.SetActive(false);
    }

    void ShowScreen(GameObject screen)
    {
        screen.SetActive(true);
        screen.transform.SetAsLastSibling();
        StartCoroutine(FadeIn(screen.GetComponent<CanvasGroup>()));
    }

    IEnumerator FadeIn(CanvasGroup group)
    {
        const float duration = 0.2f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / duration;
            yield return null;
        }
        group.alpha = 1f;
    }

    public void ShowHome()
    {
        StopAllCoroutines();
        running = false;
        paused = false;
        booster = Booster.None;
        HideHint();
        ClearPopups();
        HideAllScreens();
        ShowScreen(homePanel);
    }

    // ================= Trang chủ =================

    void BuildHome()
    {
        var screen = UI.Screen(canvasRect, "HomePanel");
        homePanel = AddScreen(screen);
        var art = UI.Background(screen, homeScreenSprite).rectTransform;
        // Vị trí nút tính theo tỉ lệ (0..1) trên ảnh, gốc ở góc dưới-trái
        CreateImageButton(art, "AdventureButton", new Rect(0.32f, 0.483f, 0.40f, 0.074f), ShowAdventureMap);
        CreateImageButton(art, "ClassicButton", new Rect(0.32f, 0.380f, 0.40f, 0.074f), StartClassic);
        UI.Place(UI.IconButton(screen, "SettingsButton", "icon_gear", Vector2.one * 110f, () => OpenSettings(false)).transform as RectTransform,
                 new Vector2(1f, 1f), new Vector2(-85f, -85f), Vector2.one * 110f);
    }

    // Nút trong suốt đặt đè lên nút đã vẽ trong ảnh. Bấm vào sẽ tối nhẹ lại.
    void CreateImageButton(RectTransform parent, string name, Rect area, UnityEngine.Events.UnityAction onClick)
    {
        var img = UI.Image(parent, name, null, Vector2.zero);
        UI.Area(img.rectTransform, area);
        img.raycastTarget = true;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0f);
        colors.pressedColor = new Color(0f, 0f, 0f, 0.25f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
        btn.onClick.AddListener(() => AudioManager.Play(Sfx.Click));
        btn.onClick.AddListener(onClick);
    }

    // ================= Game Over (Classic) =================

    void BuildGameOver()
    {
        var screen = UI.Screen(canvasRect, "GameOverPanel");
        gameOverPanel = AddScreen(screen);
        var art = UI.Background(screen, gameOverScreenSprite).rectTransform;

        gameOverScoreText = UI.Text(art, "Score", "0", 160, Color.white, Vector2.zero);
        UI.Area(gameOverScoreText.rectTransform, new Rect(0.25f, 0.49f, 0.50f, 0.09f));
        gameOverScoreText.enableAutoSizing = true;
        gameOverScoreText.fontSizeMin = 40f;
        gameOverScoreText.fontSizeMax = 160f;

        gameOverBestText = UI.Text(art, "Best", "", 56, UI.Gold, Vector2.zero);
        UI.Area(gameOverBestText.rectTransform, new Rect(0.25f, 0.455f, 0.50f, 0.035f));
        gameOverBestText.enableAutoSizing = true;
        gameOverBestText.fontSizeMin = 24f;
        gameOverBestText.fontSizeMax = 56f;

        CreateImageButton(art, "HomeButton", new Rect(0.115f, 0.290f, 0.352f, 0.065f), ShowHome);
        CreateImageButton(art, "PlayButton", new Rect(0.519f, 0.290f, 0.345f, 0.065f), StartClassic);
    }

    void ShowGameOver()
    {
        gameOverScoreText.text = score.ToString();
        gameOverBestText.text = score >= best && score > 0 ? "NEW BEST SCORE!" : $"BEST {best}";
        ShowScreen(gameOverPanel);
    }

    // ================= Adventure: chọn màn =================

    void BuildAdventureMap()
    {
        var screen = UI.Screen(canvasRect, "AdventurePanel");
        mapPanel = AddScreen(screen);
        UI.Background(screen, UI.S("bg_home"));

        UI.Place(UI.IconButton(screen, "BackButton", "icon_back", Vector2.one * 100f, ShowHome).transform as RectTransform,
                 new Vector2(0f, 1f), new Vector2(85f, -85f), Vector2.one * 100f);
        var title = UI.Outline(UI.Text(screen, "Title", "Adventure", 76, Color.white, Vector2.zero), 0.2f, new Color32(20, 20, 80, 255));
        UI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(700f, 110f));
        UI.Place(UI.Image(screen, "Logo", "logo", Vector2.zero).rectTransform,
                 new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(600f, 378f));

        // Danh sách màn (cuộn dọc)
        var viewport = UI.Image(screen, "Viewport", null, Vector2.zero);
        viewport.color = Color.clear;
        viewport.raycastTarget = true;
        UI.Stretch(viewport.rectTransform, 60f, 290f, 60f, 540f);
        viewport.gameObject.AddComponent<RectMask2D>();

        mapContent = UI.Rect(viewport.transform, "Content");
        mapContent.anchorMin = new Vector2(0f, 1f);
        mapContent.anchorMax = new Vector2(1f, 1f);
        mapContent.pivot = new Vector2(0.5f, 1f);
        mapContent.sizeDelta = new Vector2(0f, CardGap + Levels.Count * (CardHeight + CardGap));

        mapScroll = viewport.gameObject.AddComponent<ScrollRect>();
        mapScroll.viewport = viewport.rectTransform;
        mapScroll.content = mapContent;
        mapScroll.horizontal = false;
        mapScroll.scrollSensitivity = 40f;

        cardBg = new Image[Levels.Count];
        cardStatus = new Image[Levels.Count];
        for (int i = 0; i < Levels.Count; i++) BuildLevelCard(i + 1);

        var play = UI.Button(screen, "PlayLevelButton", "btn_green", new Vector2(560f, 150f), "Level 1", 64, () => StartLevel(selectedLevel), "icon_play");
        UI.Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(560f, 150f));
        playLevelLabel = play.transform.Find("Label").GetComponent<TMP_Text>();
    }

    void BuildLevelCard(int lv)
    {
        int i = lv - 1;
        var btn = UI.Button(mapContent, $"Level{lv}", "row", new Vector2(0f, CardHeight), null, 0, () => SelectLevel(lv));
        var rt = (RectTransform)btn.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(-20f, CardHeight);
        rt.anchoredPosition = new Vector2(0f, -(CardGap + i * (CardHeight + CardGap) + CardHeight / 2f));
        cardBg[i] = btn.GetComponent<Image>();

        // Xem trước bàn chơi với các khối đá
        const float cell = 18f, gap = 2f;
        float boardSize = BoardManager.Size * (cell + gap) + gap;
        var mini = UI.Image(rt, "Preview", null, Vector2.zero);
        mini.color = new Color(0.04f, 0.08f, 0.25f, 1f);
        UI.Place(mini.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f + boardSize / 2f, 0f), Vector2.one * boardSize);
        var stones = new HashSet<Vector2Int>(Levels.Obstacles(lv));
        for (int r = 0; r < BoardManager.Size; r++)
            for (int c = 0; c < BoardManager.Size; c++)
            {
                bool stone = stones.Contains(new Vector2Int(r, c));
                var img = UI.Image(mini.transform, "C", stone ? "block_stone" : null, Vector2.one * cell);
                img.color = stone ? Color.white : new Color(0.2f, 0.3f, 0.6f, 0.5f);
                UI.Place(img.rectTransform, new Vector2(0f, 1f),
                         new Vector2(gap + c * (cell + gap) + cell / 2f, -(gap + r * (cell + gap) + cell / 2f)), Vector2.one * cell);
            }

        var name = UI.Outline(UI.Text(rt, "Name", $"Level {lv}", 58, Color.white, Vector2.zero), 0.15f, new Color32(0, 0, 40, 200));
        name.alignment = TextAlignmentOptions.Left;
        UI.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(230f + 200f, 26f), new Vector2(400f, 80f));
        var target = UI.Text(rt, "Target", $"Target: {Levels.Target(lv)}", 38, UI.LightBlue, Vector2.zero);
        target.alignment = TextAlignmentOptions.Left;
        UI.Place(target.rectTransform, new Vector2(0f, 0.5f), new Vector2(230f + 200f, -38f), new Vector2(400f, 60f));

        cardStatus[i] = UI.Image(rt, "Status", "icon_lock", Vector2.zero);
        UI.Place(cardStatus[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(-90f, 0f), Vector2.one * 96f);
    }

    void ShowAdventureMap()
    {
        HideAllScreens();
        selectedLevel = Levels.Unlocked;
        RefreshMap();
        ShowScreen(mapPanel);

        // Cuộn tới màn đang chọn
        Canvas.ForceUpdateCanvases();
        float viewH = mapScroll.viewport.rect.height;
        float maxY = Mathf.Max(0f, mapContent.rect.height - viewH);
        float y = CardGap + (selectedLevel - 1) * (CardHeight + CardGap) + CardHeight / 2f - viewH / 2f;
        mapContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, maxY));
        mapScroll.velocity = Vector2.zero;
    }

    void SelectLevel(int lv)
    {
        if (!Levels.IsUnlocked(lv))
        {
            AudioManager.Vibrate();
            return;
        }
        selectedLevel = lv;
        RefreshMap();
    }

    void RefreshMap()
    {
        for (int i = 0; i < Levels.Count; i++)
        {
            int lv = i + 1;
            bool unlocked = Levels.IsUnlocked(lv);
            cardBg[i].sprite = UI.S(lv == selectedLevel ? "panel" : "row");
            cardBg[i].color = unlocked ? Color.white : new Color(0.7f, 0.7f, 0.8f, 0.7f);
            cardStatus[i].sprite = UI.S(Levels.IsDone(lv) ? "icon_star" : unlocked ? "icon_play" : "icon_lock");
        }
        playLevelLabel.text = $"Level {selectedLevel}";
    }

    // ================= You Win / So Close (Adventure) =================

    void BuildWin()
    {
        var screen = UI.Screen(canvasRect, "WinPanel");
        winPanel = AddScreen(screen);
        UI.Background(screen, UI.S("bg_dark"));

        var title = UI.Outline(UI.Text(screen, "Title", "YOU WIN", 140, UI.LightBlue, Vector2.zero), 0.2f, new Color32(30, 50, 90, 255));
        UI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 560f), new Vector2(1000f, 200f));

        var frame = UI.Image(screen, "Frame", "frame", Vector2.zero);
        UI.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(760f, 680f));
        winLevelText = UI.Text(frame.transform, "Level", "LEVEL 1", 56, UI.Cyan, Vector2.zero);
        UI.Place(winLevelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(560f, 80f));
        winScoreText = UI.Text(frame.transform, "Score", "0", 150, Color.white, Vector2.zero);
        UI.Place(winScoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(560f, 190f));
        winScoreText.enableAutoSizing = true;
        winScoreText.fontSizeMin = 40f;
        winScoreText.fontSizeMax = 150f;
        winTargetText = UI.Text(frame.transform, "Target", "", 44, UI.Gold, Vector2.zero);
        UI.Place(winTargetText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -115f), new Vector2(560f, 70f));

        var home = UI.Button(screen, "HomeButton", "btn_blue", new Vector2(340f, 130f), null, 0, ShowAdventureMap, "icon_home");
        UI.Place((RectTransform)home.transform, new Vector2(0.5f, 0.5f), new Vector2(-195f, -450f), new Vector2(340f, 130f));
        var next = UI.Button(screen, "NextButton", "btn_yellow", new Vector2(340f, 130f), null, 0, NextLevel, "icon_play");
        UI.Place((RectTransform)next.transform, new Vector2(0.5f, 0.5f), new Vector2(195f, -450f), new Vector2(340f, 130f));
    }

    void ShowWin()
    {
        winLevelText.text = $"LEVEL {level}";
        winScoreText.text = score.ToString();
        winTargetText.text = $"TARGET {Levels.Target(level)}";
        ShowScreen(winPanel);
    }

    void NextLevel()
    {
        if (level < Levels.Count) StartLevel(level + 1);
        else ShowAdventureMap();
    }

    void BuildLose()
    {
        var screen = UI.Screen(canvasRect, "LosePanel");
        losePanel = AddScreen(screen);
        UI.Background(screen, UI.S("bg_dark"));

        var title = UI.Outline(UI.Text(screen, "Title", "So Close!", 140, UI.Cyan, Vector2.zero), 0.2f, new Color32(10, 30, 80, 255));
        UI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 500f), new Vector2(1000f, 200f));

        var label = UI.Text(screen, "ScoreLabel", "SCORE", 64, UI.Cyan, Vector2.zero);
        UI.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(600f, 90f));
        loseScoreText = UI.Text(screen, "Score", "0", 130, Color.white, Vector2.zero);
        UI.Place(loseScoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(800f, 160f));
        loseBar = BuildBar(screen, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(720f, 70f));
        loseTipText = UI.Text(screen, "Tip", "", 42, UI.LightBlue, Vector2.zero);
        UI.Place(loseTipText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(900f, 70f));

        var retry = UI.Button(screen, "RetryButton", "btn_green", new Vector2(520f, 150f), "Retry", 66, Replay, "icon_retry");
        UI.Place((RectTransform)retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(520f, 150f));
        var home = UI.Button(screen, "HomeButton", "btn_blue", new Vector2(520f, 130f), "Levels", 56, ShowAdventureMap, "icon_home");
        UI.Place((RectTransform)home.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -500f), new Vector2(520f, 130f));
    }

    void ShowLose()
    {
        int target = Levels.Target(level);
        loseScoreText.text = score.ToString();
        loseBar.Set(score, target);
        loseTipText.text = $"Only {Mathf.Max(0, target - score)} more points to win!";
        ShowScreen(losePanel);
    }

    // ================= Settings =================

    // Panel giữa màn hình, tự co giãn theo nội dung
    RectTransform BuildPopup(string name, string titleText, out GameObject screenGo, UnityEngine.Events.UnityAction onClose, bool backIcon = false)
    {
        var screen = UI.Screen(canvasRect, name);
        screenGo = AddScreen(screen);
        var dim = UI.Dim(screen);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(onClose);

        var panel = UI.Image(screen, "Panel", "panel", Vector2.zero, true);
        panel.raycastTarget = true;
        var prt = UI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 600f));
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(60, 60, 40, 60);
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = UI.Outline(UI.Text(prt, "Title", titleText, 76, Color.white, Vector2.zero), 0.2f, new Color32(10, 20, 60, 255));
        Layout(title.gameObject, 120f);

        var close = UI.IconButton(prt, "CloseButton", backIcon ? "icon_back" : "icon_close", Vector2.one * 84f, onClose);
        close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        UI.Place((RectTransform)close.transform, backIcon ? new Vector2(0f, 1f) : new Vector2(1f, 1f),
                 backIcon ? new Vector2(80f, -78f) : new Vector2(-80f, -78f), Vector2.one * 84f);
        return prt;
    }

    static LayoutElement Layout(GameObject go, float height)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return le;
    }

    void BuildSettings()
    {
        var panel = BuildPopup("SettingsPanel", "Settings", out settingsPanel, CloseSettings);

        AddToggleRow(panel, "icon_sound", "Sound", () => AudioManager.SoundOn, v => AudioManager.SoundOn = v);
        AddToggleRow(panel, "icon_music", "Music", () => AudioManager.MusicOn, v => AudioManager.MusicOn = v);
        AddToggleRow(panel, "icon_vibrate", "Vibration", () => AudioManager.VibrationOn, v =>
        {
            AudioManager.VibrationOn = v;
            if (v) AudioManager.Vibrate();
        });

        // Nút trong game (ẩn khi mở từ trang chủ)
        replayButton = UI.Button(panel, "ReplayButton", "btn_green", Vector2.zero, "Replay", 54, Replay, "icon_retry").gameObject;
        Layout(replayButton, 120f);
        homeButton = UI.Button(panel, "BackHomeButton", "btn_blue", Vector2.zero, "Back to Home", 54, ShowHome, "icon_home").gameObject;
        Layout(homeButton, 120f);
        var more = UI.Button(panel, "MoreButton", "btn_yellow", Vector2.zero, "More Settings", 54, OpenMoreSettings, "icon_gear").gameObject;
        Layout(more, 120f);
        FixButtonSize(replayButton, 120f);
        FixButtonSize(homeButton, 120f);
        FixButtonSize(more, 120f);
    }

    // Nút tạo với size 0 rồi để layout giãn: đặt lại icon theo chiều cao thật
    static void FixButtonSize(GameObject button, float height)
    {
        var icon = (RectTransform)button.transform.Find("Icon");
        float s = height * 0.55f;
        icon.sizeDelta = new Vector2(s, s);
        icon.anchoredPosition = new Vector2(height * 0.6f, height * 0.05f);
        var label = (RectTransform)button.transform.Find("Label");
        UI.Stretch(label, height * 0.9f, 0f, height * 0.4f, 0f).anchoredPosition += new Vector2(0f, height * 0.05f);
    }

    void AddToggleRow(RectTransform panel, string icon, string label, System.Func<bool> get, System.Action<bool> set)
    {
        var row = UI.Image(panel, label + "Row", "row", Vector2.zero, true);
        Layout(row.gameObject, 112f);
        UI.Place(UI.Image(row.transform, "Icon", icon, Vector2.zero).rectTransform,
                 new Vector2(0f, 0.5f), new Vector2(64f, 0f), Vector2.one * 72f);
        var text = UI.Text(row.transform, "Label", label, 50, Color.white, Vector2.zero);
        text.alignment = TextAlignmentOptions.Left;
        UI.Stretch(text.rectTransform, 125f, 0f, 200f, 0f);

        var toggle = new ToggleRow { get = get };
        var btn = UI.IconButton(row.transform, "Toggle", "toggle_on", new Vector2(150f, 80f), () =>
        {
            set(!get());
            RefreshToggles();
        });
        UI.Place((RectTransform)btn.transform, new Vector2(1f, 0.5f), new Vector2(-100f, 0f), new Vector2(150f, 80f));
        toggle.image = btn.GetComponent<Image>();
        toggles.Add(toggle);
    }

    void RefreshToggles()
    {
        foreach (var t in toggles)
            t.image.sprite = UI.S(t.get() ? "toggle_on" : "toggle_off");
    }

    void OpenSettings(bool inGame)
    {
        if (inGame && !running) return;
        if (inGame)
        {
            paused = true;
            if (booster != Booster.None) CancelBooster();
        }
        replayButton.SetActive(inGame);
        homeButton.SetActive(inGame);
        RefreshToggles();
        ShowScreen(settingsPanel);
    }

    void CloseSettings()
    {
        PlayerPrefs.Save();
        settingsPanel.SetActive(false);
        paused = false;
    }

    // ================= More Settings / thông tin =================

    void BuildMoreSettings()
    {
        var panel = BuildPopup("MoreSettingsPanel", "More Settings", out morePanel, CloseMoreSettings, true);

        var app = UI.Image(panel, "AppInfo", "row", Vector2.zero, true);
        Layout(app.gameObject, 190f);
        UI.Place(UI.Image(app.transform, "Logo", "logo", Vector2.zero).rectTransform,
                 new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(230f, 145f));
        var name = UI.Text(app.transform, "Name", "Block Crush!", 50, Color.white, Vector2.zero);
        name.alignment = TextAlignmentOptions.Left;
        UI.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(480f, 28f), new Vector2(400f, 70f));
        var version = UI.Text(app.transform, "Version", $"Version {Application.version}", 38, UI.LightBlue, Vector2.zero);
        version.alignment = TextAlignmentOptions.Left;
        UI.Place(version.rectTransform, new Vector2(0f, 0.5f), new Vector2(480f, -34f), new Vector2(400f, 60f));

        AddLinkRow(panel, "icon_doc", "Terms of Service",
            "By playing Block Crush! you agree to play fair and have fun.\n\nThe game is provided as-is for learning purposes. Scores and progress are stored only on this device.");
        AddLinkRow(panel, "icon_shield", "Privacy Policy",
            "Block Crush! does not collect, store or share any personal data.\n\nYour settings, best score and Adventure progress are saved locally on your device only.");
        AddLinkRow(panel, "icon_info", "About Us",
            "Block Crush! is a block puzzle game made with Unity.\n\nDrag blocks onto the 8x8 board, fill rows or columns to clear them and build combos for big scores!");
    }

    void AddLinkRow(RectTransform panel, string icon, string label, string body)
    {
        var btn = UI.Button(panel, label + "Row", "row", Vector2.zero, null, 0, () => ShowInfo(label, body));
        Layout(btn.gameObject, 112f);
        UI.Place(UI.Image(btn.transform, "Icon", icon, Vector2.zero).rectTransform,
                 new Vector2(0f, 0.5f), new Vector2(64f, 0f), Vector2.one * 68f);
        var text = UI.Text(btn.transform, "Label", label, 48, Color.white, Vector2.zero);
        text.alignment = TextAlignmentOptions.Left;
        UI.Stretch(text.rectTransform, 125f, 0f, 90f, 0f);
        var arrow = UI.Image(btn.transform, "Arrow", "icon_play", Vector2.zero);
        UI.Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(-60f, 0f), Vector2.one * 44f);
    }

    void OpenMoreSettings()
    {
        settingsPanel.SetActive(false);
        ShowScreen(morePanel);
    }

    void CloseMoreSettings()
    {
        morePanel.SetActive(false);
        ShowScreen(settingsPanel);
    }

    void BuildInfo()
    {
        var panel = BuildPopup("InfoPanel", "Info", out infoPanel, CloseInfo);
        infoTitle = panel.Find("Title").GetComponent<TMP_Text>();
        infoBody = UI.Text(panel, "Body", "", 42, UI.LightBlue, Vector2.zero);
        infoBody.textWrappingMode = TextWrappingModes.Normal;
        infoBody.alignment = TextAlignmentOptions.TopLeft;
        infoBody.fontStyle = FontStyles.Normal;
        Layout(infoBody.gameObject, 420f);
        var ok = UI.Button(panel, "OkButton", "btn_green", Vector2.zero, "OK", 56, CloseInfo).gameObject;
        Layout(ok, 120f);
    }

    void ShowInfo(string title, string body)
    {
        infoTitle.text = title;
        infoBody.text = body;
        morePanel.SetActive(false);
        ShowScreen(infoPanel);
    }

    void CloseInfo()
    {
        infoPanel.SetActive(false);
        ShowScreen(morePanel);
    }
}
