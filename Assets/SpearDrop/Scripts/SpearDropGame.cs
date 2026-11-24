using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using GameTouch;

// Spear Drop: a first-person spear-throwing range. Drop all five test dummies with eight spears.
// The bootstrap builds the whole map at runtime, runs the slow motion and keeps score.
public class SpearDropGame : MonoBehaviour
{
    public static SpearDropGame I;

    public const int SpearsPerRound = 8;
    public SpearThrower player;
    public SpearHud hud;
    public readonly List<Dummy> dummies = new List<Dummy>();
    public int score, best, round, spearsLeft, throws, hits, heads;
    public bool busy, roundOver;
    public string lastResult = "";
    public float slow = 1f, slowTarget = 1f;

    Transform level, dynamicRoot;
    TextMeshPro boardLeft, boardRight;
    readonly List<string> log = new List<string>();
    ParticleSystem puffPrefab, dustPrefab;
    Spear tracked;
    bool resolving;
    TouchPad touch;

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        BuildWorld();
        player = SpearThrower.Build(new Vector3(0, 0, 0), 0f);
        player.onThrow = OnThrow;
        hud = SpearHud.Build();
        // phones: stick on the left to walk, drag elsewhere to look, hold THROW to pull back and release to throw
        touch = TouchPad.Create(SpearKit.Heavy, SpearKit.HeavyOutline);
        touch.AddStick(new Rect(0, 0, 0.45f, 0.55f), new Vector2(0, 0), new Vector2(240, 380), 140);
        player.ThrowButton = touch.AddButton("THROW", new Vector2(1, 0), new Vector2(-195, 380), 130, new Color(1f, 0.80f, 0.16f), 44);
        NewRound();
    }

    void OnDestroy()
    {
        if (I == this) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; }
    }

    // ---------------- world ----------------

    struct Spot { public Vector3 a, b; public Spot(Vector3 a, Vector3 b) { this.a = a; this.b = b; } }
    static readonly Spot[] spots =
    {
        new Spot(new Vector3(-4.2f, 1.4f, 15f), new Vector3(1.2f, 1.4f, 15f)),   // pacing on the near ledge
        new Spot(new Vector3(7.5f, 3.2f, 21f), new Vector3(7.5f, 3.2f, 21f)),    // standing on the tall block
        new Spot(new Vector3(-10f, 2.6f, 24.5f), new Vector3(-10f, 2.6f, 24.5f)),// on top of the steps
        new Spot(new Vector3(-2f, 0f, 30f), new Vector3(5f, 0f, 30f)),           // walking on the floor
        new Spot(new Vector3(-1.5f, 2f, 38f), new Vector3(4.5f, 2f, 38f)),       // pacing on the far ledge
    };

    void BuildWorld()
    {
        level = new GameObject("Level").transform;

        // sun, sky and haze: bright blue sky, hard shadows, pale horizon
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(level);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.5f;
        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sunGo.transform.rotation = Quaternion.Euler(34f, 172f, 0f);
        RenderSettings.sun = sun;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.80f, 0.86f, 0.95f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 320f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.90f);
        RenderSettings.ambientEquatorColor = new Color(0.66f, 0.70f, 0.78f);
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.47f, 0.52f);
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.035f);
        sky.SetFloat("_AtmosphereThickness", 0.75f);
        sky.SetColor("_SkyTint", new Color(0.42f, 0.55f, 0.85f));
        sky.SetColor("_GroundColor", new Color(0.62f, 0.66f, 0.72f));
        sky.SetFloat("_Exposure", 1.25f);
        RenderSettings.skybox = sky;

        var floorMat = SpearKit.TexMat(SpearKit.GridTexture(), new Vector2(75, 75), new Color(0.70f, 0.72f, 0.78f), 0.12f);
        var floor = SpearKit.Box("Floor", level, new Vector3(0, -0.5f, 60), new Vector3(600, 1, 600), floorMat);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        var block = SpearKit.Mat(new Color(0.68f, 0.71f, 0.78f), 0.08f);
        var blockDark = SpearKit.Mat(new Color(0.58f, 0.61f, 0.69f), 0.08f);
        SpearKit.Box("Ledge Near", level, new Vector3(-1.5f, 0.7f, 15f), new Vector3(9f, 1.4f, 3.5f), block);
        SpearKit.Box("Block Tall", level, new Vector3(7.5f, 1.6f, 21f), new Vector3(3f, 3.2f, 3f), block);
        SpearKit.Box("Step Low", level, new Vector3(-9f, 0.5f, 23f), new Vector3(6f, 1f, 5f), blockDark);
        SpearKit.Box("Step High", level, new Vector3(-10f, 1.3f, 24.5f), new Vector3(3.5f, 2.6f, 3f), block);
        SpearKit.Box("Ledge Far", level, new Vector3(1.5f, 1.0f, 38f), new Vector3(10f, 2f, 3.5f), block);
        SpearKit.Box("Ledge Far Step", level, new Vector3(-4.5f, 0.5f, 38f), new Vector3(2.5f, 1f, 3.5f), blockDark);
        SpearKit.Box("Pillar", level, new Vector3(15f, 3f, 31f), new Vector3(2f, 6f, 2f), block);
        SpearKit.Box("Wall", level, new Vector3(-16f, 2f, 12f), new Vector3(2f, 4f, 9f), block);
        SpearKit.Box("Low Slab", level, new Vector3(-6f, 0.3f, 8f), new Vector3(4f, 0.6f, 2.5f), blockDark);
        // shallow pool: a grey frame around a pale blue inset
        SpearKit.Box("Pool Rim", level, new Vector3(15f, 0.15f, 13f), new Vector3(9f, 0.3f, 6f), block);
        SpearKit.Box("Pool Water", level, new Vector3(15f, 0.16f, 13f), new Vector3(8.2f, 0.3f, 5.2f), SpearKit.Mat(new Color(0.60f, 0.75f, 0.88f), 0.85f), false);

        boardLeft = BuildBoard("Board Left", new Vector3(-5f, 0, 50f), "TEST LOG");
        boardRight = BuildBoard("Board Right", new Vector3(6f, 0, 50f), "SESSION");

        // spears left in the map from earlier throws
        StickDecor(new Vector3(3.2f, 1.4f, 14.2f), new Vector3(0.5f, -0.45f, 1f));
        StickDecor(new Vector3(-7.5f, 0f, 33f), new Vector3(-0.3f, -0.5f, 1f));
        StickDecor(new Vector3(10.5f, 0f, 27f), new Vector3(0.6f, -0.35f, 1f));
        StickDecor(new Vector3(-2.5f, 0f, 44f), new Vector3(0.2f, -0.6f, 1f));

        puffPrefab = MakeBurst("HitPuff", new Color(0.62f, 0.04f, 0.06f), 26, 0.035f, 0.08f, 1.6f, 4.5f);
        dustPrefab = MakeBurst("Dust", new Color(0.72f, 0.74f, 0.78f), 16, 0.04f, 0.09f, 0.8f, 2.2f);
        dynamicRoot = new GameObject("Round").transform;
    }

    void StickDecor(Vector3 tipAt, Vector3 dir)
    {
        var s = Spear.Build(level);
        s.name = "Old Spear";
        dir.Normalize();
        s.transform.rotation = Quaternion.LookRotation(dir);
        s.transform.position = tipAt + dir * 0.3f - dir * Spear.TipOffset;
        s.enabled = false;
    }

    TextMeshPro BuildBoard(string name, Vector3 at, string title)
    {
        var root = new GameObject(name).transform;
        root.SetParent(level);
        root.position = at;
        root.rotation = Quaternion.LookRotation(new Vector3(at.x * 0.3f, 0, at.z).normalized);
        var post = SpearKit.Mat(new Color(0.55f, 0.57f, 0.62f), 0.3f, 0.4f);
        var panel = SpearKit.Mat(new Color(0.05f, 0.06f, 0.07f), 0.3f);
        SpearKit.Prim(PrimitiveType.Cylinder, "PostL", root, new Vector3(-2.2f, 2.1f, 0.05f), new Vector3(0.18f, 2.1f, 0.18f), post, true);
        SpearKit.Prim(PrimitiveType.Cylinder, "PostR", root, new Vector3(2.2f, 2.1f, 0.05f), new Vector3(0.18f, 2.1f, 0.18f), post, true);
        SpearKit.Prim(PrimitiveType.Cube, "Panel", root, new Vector3(0, 2.6f, 0), new Vector3(4.2f, 3f, 0.12f), panel, true);
        var titleT = MakeWorldText(root, "Title", new Vector3(-1.85f, 3.85f, -0.07f), new Vector2(3.7f, 0.5f), title, 3.6f, SpearKit.Heavy, Color.white);
        titleT.alignment = TextAlignmentOptions.TopLeft;
        var body = MakeWorldText(root, "Rows", new Vector3(-1.85f, 3.2f, -0.07f), new Vector2(3.7f, 2.0f), "", 2.4f, SpearKit.Bold, new Color(0.88f, 0.90f, 0.93f));
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 6f;
        return body;
    }

    TextMeshPro MakeWorldText(Transform parent, string name, Vector3 localPos, Vector2 size, string s, float fontSize, TMP_FontAsset font, Color c)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshPro>();
        var r = g.GetComponent<RectTransform>();
        r.pivot = new Vector2(0, 1);
        r.sizeDelta = size;
        r.localPosition = localPos;
        t.font = font;
        t.text = s;
        t.fontSize = fontSize;
        t.color = c;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        return t;
    }

    ParticleSystem MakeBurst(string name, Color c, int count, float sizeMin, float sizeMax, float gravity, float speed)
    {
        var g = new GameObject(name);
        g.SetActive(false);
        g.transform.SetParent(level);
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = c;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 35f;
        sh.radius = 0.03f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                     new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        r.sharedMaterial = SpearKit.Mat(c, 0.3f);
        return ps;
    }

    void Burst(ParticleSystem prefab, Vector3 at, Vector3 normal)
    {
        var ps = Instantiate(prefab, at, Quaternion.LookRotation(normal), dynamicRoot);
        ps.gameObject.SetActive(true);
        ps.Play();
        Destroy(ps.gameObject, 30f);
    }

    // ---------------- rounds ----------------

    public void NewRound()
    {
        StopAllCoroutines();
        resolving = false;
        if (dynamicRoot != null) Destroy(dynamicRoot.gameObject);
        dynamicRoot = new GameObject("Round").transform;
        dummies.Clear();
        for (int i = 0; i < spots.Length; i++)
            dummies.Add(Dummy.Build(i, spots[i].a, spots[i].b, dynamicRoot));
        if (player.inFlight != null) Destroy(player.inFlight.gameObject);
        player.inFlight = null;
        round++;
        score = 0; throws = 0; hits = 0; heads = 0;
        spearsLeft = SpearsPerRound;
        busy = false; roundOver = false;
        lastResult = "";
        slow = slowTarget = 1f;
        ApplySlow();
        player.transform.position = Vector3.zero;
        player.yaw = 0f; player.pitch = 0f;
        player.mode = SpearThrower.Mode.Aim;
        player.cam.fieldOfView = player.baseFov;
        player.charging = false; player.charge = 0f;
        player.ApplyLook();
        player.GiveSpear();
        player.canThrow = true;
        hud.overlayTarget = 0f;
        hud.HideHit();
        hud.HideMessage();
        log.Clear();
        UpdateBoards();
    }

    void OnThrow(Spear s)
    {
        spearsLeft--;
        throws++;
        busy = true;
        tracked = s;
        s.onHit = OnSpearHit;
        slowTarget = 0.25f;
        hud.HideHit();
        StartCoroutine(WatchFlight(s));
    }

    IEnumerator WatchFlight(Spear s)
    {
        float game = 0f;
        while (s != null && s.flying && game < 6f) { game += Time.deltaTime; yield return null; }
        if (s == null || (!s.stuck && !resolving))
        {
            // flew off the map
            if (s != null) Destroy(s.gameObject);
            lastResult = "miss";
            yield return Resolve(false, 0.4f);
        }
    }

    void OnSpearHit(Spear s, RaycastHit hit)
    {
        var dummy = hit.collider.GetComponentInParent<Dummy>();
        var bone = dummy != null ? dummy.BoneOf(hit.collider) : null;
        player.inFlight = null;
        if (dummy != null && bone != null)
        {
            bool fresh = !dummy.down;
            var local = bone.t.InverseTransformPoint(hit.point);
            string part = PartName(bone, local);
            int pts = fresh ? Points(bone.part) : 0;
            dummy.Ragdoll(bone, hit.point, s.velocity.normalized * 48f);
            Burst(puffPrefab, hit.point, -s.velocity.normalized + Vector3.up * 0.3f);
            if (fresh) { hits++; if (bone.part == Dummy.Part.Head) heads++; }
            score += pts;
            lastResult = part + (pts > 0 ? " +" + pts : "");
            log.Insert(0, $"DUMMY {dummy.index + 1:00}   {part.Split(' ')[0],-8} {(pts > 0 ? "+" + pts : "--")}");
            if (log.Count > 6) log.RemoveAt(log.Count - 1);
            hud.ShowHit(bone.t, bone.center + Vector3.up * bone.size.y * 0.5f, part, pts);
            hud.overlayTarget = 0.68f;
            player.followTarget = bone.t;
            slow = Mathf.Min(slow, 0.05f);
            slowTarget = 0.05f;
            ApplySlow();
            UpdateBoards();
            StartCoroutine(Resolve(true, 0f));
        }
        else
        {
            Burst(dustPrefab, hit.point, hit.normal);
            player.followPoint = hit.point;
            player.hasFollowPoint = true;
            lastResult = "miss";
            StartCoroutine(Resolve(false, 0.5f));
        }
    }

    IEnumerator WaitReal(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += SpearKit.RealDt; yield return null; }
    }

    IEnumerator Resolve(bool hit, float settle)
    {
        resolving = true;
        if (hit)
        {
            yield return WaitReal(1.1f);
            slowTarget = 0.3f;
            yield return WaitReal(1.5f);
            slowTarget = 1f;
            hud.overlayTarget = 0f;
            hud.HideHit();
            yield return WaitReal(0.5f);
        }
        else
        {
            slowTarget = 0.3f;
            yield return WaitReal(settle);
            slowTarget = 1f;
            yield return WaitReal(0.4f);
        }
        hud.overlayTarget = 0f;
        player.BeginReturn();
        int down = DownCount;
        if (down == dummies.Count || spearsLeft <= 0)
        {
            roundOver = true;
            best = Mathf.Max(best, score);
            UpdateBoards();
            if (down == dummies.Count) hud.ShowMessage("ALL " + dummies.Count + " DOWN", "SCORE " + score + "  ·  " + heads + " HEADSHOTS");
            else hud.ShowMessage("OUT OF SPEARS", "SCORE " + score + "  ·  " + down + "/" + dummies.Count + " DOWN");
            busy = false;
            resolving = false;
            yield return WaitReal(3f);
            NewRound();
            yield break;
        }
        player.GiveSpear();
        while (player.mode != SpearThrower.Mode.Aim) yield return null;
        busy = false;
        resolving = false;
    }

    public int DownCount { get { int n = 0; foreach (var d in dummies) if (d.down) n++; return n; } }

    static int Points(Dummy.Part p)
    {
        switch (p)
        {
            case Dummy.Part.Head: return 100;
            case Dummy.Part.Chest: return 50;
            case Dummy.Part.Pelvis: return 40;
            default: return 25;
        }
    }

    static string PartName(Dummy.Bone b, Vector3 local)
    {
        var p = local - b.center;
        switch (b.part)
        {
            case Dummy.Part.Head:
                if (p.y > 0.11f) return "HEAD — parietal bone";
                if (p.z > 0.1f) return p.y < -0.06f ? "HEAD — jaw (mandible)" : "HEAD — frontal bone";
                if (p.z < -0.1f) return "HEAD — occipital bone";
                return "HEAD — temporal bone";
            case Dummy.Part.Chest:
                if (p.z < -0.09f) return "BACK — spine (thoracic)";
                if (p.y > 0.14f) return "CHEST — collarbone";
                if (Mathf.Abs(p.x) > 0.12f) return "CHEST — rib cage";
                return "CHEST — sternum";
            case Dummy.Part.Pelvis: return "HIP — pelvis (ilium)";
            case Dummy.Part.UpperArm: return "ARM — humerus";
            case Dummy.Part.LowerArm: return "FOREARM — radius";
            case Dummy.Part.UpperLeg: return "THIGH — femur";
            default: return "SHIN — tibia";
        }
    }

    void UpdateBoards()
    {
        if (boardLeft == null) return;
        boardLeft.text = log.Count == 0 ? "no hits yet" : string.Join("\n", log);
        boardRight.text = $"ROUND      {round}\nSCORE      {score}\nBEST       {best}\nHEADSHOTS  {heads}\nSPEARS     {spearsLeft}/{SpearsPerRound}";
    }

    // ---------------- slow motion ----------------

    void ApplySlow()
    {
        Time.timeScale = slow;
        Time.fixedDeltaTime = 0.02f * slow;
    }

    void Update()
    {
        if (player.RestartPressed) { NewRound(); return; }
        float rdt = SpearKit.RealDt;

        if (tracked != null && tracked.flying)
        {
            // a deeper slow motion just before the spear lands
            float tti = tracked.TimeToImpact();
            slowTarget = tti < 0.07f ? 0.05f : 0.25f;
        }
        float rate = slowTarget < slow ? 9f : 2.2f;
        slow = Mathf.Exp(Mathf.Lerp(Mathf.Log(slow), Mathf.Log(slowTarget), 1f - Mathf.Exp(-rate * rdt)));
        if (Mathf.Abs(slow - slowTarget) < 0.002f) slow = slowTarget;
        ApplySlow();

        hud.SetStats(score, DownCount, dummies.Count, spearsLeft);
        hud.slowTarget = slow < 0.98f ? 1f : 0f;
        hud.SetSlow(slow);
        bool aiming = player.mode == SpearThrower.Mode.Aim && !busy && !roundOver;
        hud.hintTarget = aiming && !player.charging && slow > 0.98f ? 1f : 0f;
        hud.powerTarget = player.charging ? 1f : 0f;
        touch.hidden = player.botDriven;
        player.ThrowButton.visible = aiming && player.held != null && player.canThrow;
        hud.SetPower(player.charge);
    }

    // ---------------- bot ----------------

    // Solves the launch velocity (low arc, fixed speed) that meets a moving target; two passes of lead.
    // Angle from the projectile equation tan(a) = (v^2 - sqrt(v^4 - g(g x^2 + 2 y v^2))) / (g x),
    // after mnijaki/AutoAimBowAndArrow (MIT).
    public static bool Solve(Vector3 origin, System.Func<float, Vector3> targetAt, float speed, out Vector3 vel, out float time)
    {
        float g = -Physics.gravity.y;
        float t = 0f;
        vel = Vector3.zero; time = 0f;
        for (int pass = 0; pass < 4; pass++)
        {
            var target = targetAt(t);
            var d = target - origin;
            var flat = new Vector3(d.x, 0, d.z);
            float x = flat.magnitude, y = d.y, v2 = speed * speed;
            float disc = v2 * v2 - g * (g * x * x + 2 * y * v2);
            if (disc < 0 || x < 0.01f) return false;
            float ang = Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (g * x));
            vel = flat.normalized * Mathf.Cos(ang) * speed + Vector3.up * Mathf.Sin(ang) * speed;
            t = x / (speed * Mathf.Cos(ang));
        }
        time = t;
        return true;
    }

    public Dummy PickStanding(int preferred)
    {
        if (preferred >= 0 && preferred < dummies.Count && !dummies[preferred].down) return dummies[preferred];
        foreach (var d in dummies) if (!d.down) return d;
        return null;
    }

    // One bot throw: turn toward the target, pull back, release with the solved velocity, wait for the result.
    public IEnumerator BotThrow(int index, bool head, float miss = 0f)
    {
        player.botDriven = true;
        while (busy || roundOver || player.held == null || player.mode != SpearThrower.Mode.Aim) yield return null;
        var d = PickStanding(index);
        if (d == null) yield break;
        float speed = SpearThrower.MaxSpeed;
        var offset = (head ? d.HeadPoint : d.ChestPoint) - d.transform.position + Vector3.up * miss;
        System.Func<float, Vector3> targetAt = t => d.PredictFeet(t) + offset;

        float startYaw = player.yaw, startPitch = player.pitch;
        float turn = 0f, chargeT = 0f;
        Vector3 vel = Vector3.forward;
        while (true)
        {
            float rdt = SpearKit.RealDt;
            var origin = player.LaunchOrigin(player.AimRotation);
            if (Solve(origin, targetAt, speed, out vel, out _))
            {
                var e = Quaternion.LookRotation(vel).eulerAngles;
                float wantYaw = e.y, wantPitch = Mathf.DeltaAngle(0, e.x);
                turn = Mathf.Min(1f, turn + rdt / 0.6f);
                float k = Mathf.SmoothStep(0, 1, turn);
                player.yaw = Mathf.LerpAngle(startYaw, wantYaw, k);
                player.pitch = Mathf.Lerp(startPitch, wantPitch, k);
            }
            if (turn >= 0.35f)
            {
                player.charging = true;
                chargeT += rdt;
                player.charge = Mathf.Min(1f, chargeT / 0.6f);
            }
            if (turn >= 1f && player.charge >= 1f && !d.NearTurn(0.9f)) break;
            yield return null;
        }
        player.ApplyLook();
        var o = player.LaunchOrigin(player.AimRotation);
        Solve(o, targetAt, speed, out vel, out _);
        player.Release(vel);
        yield return null;
        while (busy) yield return null;
    }

    public string State()
    {
        return $"round {round} score {score} best {best} spears {spearsLeft} down {DownCount}/{dummies.Count} throws {throws} hits {hits} heads {heads} last '{lastResult}' mode {player.mode} timeScale {Time.timeScale:0.000} busy {busy}";
    }
}
