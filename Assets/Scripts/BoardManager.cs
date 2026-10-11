using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    public const int Size = 8;                 // lưới 8x8
    public const int Stone = 99;               // index "màu" của khối đá (chướng ngại vật Adventure)
    const int Empty = -1;

    [Header("Kéo thả trong Inspector")]
    public RectTransform boardRect;            // object Board
    public Sprite[] blockSprites;              // các sprite màu
    public Sprite stoneSprite;                 // để trống sẽ tự lấy UI/block_stone
    public float padding = 5f;                 // viền của board_bg

    // Gọi khi người chơi chạm vào 1 ô trên bàn (dùng cho booster)
    public System.Action<int, int> OnCellTapped;

    [Header("Hiệu ứng")]
    [Range(0f, 1f)] public float ghostAlpha = 0.4f;   // độ mờ của bóng khối khi kéo
    public float clearDuration = 0.3f;                // thời gian hiệu ứng xóa hàng

    [HideInInspector] public float cellSize;   // tự tính

    int[,] colors = new int[Size, Size];       // Dữ liệu: -1 = trống, >= 0 = index màu
    Image[,] cellImages = new Image[Size, Size]; // Hình ảnh của từng ô
    readonly List<GameObject> fxObjects = new List<GameObject>();

    void Awake()
    {
        cellSize = (boardRect.rect.width - padding * 2f) / Size;
        if (stoneSprite == null) stoneSprite = UI.S("block_stone");
        BuildGrid();
        ResetBoard();
        boardRect.gameObject.AddComponent<BoardTapHandler>().board = this;
    }

    public bool IsFilled(int r, int c) => IsInside(r, c) && colors[r, c] != Empty;

    // Đặt sẵn các khối đá lên bàn
    public void SetObstacles(IEnumerable<Vector2Int> cells)
    {
        foreach (var p in cells)
            if (IsInside(p.x, p.y)) colors[p.x, p.y] = Stone;
        Refresh();
    }

    // Xóa các ô (booster), hiệu ứng lan ra từ tâm. Trả về số ô có khối đã bị xóa.
    public int ClearCells(IEnumerable<Vector2Int> cells, Vector2Int center)
    {
        int count = 0;
        foreach (var p in cells)
        {
            if (!IsFilled(p.x, p.y)) continue;
            float delay = Vector2Int.Distance(p, center) * 0.05f;
            SpawnClearFx(p.x, p.y, colors[p.x, p.y], delay);
            colors[p.x, p.y] = Empty;
            count++;
        }
        Refresh();
        return count;
    }

    // Tạo 64 ô Image bằng code, xếp theo hàng cột
    void BuildGrid()
    {
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                var img = CreateCellImage($"Cell_{r}_{c}", r, c);
                cellImages[r, c] = img;
            }
        }
    }

    Image CreateCellImage(string name, int r, int c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(boardRect, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // neo ở góc trên-trái Board
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cellSize, cellSize);
        rt.anchoredPosition = new Vector2(
            padding + c * cellSize + cellSize / 2f,
            -(padding + r * cellSize + cellSize / 2f));

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;     // ô không cần nhận chuột
        img.color = Color.clear;       // ô trống = trong suốt
        return img;
    }

    public void ResetBoard()
    {
        StopAllCoroutines();
        foreach (var fx in fxObjects)
            if (fx != null) Destroy(fx);
        fxObjects.Clear();

        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                colors[r, c] = Empty;
        Refresh();
    }

    public bool IsInside(int r, int c)
    {
        return r >= 0 && r < Size && c >= 0 && c < Size;
    }

    public bool IsEmpty()
    {
        foreach (int v in colors)
            if (v != Empty) return false;
        return true;
    }

    // Khối (cells) đặt tại (row, col) có hợp lệ không?
    // Hợp lệ = MỌI ô đều nằm trong lưới VÀ không đè ô đã có khối
    public bool CanPlace(Vector2Int[] cells, int row, int col)
    {
        foreach (var o in cells)
        {
            int r = row + o.x;
            int c = col + o.y;
            if (!IsInside(r, c)) return false;
            if (colors[r, c] != Empty) return false;
        }
        return true;
    }

    // Có chỗ nào trên bàn đặt được khối này không?
    public bool CanPlaceAnywhere(Vector2Int[] cells)
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                if (CanPlace(cells, r, c)) return true;
        return false;
    }

    // Đặt khối (gọi sau khi đã CanPlace = true), xóa các hàng/cột đầy.
    // Trả về số hàng + cột đã xóa.
    public int Place(Vector2Int[] cells, int row, int col, int colorIndex)
    {
        foreach (var o in cells)
            colors[row + o.x, col + o.y] = colorIndex;

        var fullRows = new List<int>();
        var fullCols = new List<int>();
        FindFullLines(colors, fullRows, fullCols);

        // Lưu lại màu cũ để làm hiệu ứng, rồi mới xóa dữ liệu
        var cleared = new Dictionary<Vector2Int, float>();   // ô -> độ trễ hiệu ứng
        foreach (int r in fullRows)
            for (int c = 0; c < Size; c++)
                AddClearedCell(cleared, r, c, c);
        foreach (int c in fullCols)
            for (int r = 0; r < Size; r++)
                AddClearedCell(cleared, r, c, r);

        foreach (var kv in cleared)
        {
            int r = kv.Key.x, c = kv.Key.y;
            SpawnClearFx(r, c, colors[r, c], kv.Value);
            colors[r, c] = Empty;
        }

        Refresh();
        return fullRows.Count + fullCols.Count;
    }

    static void AddClearedCell(Dictionary<Vector2Int, float> cleared, int r, int c, int order)
    {
        var key = new Vector2Int(r, c);
        float delay = order * 0.025f;
        if (!cleared.TryGetValue(key, out float d) || delay < d)
            cleared[key] = delay;
    }

    static void FindFullLines(int[,] grid, List<int> rows, List<int> cols)
    {
        for (int r = 0; r < Size; r++)
        {
            bool full = true;
            for (int c = 0; c < Size && full; c++)
                if (grid[r, c] == Empty) full = false;
            if (full) rows.Add(r);
        }
        for (int c = 0; c < Size; c++)
        {
            bool full = true;
            for (int r = 0; r < Size && full; r++)
                if (grid[r, c] == Empty) full = false;
            if (full) cols.Add(c);
        }
    }

    // Đổi điểm (world) sang ô (row, col) trên lưới. Có thể nằm ngoài lưới.
    public void WorldToCell(Vector3 worldPos, out int row, out int col)
    {
        Vector2 local = boardRect.InverseTransformPoint(worldPos);
        Rect rect = boardRect.rect;
        float x = local.x - rect.xMin - padding;
        float y = rect.yMax - padding - local.y;
        col = Mathf.FloorToInt(x / cellSize);
        row = Mathf.FloorToInt(y / cellSize);
    }

    // Hiện bóng mờ của khối + tô sáng các hàng/cột sẽ bị xóa
    public void ShowPreview(Vector2Int[] cells, int row, int col, int colorIndex)
    {
        Refresh();
        if (!CanPlace(cells, row, col)) return;

        var grid = (int[,])colors.Clone();
        foreach (var o in cells)
            grid[row + o.x, col + o.y] = colorIndex;

        foreach (var o in cells)
        {
            var img = cellImages[row + o.x, col + o.y];
            img.sprite = blockSprites[colorIndex];
            img.color = new Color(1f, 1f, 1f, ghostAlpha);
        }

        var fullRows = new List<int>();
        var fullCols = new List<int>();
        FindFullLines(grid, fullRows, fullCols);
        foreach (int r in fullRows)
            for (int c = 0; c < Size; c++)
                SetImage(cellImages[r, c], colorIndex);
        foreach (int c in fullCols)
            for (int r = 0; r < Size; r++)
                SetImage(cellImages[r, c], colorIndex);
    }

    public void ClearPreview()
    {
        Refresh();
    }

    // Vẽ lại toàn bộ ô theo dữ liệu
    void Refresh()
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                SetImage(cellImages[r, c], colors[r, c]);
    }

    void SetImage(Image img, int colorIndex)
    {
        if (colorIndex != Empty)
        {
            img.sprite = colorIndex == Stone ? stoneSprite : blockSprites[colorIndex];
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = Color.clear;
        }
    }

    void SpawnClearFx(int r, int c, int colorIndex, float delay)
    {
        var img = CreateCellImage("ClearFx", r, c);
        SetImage(img, colorIndex);
        fxObjects.Add(img.gameObject);
        StartCoroutine(ClearFxRoutine(img, delay));
    }

    IEnumerator ClearFxRoutine(Image img, float delay)
    {
        yield return new WaitForSeconds(delay);
        var rt = img.rectTransform;
        float t = 0f;
        while (t < clearDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / clearDuration);
            float scale = k < 0.3f ? Mathf.Lerp(1f, 1.15f, k / 0.3f) : Mathf.Lerp(1.15f, 0f, (k - 0.3f) / 0.7f);
            rt.localScale = Vector3.one * scale;
            img.color = new Color(1f, 1f, 1f, 1f - k * k);
            yield return null;
        }
        fxObjects.Remove(img.gameObject);
        Destroy(img.gameObject);
    }
}

// Bắt sự kiện chạm lên bàn và đổi ra ô (hàng, cột)
public class BoardTapHandler : MonoBehaviour, IPointerClickHandler
{
    public BoardManager board;

    public void OnPointerClick(PointerEventData e)
    {
        if (board.OnCellTapped == null) return;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(board.boardRect, e.position, e.pressEventCamera, out Vector3 world))
            return;
        board.WorldToCell(world, out int r, out int c);
        if (board.IsInside(r, c)) board.OnCellTapped(r, c);
    }
}
