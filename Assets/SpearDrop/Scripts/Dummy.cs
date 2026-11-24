using System.Collections.Generic;
using UnityEngine;

// A crash-test style mannequin built from boxes. It stands or paces on its ledge as a kinematic
// ragdoll, and goes limp the moment a spear lands (every bone becomes a dynamic rigidbody).
public class Dummy : MonoBehaviour
{
    public enum Part { Head, Chest, Pelvis, UpperArm, LowerArm, UpperLeg, LowerLeg }

    public class Bone
    {
        public Transform t;
        public Rigidbody rb;
        public Part part;
        public Vector3 center, size;
    }

    public int index;
    public bool down;
    public Vector3 pointA, pointB;     // patrol ends (feet), equal when standing still
    public float walkSpeed = 0.9f;
    public readonly List<Bone> bones = new List<Bone>();
    public Bone head, chest, pelvis, armUL, armUR, armLL, armLR, legUL, legUR, legLL, legLR;

    float phase, pause, dir = 1f;
    Vector3 velocity;

    public Vector3 Velocity => down ? Vector3.zero : velocity;
    public Vector3 HeadPoint => head.t.TransformPoint(head.center);
    public Vector3 ChestPoint => chest.t.TransformPoint(chest.center);

    static Material skin, vest, belt, eye, mark;

    public static Dummy Build(int index, Vector3 a, Vector3 b, Transform parent)
    {
        if (skin == null)
        {
            skin = SpearKit.Mat(new Color(0.86f, 0.87f, 0.89f), 0.25f);
            vest = SpearKit.Mat(new Color(0.13f, 0.15f, 0.17f), 0.2f);
            belt = SpearKit.Mat(new Color(0.95f, 0.72f, 0.12f), 0.3f);
            eye = SpearKit.Mat(new Color(0.08f, 0.08f, 0.09f), 0.5f);
            mark = SpearKit.Mat(new Color(0.08f, 0.08f, 0.09f), 0.3f);
        }
        var go = new GameObject("Dummy " + (index + 1));
        go.transform.SetParent(parent, false);
        go.transform.position = a;
        var d = go.AddComponent<Dummy>();
        d.index = index;
        d.pointA = a; d.pointB = b;
        d.phase = index * 1.7f;
        d.pause = 0.5f + index * 0.4f;
        var face = (b - a); face.y = 0;
        // pacing dummies face along their path; standing ones face the player at the origin
        go.transform.rotation = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : Quaternion.LookRotation(new Vector3(-a.x, 0, -a.z).normalized);
        d.BuildBody();
        return d;
    }

