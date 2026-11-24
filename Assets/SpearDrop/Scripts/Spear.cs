using UnityEngine;

// A thrown spear. Ballistic flight integrated in Update (smooth at any time scale), always facing its
// velocity; the tip is swept with a raycast each frame so a thin, fast spear never tunnels. On impact it
// sticks: parented to whatever it hit, a dummy bone or the map.
public class Spear : MonoBehaviour
{
    public const float Length = 2.3f;
    public const float TipOffset = 1.15f;   // from the pivot (centre) to the tip

    public Vector3 velocity;
    public bool flying, stuck;
    public RaycastHit lastHit;
    public System.Action<Spear, RaycastHit> onHit;
    public int mask = ~0;

    static Material wood, wrap, steel;

    public Vector3 Tip => transform.position + transform.forward * TipOffset;

    public static Spear Build(Transform parent)
    {
        if (wood == null)
        {
            wood = SpearKit.Mat(new Color(0.36f, 0.24f, 0.15f), 0.35f);
            wrap = SpearKit.Mat(new Color(0.09f, 0.08f, 0.08f), 0.2f);
            steel = SpearKit.Mat(new Color(0.62f, 0.64f, 0.68f), 0.78f, 0.9f);
        }
        var g = new GameObject("Spear");
        g.transform.SetParent(parent, false);
        var s = g.AddComponent<Spear>();
        var t = g.transform;
        var rotX = Quaternion.Euler(90, 0, 0);
        // shaft along +z (cylinder is along y: rotate), the blade at +z
        SpearKit.Prim(PrimitiveType.Cylinder, "shaft", t, new Vector3(0, 0, -0.1f), new Vector3(0.045f, 1.0f, 0.045f), wood, false, rotX);
        SpearKit.Prim(PrimitiveType.Cylinder, "grip", t, new Vector3(0, 0, -0.45f), new Vector3(0.052f, 0.22f, 0.052f), wrap, false, rotX);
        SpearKit.Prim(PrimitiveType.Cylinder, "butt", t, new Vector3(0, 0, -1.1f), new Vector3(0.055f, 0.03f, 0.055f), steel, false, rotX);
        SpearKit.Prim(PrimitiveType.Cylinder, "socket", t, new Vector3(0, 0, 0.88f), new Vector3(0.06f, 0.07f, 0.06f), steel, false, rotX);
        // leaf blade: a cube turned 45 degrees inside a stretched parent gives a long diamond
        var blade = new GameObject("blade").transform;
        blade.SetParent(t, false);
        blade.localPosition = new Vector3(0, 0, 0.99f);
        blade.localScale = new Vector3(0.13f, 0.022f, 0.34f);
        SpearKit.Prim(PrimitiveType.Cube, "leaf", blade, Vector3.zero, new Vector3(0.7071f, 1f, 0.7071f), steel, false, Quaternion.Euler(0, 45, 0));
        var rib = new GameObject("rib").transform;
        rib.SetParent(t, false);
        rib.localPosition = new Vector3(0, 0, 0.99f);
        rib.localScale = new Vector3(0.03f, 0.05f, 0.33f);
        SpearKit.Prim(PrimitiveType.Cube, "ridge", rib, Vector3.zero, new Vector3(0.7071f, 1f, 0.7071f), steel, false, Quaternion.Euler(0, 45, 0));
        return s;
    }

    public void Launch(Vector3 from, Vector3 vel, int layerMask)
    {
        transform.SetParent(null, true);
        transform.position = from;
        transform.rotation = Quaternion.LookRotation(vel);
        velocity = vel;
        mask = layerMask;
        flying = true;
    }

    void Update()
    {
        if (!flying) return;
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        Physics.SyncTransforms();
        var tip0 = Tip;
        velocity += Physics.gravity * dt;
        var pos1 = transform.position + velocity * dt;
        var rot1 = Quaternion.LookRotation(velocity);
        var tip1 = pos1 + rot1 * Vector3.forward * TipOffset;
        var d = tip1 - tip0;
        if (Physics.Raycast(tip0, d.normalized, out var hit, d.magnitude + 0.02f, mask, QueryTriggerInteraction.Ignore))
        {
            flying = false; stuck = true;
            lastHit = hit;
            // bury the tip a little past the surface, along the flight line
            var dirN = velocity.normalized;
            transform.rotation = Quaternion.LookRotation(dirN);
            float bury = hit.rigidbody != null ? 0.32f : 0.28f;
            transform.position = hit.point + dirN * bury - dirN * TipOffset;
            // ride along with moving bodies; static map pieces are scaled boxes, so stay unparented there
            if (hit.rigidbody != null) transform.SetParent(hit.collider.transform, true);
            onHit?.Invoke(this, hit);
            return;
        }
        transform.SetPositionAndRotation(pos1, rot1);
        if (transform.position.y < -20f) { flying = false; Destroy(gameObject); }
    }

    // Time until the tip reaches something along its current line, or +inf.
    public float TimeToImpact(float lookAhead = 12f)
    {
        if (!flying) return float.PositiveInfinity;
        float speed = velocity.magnitude;
        if (Physics.SphereCast(Tip, 0.15f, velocity.normalized, out var hit, lookAhead, mask, QueryTriggerInteraction.Ignore))
            return hit.distance / Mathf.Max(0.01f, speed);
        return float.PositiveInfinity;
    }
}
