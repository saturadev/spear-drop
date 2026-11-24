using UnityEngine;
using UnityEngine.InputSystem;
using GameTouch;

// First-person thrower: mouse/stick look, WASD walk, hold to raise and pull the spear back, release to throw.
// After the throw the camera turns to follow the spear and narrows its view as it flies away.
public class SpearThrower : MonoBehaviour
{
    public enum Mode { Aim, Follow, Return }

    public Camera cam;
    public Transform camT;
    public Mode mode = Mode.Aim;
    public float yaw, pitch;
    public float charge;
    public bool charging;
    public bool botDriven;
    public float baseFov = 68f;
    public Spear held, inFlight;
    public Transform followTarget;      // set by the game after a hit (the struck bone)
    public Vector3 followPoint;
    public bool hasFollowPoint;
    public System.Action<Spear> onThrow;
    public bool canThrow = true;
    public TouchPad.Button ThrowButton;   // on-screen hold-to-throw for phones

    public const float MinSpeed = 16f, MaxSpeed = 34f;
    public static float SpeedFor(float c) => Mathf.Lerp(MinSpeed, MaxSpeed, c);
    public const int SpearMask = ~(1 << 2);   // everything but Ignore Raycast (the player)

    CharacterController cc;
    Transform handR, handL, armR, armL, rigR;
    float hide = 0f, hideTarget = 0f;
    float thrust = -1f;   // seconds since release while the arm swings through
    Vector3 thrustFrom;
    float vy;
    InputAction move, look, fire, padLook, restart;

    static Material skinMat;

    public static SpearThrower Build(Vector3 at, float startYaw)
    {
        var g = new GameObject("Player");
        g.layer = 2;
        g.transform.position = at;
        var p = g.AddComponent<SpearThrower>();
        p.cc = g.AddComponent<CharacterController>();
        p.cc.height = 1.8f; p.cc.radius = 0.3f; p.cc.center = new Vector3(0, 0.9f, 0);
        var cg = new GameObject("Main Camera");
        cg.tag = "MainCamera";
        cg.transform.SetParent(g.transform, false);
        cg.transform.localPosition = new Vector3(0, 1.62f, 0);
        p.cam = cg.AddComponent<Camera>();
        p.cam.nearClipPlane = 0.03f;
        p.cam.farClipPlane = 600f;
        p.cam.fieldOfView = p.baseFov;
        cg.AddComponent<AudioListener>();
        p.camT = cg.transform;
        p.yaw = startYaw;
        p.BuildHands();
        p.ApplyLook();
        return p;
    }

    void BuildHands()
    {
        if (skinMat == null) skinMat = SpearKit.Mat(new Color(0.93f, 0.79f, 0.67f), 0.3f);
        rigR = new GameObject("HandR").transform;
        rigR.SetParent(camT, false);
        handR = rigR;
        SpearKit.Prim(PrimitiveType.Cube, "palm", rigR, new Vector3(0.02f, -0.02f, 0f), new Vector3(0.085f, 0.1f, 0.12f), skinMat);
        SpearKit.Prim(PrimitiveType.Cube, "fingers", rigR, new Vector3(-0.035f, 0.0f, 0.005f), new Vector3(0.05f, 0.11f, 0.11f), skinMat);
        SpearKit.Prim(PrimitiveType.Cube, "thumb", rigR, new Vector3(-0.02f, 0.05f, -0.02f), new Vector3(0.04f, 0.035f, 0.07f), skinMat);
        armR = SpearKit.Prim(PrimitiveType.Capsule, "forearmR", camT, Vector3.zero, Vector3.one, skinMat).transform;

        handL = new GameObject("HandL").transform;
        handL.SetParent(camT, false);
        handL.localPosition = new Vector3(-0.30f, -0.33f, 0.48f);
        handL.localRotation = Quaternion.Euler(-35f, 25f, -20f);
        SpearKit.Prim(PrimitiveType.Cube, "palm", handL, Vector3.zero, new Vector3(0.1f, 0.035f, 0.11f), skinMat);
        for (int i = 0; i < 4; i++)
            SpearKit.Prim(PrimitiveType.Cube, "finger", handL, new Vector3(-0.036f + i * 0.024f, 0.008f, 0.085f), new Vector3(0.02f, 0.022f, 0.07f), skinMat, false, Quaternion.Euler(-20f, 0, 0));
        SpearKit.Prim(PrimitiveType.Cube, "thumb", handL, new Vector3(0.065f, 0.01f, 0.02f), new Vector3(0.022f, 0.022f, 0.06f), skinMat, false, Quaternion.Euler(0, 40f, 0));
        armL = SpearKit.Prim(PrimitiveType.Capsule, "forearmL", camT, Vector3.zero, Vector3.one, skinMat).transform;
    }