    Bone AddBone(string name, Transform parent, Vector3 pivotLocal, Vector3 center, Vector3 size, Part part, float mass, Material m)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pivotLocal;
        var col = g.AddComponent<BoxCollider>();
        col.center = center; col.size = size;
        var rb = g.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.isKinematic = true;
        rb.solverIterations = 12;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.4f;
        SpearKit.Prim(PrimitiveType.Cube, "mesh", g.transform, center, size, m);
        var bone = new Bone { t = g.transform, rb = rb, part = part, center = center, size = size };
        bones.Add(bone);
        return bone;
    }

    void Joint(Bone child, Bone parent, float twistLo, float twistHi, float swing1, float swing2)
    {
        var j = child.t.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = parent.rb;
        j.anchor = Vector3.zero;
        j.axis = Vector3.right;
        j.swingAxis = Vector3.forward;
        j.lowTwistLimit = new SoftJointLimit { limit = twistLo };
        j.highTwistLimit = new SoftJointLimit { limit = twistHi };
        j.swing1Limit = new SoftJointLimit { limit = swing1 };
        j.swing2Limit = new SoftJointLimit { limit = swing2 };
        j.enableProjection = true;
        j.enablePreprocessing = false;
    }

    void BuildBody()
    {
        var root = transform;
        pelvis = AddBone("Pelvis", root, new Vector3(0, 1.0f, 0), Vector3.zero, new Vector3(0.36f, 0.22f, 0.22f), Part.Pelvis, 10f, vest);
        chest = AddBone("Chest", pelvis.t, new Vector3(0, 0.12f, 0), new Vector3(0, 0.25f, 0), new Vector3(0.44f, 0.5f, 0.26f), Part.Chest, 18f, vest);
        head = AddBone("Head", chest.t, new Vector3(0, 0.52f, 0), new Vector3(0, 0.18f, 0), new Vector3(0.3f, 0.32f, 0.3f), Part.Head, 5f, skin);
        armUL = AddBone("UpperArm.L", chest.t, new Vector3(-0.30f, 0.48f, 0), new Vector3(0, -0.17f, 0), new Vector3(0.13f, 0.34f, 0.13f), Part.UpperArm, 2.5f, skin);
        armUR = AddBone("UpperArm.R", chest.t, new Vector3(0.30f, 0.48f, 0), new Vector3(0, -0.17f, 0), new Vector3(0.13f, 0.34f, 0.13f), Part.UpperArm, 2.5f, skin);
        armLL = AddBone("LowerArm.L", armUL.t, new Vector3(0, -0.34f, 0), new Vector3(0, -0.17f, 0), new Vector3(0.12f, 0.34f, 0.12f), Part.LowerArm, 2f, skin);
        armLR = AddBone("LowerArm.R", armUR.t, new Vector3(0, -0.34f, 0), new Vector3(0, -0.17f, 0), new Vector3(0.12f, 0.34f, 0.12f), Part.LowerArm, 2f, skin);
        legUL = AddBone("UpperLeg.L", pelvis.t, new Vector3(-0.1f, -0.1f, 0), new Vector3(0, -0.22f, 0), new Vector3(0.16f, 0.44f, 0.17f), Part.UpperLeg, 7f, vest);
        legUR = AddBone("UpperLeg.R", pelvis.t, new Vector3(0.1f, -0.1f, 0), new Vector3(0, -0.22f, 0), new Vector3(0.16f, 0.44f, 0.17f), Part.UpperLeg, 7f, vest);
        legLL = AddBone("LowerLeg.L", legUL.t, new Vector3(0, -0.44f, 0), new Vector3(0, -0.22f, 0), new Vector3(0.15f, 0.44f, 0.16f), Part.LowerLeg, 4.5f, skin);
        legLR = AddBone("LowerLeg.R", legUR.t, new Vector3(0, -0.44f, 0), new Vector3(0, -0.22f, 0), new Vector3(0.15f, 0.44f, 0.16f), Part.LowerLeg, 4.5f, skin);

        Joint(chest, pelvis, -25, 30, 20, 15);
        Joint(head, chest, -35, 35, 30, 25);
        Joint(armUL, chest, -80, 80, 70, 40);
        Joint(armUR, chest, -80, 80, 70, 40);
        Joint(armLL, armUL, -120, 0, 8, 8);
        Joint(armLR, armUR, -120, 0, 8, 8);
        Joint(legUL, pelvis, -80, 30, 30, 15);
        Joint(legUR, pelvis, -80, 30, 30, 15);
        Joint(legLL, legUL, 0, 120, 6, 6);
        Joint(legLR, legUR, 0, 120, 6, 6);

        // Details: crash-test style yellow belt and shoulder stripes, eyes, side markers, feet, hands.
        SpearKit.Prim(PrimitiveType.Cube, "belt", pelvis.t, new Vector3(0, 0.07f, 0), new Vector3(0.37f, 0.06f, 0.23f), belt);
        SpearKit.Prim(PrimitiveType.Cube, "stripe", chest.t, new Vector3(0, 0.36f, 0), new Vector3(0.45f, 0.05f, 0.27f), belt);
        SpearKit.Prim(PrimitiveType.Cube, "eyeL", head.t, new Vector3(-0.065f, 0.2f, 0.151f), new Vector3(0.04f, 0.05f, 0.01f), eye);
        SpearKit.Prim(PrimitiveType.Cube, "eyeR", head.t, new Vector3(0.065f, 0.2f, 0.151f), new Vector3(0.04f, 0.05f, 0.01f), eye);
        SpearKit.Prim(PrimitiveType.Cube, "mouth", head.t, new Vector3(0, 0.1f, 0.151f), new Vector3(0.09f, 0.015f, 0.01f), eye);
        foreach (var sx in new[] { -1f, 1f })
        {
            // quartered target marker on each side of the head
            var p = new Vector3(sx * 0.151f, 0.19f, 0);
            SpearKit.Prim(PrimitiveType.Cube, "markA", head.t, p + new Vector3(0, 0.03f, 0.03f), new Vector3(0.01f, 0.06f, 0.06f), belt);
            SpearKit.Prim(PrimitiveType.Cube, "markB", head.t, p + new Vector3(0, -0.03f, -0.03f), new Vector3(0.01f, 0.06f, 0.06f), belt);
            SpearKit.Prim(PrimitiveType.Cube, "markC", head.t, p + new Vector3(0, 0.03f, -0.03f), new Vector3(0.01f, 0.06f, 0.06f), mark);
            SpearKit.Prim(PrimitiveType.Cube, "markD", head.t, p + new Vector3(0, -0.03f, 0.03f), new Vector3(0.01f, 0.06f, 0.06f), mark);
        }
        SpearKit.Prim(PrimitiveType.Cube, "foot", legLL.t, new Vector3(0, -0.42f, 0.05f), new Vector3(0.16f, 0.06f, 0.26f), vest);
        SpearKit.Prim(PrimitiveType.Cube, "foot", legLR.t, new Vector3(0, -0.42f, 0.05f), new Vector3(0.16f, 0.06f, 0.26f), vest);
        SpearKit.Prim(PrimitiveType.Cube, "hand", armLL.t, new Vector3(0, -0.38f, 0), new Vector3(0.1f, 0.1f, 0.12f), skin);
        SpearKit.Prim(PrimitiveType.Cube, "hand", armLR.t, new Vector3(0, -0.38f, 0), new Vector3(0.1f, 0.1f, 0.12f), skin);
    }

    void Update()
    {
        if (down) return;
        float dt = Time.deltaTime;
        bool walks = (pointB - pointA).sqrMagnitude > 0.05f;
        float swing = 0f;
        if (walks)
        {
            if (pause > 0) { pause -= dt; velocity = Vector3.zero; }
            else
            {
                var target = dir > 0 ? pointB : pointA;
                var to = target - transform.position;
                if (to.magnitude < 0.05f)
                {
                    dir = -dir; pause = 1.2f;
                    velocity = Vector3.zero;
                }
                else
                {
                    var step = to.normalized * walkSpeed;
                    velocity = step;
                    transform.position += Vector3.ClampMagnitude(step * dt, to.magnitude);
                    var look = Quaternion.LookRotation(new Vector3(to.x, 0, to.z));
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 240f * dt);
                    phase += dt * walkSpeed * 5.2f;
                    swing = 1f;
                }
            }
        }
        float idle = Time.time * 1.3f + index;
        float s = Mathf.Sin(phase) * 28f * swing;
        legUL.t.localRotation = Quaternion.Euler(s, 0, 0);
        legUR.t.localRotation = Quaternion.Euler(-s, 0, 0);
        legLL.t.localRotation = Quaternion.Euler(Mathf.Max(0, -Mathf.Cos(phase)) * 30f * swing, 0, 0);
        legLR.t.localRotation = Quaternion.Euler(Mathf.Max(0, Mathf.Cos(phase)) * 30f * swing, 0, 0);
        armUL.t.localRotation = Quaternion.Euler(-s * 0.8f + Mathf.Sin(idle) * 2f, 0, -4f);
        armUR.t.localRotation = Quaternion.Euler(s * 0.8f - Mathf.Sin(idle) * 2f, 0, 4f);
        armLL.t.localRotation = Quaternion.Euler(-12f, 0, 0);
        armLR.t.localRotation = Quaternion.Euler(-12f, 0, 0);
        chest.t.localRotation = Quaternion.Euler(Mathf.Sin(idle * 0.7f) * 1.5f, Mathf.Sin(phase) * 5f * swing, 0);
        head.t.localRotation = Quaternion.Euler(Mathf.Sin(idle * 0.5f) * 3f, Mathf.Sin(idle * 0.35f) * 12f, 0);
        pelvis.t.localPosition = new Vector3(0, 1.0f + Mathf.Abs(Mathf.Sin(phase)) * 0.03f * swing, 0);
    }

    bool Walks => (pointB - pointA).sqrMagnitude > 0.05f;

    // Where the feet will be after t seconds of game time, following the same patrol rules as Update.
    public Vector3 PredictFeet(float t)
    {
        var pos = transform.position;
        if (down || !Walks) return pos;
        float p = pause, dr = dir;
        const float step = 0.01f;
        for (float s = 0; s < t; s += step)
        {
            if (p > 0) { p -= step; continue; }
            var target = dr > 0 ? pointB : pointA;
            var to = target - pos;
            if (to.magnitude < 0.05f) { dr = -dr; p = 1.2f; continue; }
            pos += Vector3.ClampMagnitude(to.normalized * walkSpeed * step, to.magnitude);
        }
        return pos;
    }

    // True when the dummy will reach a patrol end within the next `seconds` (a turn the bot can't lead well).
    public bool NearTurn(float seconds)
    {
        if (down || !Walks || pause > 0) return false;
        var target = dir > 0 ? pointB : pointA;
        return (target - transform.position).magnitude / walkSpeed < seconds;
    }

    public Bone BoneOf(Collider c)
    {
        foreach (var b in bones) if (b.t == c.transform) return b;
        return null;
    }

    // Goes limp: every bone becomes dynamic, keeps the walking velocity, then takes the spear's impulse.
    public void Ragdoll(Bone hit, Vector3 point, Vector3 impulse)
    {
        if (!down)
        {
            down = true;
            foreach (var b in bones)
            {
                b.rb.isKinematic = false;
                b.rb.interpolation = RigidbodyInterpolation.Interpolate;
                b.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                b.rb.linearVelocity = velocity;
            }
        }
        hit.rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
    }
}

static class VecExt
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
