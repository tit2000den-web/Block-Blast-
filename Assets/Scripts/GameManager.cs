using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum GameMode { Classic, Adventure }

// Điều khiển luồng game: 2 chế độ Classic / Adventure, sinh 3 khối, tính điểm, combo, booster, thắng/thua.
// Phần dựng các màn hình nằm ở GameScreens.cs.
[RequireComponent(typeof(BoardManager))]
public partial class GameManager : MonoBehaviour
{
    const string BestKey = "BestScore";

    enum Booster { None, Bomb, Hammer }

    [Header("Kéo thả trong Inspector (để trống sẽ tự tìm theo tên)")]
    public BoardManager board;
    public RectTransform[] slots;      // Slot1, Slot2, Slot3
    public TMP_Text scoreText;

    [Header("Điểm")]
    public int pointsPerLine = 10;
    public int boardClearBonus = 300;
    public int comboKeepMoves = 3;     // số lượt không xóa hàng trước khi mất combo

    [Header("Booster")]
    public int classicBombs = 2;
    public int classicHammers = 2;
    public int levelBombs = 1;
    public int levelHammers = 1;
    public int comboRewardEvery = 5;   // Classic: đạt combo 5, 10, ... được tặng 1 booster

    [Header("Màn hình (ảnh full màn, nút đã vẽ sẵn trong ảnh)")]
    public Sprite homeScreenSprite;      // màn hình trang chủ.png
    public Sprite gameOverScreenSprite;  // màn hình game over.png

    // Người chơi có đang được kéo thả khối không
    public bool IsPlaying => running && !paused && booster == Booster.None;

    GameMode mode;
    int level;
    bool running, paused;
    Booster booster;
    int bombs, hammers;

    Piece[] pieces;
    RectTransform canvasRect;
    Vector2 boardHome;
    int score, best, combo, movesSinceClear;
    float displayScore;
    readonly List<GameObject> popups = new List<GameObject>();

    void Awake()
    {
        if (board == null) board = GetComponent<BoardManager>();
        if (slots == null || slots.Length == 0)
        {
            slots = new RectTransform[3];
            for (int i = 0; i < 3; i++)
                slots[i] = (RectTransform)GameObject.Find($"Slot{i + 1}").transform;
        }
        if (scoreText == null) scoreText = GameObject.Find("ScoreText").GetComponent<TMP_Text>();

        canvasRect = (RectTransform)board.boardRect.GetComponentInParent<Canvas>().rootCanvas.transform;
        boardHome = board.boardRect.anchoredPosition;
        pieces = new Piece[slots.Length];
        board.OnCellTapped = OnCellTapped;
        AudioManager.Ensure();
    }

    void Start()
    {
        best = PlayerPrefs.GetInt(BestKey, 0);
        BuildUI();
        ShowHome();
    }

    void Update()
    {
        // Điểm chạy tăng dần cho đẹp
        displayScore = Mathf.MoveTowards(displayScore, score, Mathf.Max(1f, (score - displayScore) * 8f) * Time.deltaTime);
        int shown = Mathf.RoundToInt(displayScore);
        scoreText.text = shown.ToString();
        if (mode == GameMode.Adventure)
            levelBar.Set(shown, Levels.Target(level));
    }

    // ---------- bắt đầu / kết thúc ván ----------

    public void StartClassic()
    {
        mode = GameMode.Classic;
        StartRun();
    }

    public void StartLevel(int n)
    {
        mode = GameMode.Adventure;
        level = Mathf.Clamp(n, 1, Levels.Count);
        StartRun();
    }

    public void Replay()
    {
        if (mode == GameMode.Adventure) StartLevel(level);
        else StartClassic();
    }