    void OnEnable()
    {
        move = new InputAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        move.AddBinding("<Gamepad>/leftStick");
        look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        padLook = new InputAction("PadLook", InputActionType.Value, "<Gamepad>/rightStick");
        fire = new InputAction("Throw", InputActionType.Button, "<Mouse>/leftButton");
        fire.AddBinding("<Keyboard>/space");
        fire.AddBinding("<Gamepad>/rightTrigger");
        // touch goes through TouchPad (stick, look drag and the THROW button), so a finger that walks or looks never throws
        restart = new InputAction("Restart", InputActionType.Button, "<Keyboard>/r");
        move.Enable(); look.Enable(); padLook.Enable(); fire.Enable(); restart.Enable();
    }

    void OnDisable()
    {
        move?.Disable(); look?.Disable(); padLook?.Disable(); fire?.Disable(); restart?.Disable();
    }

    public bool RestartPressed => restart != null && restart.WasPressedThisFrame();

    public void GiveSpear()
    {
        if (held != null) return;
        held = Spear.Build(handR);
        held.transform.localPosition = new Vector3(0, 0, 0.45f);
        held.transform.localRotation = Quaternion.identity;
        hideTarget = 0f;
    }

    public void ApplyLook()
    {
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        camT.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    public Quaternion AimRotation => Quaternion.Euler(pitch, yaw, 0);

    // Where the spear's centre leaves the hand for a given view rotation.
    public Vector3 LaunchOrigin(Quaternion view) => camT.position + view * new Vector3(0.2f, -0.08f, 0.25f);

    void Update()
    {
        float rdt = SpearKit.RealDt;
        if (mode == Mode.Aim && !botDriven)
        {
            bool recentTouch = TouchPad.RecentTouch; // ignore mouse events a browser emulates from taps
            var m = recentTouch ? null : Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState == CursorLockMode.Locked && !recentTouch)
            {
                var d = look.ReadValue<Vector2>() * 0.09f;
                yaw += d.x; pitch -= d.y;
            }
            var pd = padLook.ReadValue<Vector2>() * 140f * rdt;
            yaw += pd.x; pitch -= pd.y;
            var tp = TouchPad.I;
            if (tp != null && tp.Dragging)
            {
                // a drag across the screen width turns about 100 degrees
                var td = tp.DragDelta / Mathf.Max(1f, Screen.width) * 100f * (cam.fieldOfView / baseFov);
                yaw += td.x; pitch -= td.y;
            }
            pitch = Mathf.Clamp(pitch, -70f, 70f);
            ApplyLook();

            var mv = move.ReadValue<Vector2>();
            if (tp != null && tp.StickHeld) mv = Vector2.ClampMagnitude(mv + tp.Stick, 1f);
            var wish = (transform.right * mv.x + transform.forward * mv.y) * 4.5f;
            vy = cc.isGrounded ? -1f : vy + Physics.gravity.y * Time.deltaTime;
            cc.Move((wish + Vector3.up * vy) * Time.deltaTime);

            if (held != null && canThrow)
            {
                bool touchFire = ThrowButton != null && (ThrowButton.held || ThrowButton.pressed);
                if ((fire.IsPressed() && !recentTouch) || touchFire) { charging = true; charge = Mathf.Min(1f, charge + rdt / 0.7f); }
                else if (charging) Release(null);
            }
        }
        else if (mode == Mode.Aim && botDriven)
        {
            ApplyLook();
        }

        UpdateCamera(rdt);
        UpdateHands(rdt);
    }

