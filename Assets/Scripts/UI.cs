using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hàm tiện ích dựng UI bằng code. Sprite nằm trong Assets/Sprites/Resources/UI.
public static class UI
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    public static readonly Color Cyan = new Color(0.25f, 0.8f, 0.95f);
    public static readonly Color LightBlue = new Color(0.66f, 0.85f, 0.94f);

    public static Sprite S(string name)
    {
        if (!cache.TryGetValue(name, out var s))
        {
            s = Resources.Load<Sprite>("UI/" + name);
            if (s == null) Debug.LogWarning($"Không tìm thấy sprite UI/{name}");
            cache[name] = s;
        }
        return s;
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    // Phủ kín cha, cách mép theo offset
    public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    // Vùng theo tỉ lệ 0..1 của cha (gốc dưới-trái)
    public static RectTransform Area(RectTransform rt, Rect area)
    {
        rt.anchorMin = area.min;
        rt.anchorMax = area.max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // Đặt theo điểm neo (vd. (0.5, 1) = giữa-trên), vị trí và kích thước
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Image(Transform parent, string name, string sprite, Vector2 size, bool sliced = false)
    {
        var rt = Rect(parent, name);
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite != null ? S(sprite) : null;
        img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    public static TMP_Text Text(Transform parent, string name, string text, float size, Color color, Vector2 rectSize)
    {
        var rt = Rect(parent, name);
        rt.sizeDelta = rectSize;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    public static TMP_Text Outline(TMP_Text t, float width, Color32 color)
    {
        t.outlineWidth = width;
        t.outlineColor = color;
        return t;
    }

    // Nút có nền sprite 9-slice, có thể kèm icon và chữ
    public static Button Button(Transform parent, string name, string sprite, Vector2 size,
                                string label, float fontSize, UnityAction onClick, string icon = null)
    {
        var img = Image(parent, name, sprite, size, true);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => AudioManager.Play(Sfx.Click));
        btn.onClick.AddListener(onClick);
        img.gameObject.AddComponent<ButtonFx>();

        float lip = size.y * 0.05f;   // nền có viền dày phía dưới, đẩy nội dung lên chút
        if (icon != null && string.IsNullOrEmpty(label))
        {
            float s = size.y * 0.62f;
            Place(Image(img.transform, "Icon", icon, Vector2.zero).rectTransform,
                  new Vector2(0.5f, 0.5f), new Vector2(0f, lip), new Vector2(s, s));
        }
        else if (icon != null)
        {
            float s = size.y * 0.55f;
            Place(Image(img.transform, "Icon", icon, Vector2.zero).rectTransform,
                  new Vector2(0f, 0.5f), new Vector2(size.y * 0.55f, lip), new Vector2(s, s));
            var t = Outline(Text(img.transform, "Label", label, fontSize, Color.white, Vector2.zero), 0.18f, new Color32(0, 0, 0, 90));
            Stretch(t.rectTransform, size.y * 0.85f, 0, 20, 0).anchoredPosition += new Vector2(0, lip);
        }
        else if (!string.IsNullOrEmpty(label))
        {
            var t = Outline(Text(img.transform, "Label", label, fontSize, Color.white, Vector2.zero), 0.18f, new Color32(0, 0, 0, 90));
            Stretch(t.rectTransform, 16, 0, 16, 0).anchoredPosition += new Vector2(0, lip);
        }
        return btn;
    }

    // Nút chỉ có icon (bánh răng, quay lại, đóng...)
    public static Button IconButton(Transform parent, string name, string icon, Vector2 size, UnityAction onClick)
    {
        var img = Image(parent, name, icon, size);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => AudioManager.Play(Sfx.Click));
        btn.onClick.AddListener(onClick);
        img.gameObject.AddComponent<ButtonFx>();
        return btn;
    }

    // Lớp nền tối phủ màn hình, chặn bấm xuyên
    public static Image Dim(Transform parent, float alpha = 0.65f)
    {
        var img = Image(parent, "Dim", null, Vector2.zero);
        Stretch(img.rectTransform);
        img.color = new Color(0.02f, 0.04f, 0.15f, alpha);
        img.raycastTarget = true;
        return img;
    }

    // Panel phủ kín màn hình (dùng cho các màn hình), có CanvasGroup để làm hiệu ứng hiện dần
    public static RectTransform Screen(Transform parent, string name)
    {
        var rt = Stretch(Rect(parent, name));
        rt.gameObject.AddComponent<CanvasGroup>();
        return rt;
    }

    // Ảnh nền luôn lấp đầy màn hình mà không méo (cắt bớt mép nếu cần)
    public static Image Background(Transform parent, Sprite sprite)
    {
        var img = Image(parent, "Background", null, Vector2.zero);
        img.sprite = sprite;
        img.raycastTarget = true;
        var fit = img.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 0.5625f;
        if (sprite == null) img.color = new Color(0.08f, 0.12f, 0.35f);
        return img;
    }
}

// Hiệu ứng nhún khi bấm nút
public class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    float target = 1f;
    Selectable selectable;

    void Awake() { selectable = GetComponent<Selectable>(); }

    public void OnPointerDown(PointerEventData e)
    {
        if (selectable == null || selectable.interactable) target = 0.92f;
    }

    public void OnPointerUp(PointerEventData e) { target = 1f; }
    public void OnPointerExit(PointerEventData e) { target = 1f; }

    void OnDisable()
    {
        target = 1f;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }
}