    void StartRun()
    {
        StopAllCoroutines();
        ClearPopups();
        HideAllScreens();
        board.boardRect.anchoredPosition = boardHome;

        board.ResetBoard();
        if (mode == GameMode.Adventure) board.SetObstacles(Levels.Obstacles(level));
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null) Destroy(pieces[i].gameObject);
            pieces[i] = null;
        }

        score = 0;
        displayScore = 0;
        combo = 0;
        movesSinceClear = 0;
        bombs = mode == GameMode.Classic ? classicBombs : levelBombs;
        hammers = mode == GameMode.Classic ? classicHammers : levelHammers;
        booster = Booster.None;
        paused = false;
        running = true;

        SetupHud();
        SpawnPieces();
        RefreshPlaceable();
    }

    void EndRun(bool win)
    {
        running = false;
        booster = Booster.None;
        HideHint();
        UpdateBoosterUI();
        StartCoroutine(EndRoutine(win));
    }

    IEnumerator EndRoutine(bool win)
    {
        PlayerPrefs.Save();
        yield return new WaitForSeconds(0.8f);

        if (mode == GameMode.Classic)
        {
            AudioManager.Play(Sfx.Lose);
            ShowGameOver();
        }
        else if (win)
        {
            if (level > Levels.Completed) Levels.Completed = level;
            AudioManager.Play(Sfx.Win);
            ShowWin();
        }
        else
        {
            AudioManager.Play(Sfx.Lose);
            ShowLose();
        }
    }

    // ---------- khối trong khay ----------

    void SpawnPieces()
    {
        // Thử vài lần để ít nhất 1 khối đặt được (tránh thua oan)
        var shapes = new Vector2Int[slots.Length][];
        for (int attempt = 0; attempt < 20; attempt++)
        {
            bool anyFits = false;
            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i] = BlockShapes.Random();
                if (board.CanPlaceAnywhere(shapes[i])) anyFits = true;
            }
            if (anyFits) break;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            var go = new GameObject("Piece", typeof(RectTransform));
            var piece = go.AddComponent<Piece>();
            int color = Random.Range(0, board.blockSprites.Length);
            piece.Init(this, board, slots[i], canvasRect, shapes[i], color);
            pieces[i] = piece;
        }
    }

    public void PlacePiece(Piece piece, int row, int col)
    {
        int lines = board.Place(piece.Cells, row, col, piece.ColorIndex);
        int gained = piece.Cells.Length;
        AudioManager.Play(Sfx.Place);

        int index = System.Array.IndexOf(pieces, piece);
        pieces[index] = null;
        Destroy(piece.gameObject);

        if (lines > 0)
        {
            combo++;
            movesSinceClear = 0;
            int linePoints = pointsPerLine * lines * lines * combo;
            gained += linePoints;
            AudioManager.Play(Sfx.Clear, 1f + 0.06f * Mathf.Min(combo - 1, 10));
            AudioManager.Vibrate();

            string msg = lines > 1 ? $"{lines} LINES!" : "";
            if (combo > 1) msg += (msg.Length > 0 ? "\n" : "") + $"Combo {combo}";
            if (board.IsEmpty())
            {
                gained += boardClearBonus;
                msg += (msg.Length > 0 ? "\n" : "") + "CLEAR!";
            }
            ShowPopup($"+{linePoints}" + (msg.Length > 0 ? "\n" + msg : ""));

            if (mode == GameMode.Classic && comboRewardEvery > 0 && combo % comboRewardEvery == 0)
                GiveRandomBooster();
        }
        else if (++movesSinceClear >= comboKeepMoves)
        {
            combo = 0;
        }

        score += gained;
        if (mode == GameMode.Classic && score > best)
        {
            best = score;
            PlayerPrefs.SetInt(BestKey, best);
        }

        bool trayEmpty = true;
        foreach (var p in pieces)
            if (p != null) trayEmpty = false;
        if (trayEmpty) SpawnPieces();

        AfterMove();
    }

    // Sau mỗi lượt: kiểm tra thắng (Adventure) hoặc hết chỗ đặt
    void AfterMove()
    {
        UpdateHudTexts();

        if (mode == GameMode.Adventure && score >= Levels.Target(level))
        {
            EndRun(true);
            return;
        }

        if (!RefreshPlaceable())
        {
            if (bombs + hammers > 0) ShowHint("No space! Use a booster", 2.5f);
            else EndRun(false);
        }
    }

    // Làm mờ các khối không còn chỗ đặt. Trả về true nếu còn ít nhất 1 khối đặt được.
    bool RefreshPlaceable()
    {
        bool any = false;
        foreach (var p in pieces)
        {
            if (p == null) continue;
            bool ok = board.CanPlaceAnywhere(p.Cells);
            p.SetPlaceable(ok);
            any |= ok;
        }
        return any;
    }

    // ---------- booster ----------

    void SelectBooster(Booster b)
    {
        if (!running || paused) return;
        if (booster == b)
        {
            CancelBooster();
            return;
        }

        int count = b == Booster.Bomb ? bombs : hammers;
        if (count <= 0)
        {
            ShowHint(b == Booster.Bomb ? "No Boom left!" : "No Hammer left!", 1.5f);
            return;
        }

        booster = b;
        ShowHint(b == Booster.Bomb ? "Tap the board to drop the Boom" : "Tap a block to destroy it", 0f);
        UpdateBoosterUI();
    }

    void CancelBooster()
    {
        booster = Booster.None;
        HideHint();
        UpdateBoosterUI();
    }

    void OnCellTapped(int r, int c)
    {
        if (!running || paused || booster == Booster.None) return;
        var center = new Vector2Int(r, c);

        if (booster == Booster.Bomb)
        {
            var cells = new List<Vector2Int>();
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    cells.Add(new Vector2Int(r + dr, c + dc));
            if (board.ClearCells(cells, center) == 0)
            {
                ShowHint("Nothing to blow up here!", 1.5f);
                return;
            }
            bombs--;
            AudioManager.Play(Sfx.Bomb);
            AudioManager.Vibrate();
            StartCoroutine(ShakeBoard());
        }
        else
        {
            if (!board.IsFilled(r, c))
            {
                ShowHint("Tap a block to destroy it", 0f);
                return;
            }
            board.ClearCells(new[] { center }, center);
            hammers--;
            AudioManager.Play(Sfx.Hammer);
        }

        CancelBooster();
        AfterMove();
    }

    void GiveRandomBooster()
    {
        if (Random.value < 0.5f) { bombs++; ShowPopup("+1 Boom!", -260f); }
        else { hammers++; ShowPopup("+1 Hammer!", -260f); }
        UpdateBoosterUI();
    }

    IEnumerator ShakeBoard()
    {
        var rt = board.boardRect;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.3f;
            rt.anchoredPosition = boardHome + Random.insideUnitCircle * 18f * k;
            yield return null;
        }
        rt.anchoredPosition = boardHome;
    }

    // ---------- chữ nổi lên giữa bàn ----------

    void ShowPopup(string message, float yOffset = 0f)
    {
        var t = UI.Text(canvasRect, "Popup", message, 90, new Color(1f, 0.9f, 0.3f), new Vector2(900f, 400f));
        t.textWrappingMode = TMPro.TextWrappingModes.Normal;
        UI.Outline(t, 0.2f, new Color32(40, 20, 0, 255));
        t.transform.position = board.boardRect.position;
        t.rectTransform.anchoredPosition += new Vector2(0f, yOffset);
        popups.Add(t.gameObject);
        StartCoroutine(PopupRoutine(t));
    }

    IEnumerator PopupRoutine(TMP_Text t)
    {
        var rt = t.rectTransform;
        Vector2 start = rt.anchoredPosition;
        const float duration = 1f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float k = time / duration;
            rt.anchoredPosition = start + new Vector2(0f, 150f * k);
            rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.1f, k / 0.15f) : 1f);
            t.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            yield return null;
        }
        popups.Remove(t.gameObject);
        Destroy(t.gameObject);
    }

    void ClearPopups()
    {
        foreach (var p in popups)
            if (p != null) Destroy(p);
        popups.Clear();
    }
}