    // Throws the held spear. exactVelocity is for the bot; players throw along the view.
    public void Release(Vector3? exactVelocity)
    {
        if (held == null) return;
        var view = AimRotation;
        var v = exactVelocity ?? view * Vector3.forward * SpeedFor(charge);
        var s = held;
        held = null;
        thrustFrom = handR.localPosition;
        thrust = 0f;
        s.Launch(LaunchOrigin(view), v, SpearMask);
        inFlight = s;
        charging = false;
        charge = 0f;
        mode = Mode.Follow;
        hasFollowPoint = false;
        followTarget = null;
        onThrow?.Invoke(s);
    }

    public void BeginReturn() { mode = Mode.Return; followTarget = null; hasFollowPoint = false; }

    void UpdateCamera(float rdt)
    {
        if (mode == Mode.Follow)
        {
            Vector3 target;
            if (followTarget != null) target = followTarget.position;
            else if (hasFollowPoint) target = followPoint;
            else if (inFlight != null) target = inFlight.transform.position + inFlight.velocity * 0.12f;
            else target = camT.position + camT.forward;
            var to = target - camT.position;
            if (to.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(to);
                camT.rotation = Quaternion.Slerp(camT.rotation, want, 1f - Mathf.Exp(-5f * rdt));
            }
            float fov = Mathf.Lerp(baseFov, 20f, Mathf.InverseLerp(4f, 24f, to.magnitude));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-3f * rdt));
        }
        else if (mode == Mode.Return)
        {
            var want = AimRotation;
            camT.rotation = Quaternion.Slerp(camT.rotation, want, 1f - Mathf.Exp(-7f * rdt));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov, 1f - Mathf.Exp(-7f * rdt));
            if (Quaternion.Angle(camT.rotation, want) < 0.5f && Mathf.Abs(cam.fieldOfView - baseFov) < 0.3f)
            {
                cam.fieldOfView = baseFov;
                ApplyLook();
                mode = Mode.Aim;
            }
        }
    }

    void UpdateHands(float rdt)
    {
        hideTarget = (mode == Mode.Aim && held != null) ? 0f : 1f;
        float c = charging ? Mathf.SmoothStep(0, 1, charge) : 0f;
        var idle = new Vector3(0.24f, -0.25f, 0.42f);
        var back = new Vector3(0.30f, -0.05f, 0.02f);
        var pos = Vector3.Lerp(idle, back, c);
        float bob = Mathf.Sin(Time.unscaledTime * 1.6f) * 0.006f;
        pos.y += bob;

        if (thrust >= 0f)
        {
            thrust += rdt;
            float k = Mathf.Clamp01(thrust / 0.18f);
            pos = Vector3.Lerp(thrustFrom, new Vector3(0.08f, -0.2f, 0.78f), Mathf.Sin(k * Mathf.PI * 0.5f));
            if (thrust > 0.25f) thrust = -1f;
            hide = Mathf.Max(0f, (thrust - 0.12f) * 4f);
        }
        else
        {
            hide = Mathf.MoveTowards(hide, hideTarget, rdt * (hideTarget > hide ? 3f : 2.2f));
        }
        float h = Mathf.SmoothStep(0, 1, hide);
        pos += new Vector3(0, -0.75f, -0.1f) * h;
        handR.localPosition = pos;
        // the spear points at a spot a little above the crosshair, far away
        var aimLocal = new Vector3(0, 0.9f + c * 0.6f, 14f);
        handR.localRotation = Quaternion.LookRotation(aimLocal - pos, Vector3.up) * Quaternion.Euler(0, 0, -8f);

        var lp = new Vector3(-0.30f, -0.33f, 0.48f) + new Vector3(0, -0.75f, -0.1f) * h + new Vector3(0, bob, 0);
        lp += new Vector3(0.04f, 0.03f, 0.04f) * c;
        handL.localPosition = lp;

        SpanLocal(armR, handR.localPosition + handR.localRotation * new Vector3(0.02f, -0.03f, -0.05f), handR.localPosition + new Vector3(0.28f, -0.5f, -0.35f), 0.085f);
        SpanLocal(armL, handL.localPosition + handL.localRotation * new Vector3(0, -0.01f, -0.06f), handL.localPosition + new Vector3(-0.2f, -0.45f, -0.35f), 0.08f);
    }

    static void SpanLocal(Transform t, Vector3 a, Vector3 b, float thick)
    {
        var d = b - a;
        t.localPosition = (a + b) * 0.5f;
        t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        t.localScale = new Vector3(thick, d.magnitude * 0.5f + thick * 0.5f, thick);
    }
}
