using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Một khối nằm trong khay, kéo thả lên bàn.
public class Piece : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
{
    public Vector2Int[] Cells { get; private set; }
    public int ColorIndex { get; private set; }

    GameManager game;
    BoardManager board;
    RectTransform canvasRect;
    RectTransform visual;        // phần hình, được tách ra Canvas khi kéo
    Image[] images;
    int width, height;
    float trayScale;
    Vector3 targetScale;
    bool dragging;

    public float liftOffset = 120f;   // khối nằm trên ngón tay bao xa khi kéo

    public void Init(GameManager game, BoardManager board, RectTransform slot, RectTransform canvasRect,
                     Vector2Int[] cells, int colorIndex)
    {
        this.game = game;
        this.board = board;
        this.canvasRect = canvasRect;
        Cells = cells;
        ColorIndex = colorIndex;

        foreach (var o in cells)
        {
            height = Mathf.Max(height, o.x + 1);
            width = Mathf.Max(width, o.y + 1);
        }

        // Vùng bắt chuột = cả ô slot
        var rt = (RectTransform)transform;
        rt.SetParent(slot, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var hit = gameObject.AddComponent<Image>();
        hit.color = Color.clear;

        float cs = board.cellSize;
        visual = new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
        visual.SetParent(rt, false);
        visual.sizeDelta = new Vector2(width * cs, height * cs);

        images = new Image[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            var go = new GameObject("Block", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(visual, false);
            var crt = (RectTransform)go.transform;
            crt.sizeDelta = new Vector2(cs, cs);
            crt.anchoredPosition = CellLocalPos(cells[i].x, cells[i].y);
            var img = go.GetComponent<Image>();
            img.sprite = board.blockSprites[colorIndex];
            img.raycastTarget = false;
            images[i] = img;
        }

        float maxSide = Mathf.Max(width, height) * cs;
        trayScale = Mathf.Min(0.55f, slot.rect.width * 0.9f / maxSide);
        visual.localScale = Vector3.zero;    // hiệu ứng "bật" ra khi xuất hiện
        targetScale = Vector3.one * trayScale;
    }

    // Tâm của ô (r, c) trong hệ tọa độ của visual (pivot ở giữa)
    Vector2 CellLocalPos(int r, int c)
    {
        float cs = board.cellSize;
        return new Vector2((c - (width - 1) / 2f) * cs, -(r - (height - 1) / 2f) * cs);
    }

    public void SetPlaceable(bool placeable)
    {
        var color = placeable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        foreach (var img in images) img.color = color;
    }

    void Update()
    {
        float k = 1f - Mathf.Exp(-20f * Time.deltaTime);
        visual.localScale = Vector3.Lerp(visual.localScale, targetScale, k);

        // Thả ra ở chỗ không hợp lệ -> bay về khay
        if (!dragging && visual.parent != transform)
        {
            visual.position = Vector3.Lerp(visual.position, transform.position, k);
            if ((visual.position - transform.position).sqrMagnitude < 1f)
            {
                visual.SetParent(transform, false);
                visual.anchoredPosition = Vector2.zero;
            }
        }
    }

    public void OnInitializePotentialDrag(PointerEventData e)
    {
        e.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!game.IsPlaying) return;
        dragging = true;
        visual.SetParent(canvasRect, true);
        visual.SetAsLastSibling();
        targetScale = Vector3.one;
        Follow(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        Follow(e);
        GetCell(out int r, out int c);
        board.ShowPreview(Cells, r, c, ColorIndex);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;
        board.ClearPreview();

        GetCell(out int r, out int c);
        if (game.IsPlaying && board.CanPlace(Cells, r, c))
            game.PlacePiece(this, r, c);
        else
            targetScale = Vector3.one * trayScale;
    }

    void Follow(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, e.position, e.pressEventCamera, out Vector2 lp);
        visual.localPosition = lp + new Vector2(0f, liftOffset + height * board.cellSize * 0.5f);
    }

    // Ô trên lưới mà ô (0,0) của khối đang nằm trên
    void GetCell(out int row, out int col)
    {
        Vector3 origin = visual.TransformPoint(CellLocalPos(0, 0));
        board.WorldToCell(origin, out row, out col);
    }

    void OnDestroy()
    {
        if (visual != null && visual.parent != transform)
            Destroy(visual.gameObject);
    }
}
