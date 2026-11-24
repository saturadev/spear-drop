using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Screen UI: title and stats, crosshair, power bar, slow-motion pill, the hit label next to the wound,
// the dark hit overlay and the round message. All text is TextMeshPro with Barlow (OFL).
public class SpearHud : MonoBehaviour
{
    public TextMeshProUGUI score, dummies, spears, slowText, hint, hitPart, hitPoints, bigText, subText;
    public RectTransform canvasRect, hitPanel, slowPill, hintPill, powerBar, powerFill;
    public Image overlay;
    public CanvasGroup hitGroup, bigGroup, slowGroup, hintGroup, powerGroup;

    Transform hitAnchor; Vector3 hitLocal;
    float hitAlpha, overlayAlpha, bigAlpha, slowAlpha, hintAlpha, powerAlpha;
    public float overlayTarget, hitTarget, bigTarget, slowTarget, hintTarget, powerTarget;

    static readonly Color Yellow = new Color(1f, 0.80f, 0.16f);
    static readonly Color Ink = new Color(0.06f, 0.07f, 0.09f, 0.88f);

    public static SpearHud Build()
    {
        var g = new GameObject("HUD");
        var canvas = g.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        var scaler = g.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var h = g.AddComponent<SpearHud>();
        h.canvasRect = g.GetComponent<RectTransform>();
        h.Layout();
        return h;
    }

    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform));
        var r = g.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    Image Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, bool rounded = false)
    {
        var r = Rect(name, parent, anchor, pivot, pos, size);
        var i = r.gameObject.AddComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
        if (rounded) { i.sprite = SpearKit.Rounded(); i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f; }
        return i;
    }

    TextMeshProUGUI Txt(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string s, float fontSize, bool heavy, bool outline, Color c, TextAlignmentOptions align)
    {
        var r = Rect(name, parent, anchor, pivot, pos, size);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = heavy ? SpearKit.Heavy : SpearKit.Bold;
        if (heavy && outline) t.fontSharedMaterial = SpearKit.HeavyOutline;
        t.text = s;
        t.fontSize = fontSize;
        t.color = c;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    CanvasGroup Group(GameObject g) { var cg = g.AddComponent<CanvasGroup>(); cg.alpha = 0; cg.interactable = false; cg.blocksRaycasts = false; return cg; }

    void Layout()
    {
        var root = transform;
        overlay = Img("Overlay", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.04f, 0.10f, 0f));
        var orect = overlay.rectTransform; orect.anchorMin = Vector2.zero; orect.anchorMax = Vector2.one; orect.sizeDelta = Vector2.zero;

        // top left: title and dummies; top right: score and spears
        Txt("Title", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -50), new Vector2(600, 110), "SPEAR DROP", 76, true, true, Color.white, TextAlignmentOptions.TopLeft);
        dummies = Txt("Dummies", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -146), new Vector2(560, 60), "DUMMIES DOWN 0/5", 40, true, true, Yellow, TextAlignmentOptions.TopLeft);
        Txt("ScoreLabel", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -50), new Vector2(400, 50), "SCORE", 38, true, true, Yellow, TextAlignmentOptions.TopRight);
        score = Txt("Score", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-48, -84), new Vector2(400, 120), "0", 88, true, true, Color.white, TextAlignmentOptions.TopRight);
        spears = Txt("Spears", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -196), new Vector2(400, 60), "SPEARS 8", 40, true, true, Color.white, TextAlignmentOptions.TopRight);

        // crosshair
        var ch = Rect("Crosshair", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        foreach (var (p, s) in new[] { (new Vector2(0, 15), new Vector2(5, 16)), (new Vector2(0, -15), new Vector2(5, 16)), (new Vector2(15, 0), new Vector2(16, 5)), (new Vector2(-15, 0), new Vector2(16, 5)) })
        {
            Img("shadow", ch, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, s + new Vector2(4, 4), new Color(0, 0, 0, 0.45f));
            Img("bar", ch, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, s, Color.white);
        }

        // bottom: power bar, slow motion pill, control hint
        powerBar = Img("Power", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(420, 22), Ink, true).rectTransform;
        powerFill = Img("Fill", powerBar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(0, 12), Yellow, true).rectTransform;
        powerGroup = Group(powerBar.gameObject);

        slowPill = Img("SlowPill", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(470, 84), Ink, true).rectTransform;
        slowText = Txt("SlowText", slowPill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(460, 80), "SLOW MOTION 0.25x", 46, true, false, Yellow, TextAlignmentOptions.Center);
        slowGroup = Group(slowPill.gameObject);

        hintPill = Img("HintPill", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(640, 76), Ink, true).rectTransform;
        hint = Txt("Hint", hintPill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(630, 70), "HOLD TO AIM  ·  RELEASE TO THROW", 36, false, false, Color.white, TextAlignmentOptions.Center);
        hintGroup = Group(hintPill.gameObject);

        // hit label: a light tag with a red edge, next to the wound
        hitPanel = Rect("HitLabel", root, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(560, 72));
        Img("tag", hitPanel, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(560, 72), new Color(0.98f, 0.94f, 0.93f, 0.97f), true);
        Img("edge", hitPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(12, 44), new Color(0.80f, 0.08f, 0.10f), true);
        hitPart = Txt("Part", hitPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -8), new Vector2(400, 56), "HEAD — frontal bone", 36, false, false, new Color(0.50f, 0.04f, 0.06f), TextAlignmentOptions.Left);
        hitPoints = Txt("Points", hitPanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -8), new Vector2(150, 56), "+100", 40, true, false, new Color(0.80f, 0.08f, 0.10f), TextAlignmentOptions.Right);
        hitGroup = Group(hitPanel.gameObject);

        var big = Rect("Message", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1000, 280));
        bigText = Txt("Big", big, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1000, 160), "ALL DOWN", 120, true, true, Color.white, TextAlignmentOptions.Top);
        subText = Txt("Sub", big, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -165), new Vector2(1000, 80), "SCORE 0", 50, true, true, Yellow, TextAlignmentOptions.Top);
        bigGroup = Group(big.gameObject);
    }

    public void SetStats(int scoreV, int down, int total, int spearsLeft)
    {
        score.text = scoreV.ToString();
        dummies.text = "DUMMIES DOWN " + down + "/" + total;
        spears.text = "SPEARS " + spearsLeft;
    }

    public void ShowHit(Transform anchor, Vector3 local, string part, int points)
    {
        hitAnchor = anchor; hitLocal = local;
        hitPart.text = part;
        hitPoints.text = points > 0 ? "+" + points : "";
        hitTarget = 1f;
        PlaceHit();
    }

    public void HideHit() { hitTarget = 0f; }

    public void ShowMessage(string big, string sub) { bigText.text = big; subText.text = sub; bigTarget = 1f; }
    public void HideMessage() { bigTarget = 0f; }

    public void SetSlow(float scale)
    {
        slowText.text = "SLOW MOTION " + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x";
    }

    public void SetPower(float c) { powerFill.sizeDelta = new Vector2(Mathf.Max(12f, c * 410f), 12f); }

    void PlaceHit()
    {
        var cam = Camera.main;
        if (cam == null || hitAnchor == null) return;
        var w = hitAnchor.TransformPoint(hitLocal);
        var sp = cam.WorldToScreenPoint(w);
        if (sp.z < 0) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var lp);
        var size = canvasRect.rect.size;
        var p = lp + size * 0.5f + new Vector2(110, 70);
        p.x = Mathf.Clamp(p.x, 30, size.x - 590);
        p.y = Mathf.Clamp(p.y, 330, size.y - 420);
        hitPanel.anchoredPosition = p;
    }

    void LateUpdate()
    {
        float rdt = SpearKit.RealDt;
        float k = 1f - Mathf.Exp(-10f * rdt);
        hitAlpha = Mathf.Lerp(hitAlpha, hitTarget, k);
        overlayAlpha = Mathf.Lerp(overlayAlpha, overlayTarget, 1f - Mathf.Exp(-6f * rdt));
        bigAlpha = Mathf.Lerp(bigAlpha, bigTarget, k);
        slowAlpha = Mathf.Lerp(slowAlpha, slowTarget, k);
        hintAlpha = Mathf.Lerp(hintAlpha, hintTarget, k);
        powerAlpha = Mathf.Lerp(powerAlpha, powerTarget, 1f - Mathf.Exp(-16f * rdt));
        hitGroup.alpha = hitAlpha;
        bigGroup.alpha = bigAlpha;
        slowGroup.alpha = slowAlpha;
        hintGroup.alpha = hintAlpha;
        powerGroup.alpha = powerAlpha;
        var c = overlay.color; c.a = overlayAlpha; overlay.color = c;
        if (hitAlpha > 0.01f) PlaceHit();
    }
}
