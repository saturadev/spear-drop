using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Small helpers: materials, primitives, procedural textures and sprites, fonts.
public static class SpearKit
{
    static Shader lit, particles;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    public static Material Mat(Color c, float smooth = 0.15f, float metal = 0f)
    {
        string key = c + "|" + smooth + "|" + metal;
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(lit);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        cache[key] = m;
        return m;
    }

    public static Material TexMat(Texture2D tex, Vector2 tiling, Color tint, float smooth = 0.1f)
    {
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(lit);
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", tiling);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        return m;
    }

    public static Material ParticleMat(Color c)
    {
        if (particles == null) particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(particles);
        m.SetColor("_BaseColor", c);
        return m;
    }

    public static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material m, bool keepCollider = false, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        if (!keepCollider) Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = rot ?? Quaternion.identity;
        g.transform.localScale = localScale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider = true)
        => Prim(PrimitiveType.Cube, name, parent, pos, size, m, collider);

    // Light grey floor tiles: 4x4 tiles per texture, each tile a slightly different shade, thin grout lines.
    public static Texture2D GridTexture()
    {
        const int n = 256, tiles = 4, cell = n / tiles;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        var rnd = new System.Random(7);
        var shades = new float[tiles * tiles];
        for (int i = 0; i < shades.Length; i++) shades[i] = 0.93f + (float)rnd.NextDouble() * 0.07f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int tx = x / cell, ty = y / cell;
                int lx = x % cell, ly = y % cell;
                float v = shades[ty * tiles + tx];
                bool grout = lx < 2 || ly < 2;
                bool bevel = lx < 4 || ly < 4;
                if (grout) v *= 0.80f; else if (bevel) v *= 0.94f;
                // a faint inner panel line, like a test-map tile
                if (!grout && (lx == cell / 2 || ly == cell / 2)) v *= 0.985f;
                byte b = (byte)(Mathf.Clamp01(v) * 255);
                px[y * n + x] = new Color32(b, b, (byte)Mathf.Min(255, b + 6), 255);
            }
        tex.SetPixels32(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        tex.Apply(true, true);
        return tex;
    }

    static Sprite round;
    // A white rounded square with soft edges, 9-sliced: pills and panels.
    public static Sprite Rounded()
    {
        if (round != null) return round;
        const int n = 64; const float r = 30f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply(false, true);
        round = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
        return round;
    }

    static TMP_FontAsset heavy, bold;
    static Material heavyOutline;
    public static TMP_FontAsset Heavy => heavy != null ? heavy : (heavy = Resources.Load<TMP_FontAsset>("Barlow-ExtraBold SDF"));
    public static TMP_FontAsset Bold => bold != null ? bold : (bold = Resources.Load<TMP_FontAsset>("Barlow-Bold SDF"));
    public static Material HeavyOutline => heavyOutline != null ? heavyOutline : (heavyOutline = Resources.Load<Material>("Barlow-ExtraBold Outline"));

    // Move and stretch a unit capsule/cylinder so it spans a to b.
    public static void Span(Transform t, Vector3 a, Vector3 b, float thickness)
    {
        var d = b - a;
        t.position = (a + b) * 0.5f;
        t.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        t.localScale = new Vector3(thickness, d.magnitude * 0.5f, thickness);
    }

    public static float RealDt => Time.timeScale > 0.0001f ? Time.deltaTime / Time.timeScale : Time.unscaledDeltaTime;
}
