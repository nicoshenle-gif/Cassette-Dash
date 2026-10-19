using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Precision telescoping needle spikes mounted inside machined sockets on GuidePin_Left / GuidePin_Right.
/// Telescopes smoothly out of the cylinder top without ever poking out the bottom or floating off the ends.
/// Deals 50 HP damage on hit.
/// </summary>
public class CassettePinSpikeObstacle : MonoBehaviour
{
    public const int ObstacleVisualVersion = 3;

    [Header("Spike Motion (升降刺参数)")]
    [Tooltip("Maximum height the telescoping needles extend above the cylinder (world units).")]
    public float spikeHeight = 0.85f;
    public float retractedDuration = 1.15f;
    public float warningDuration = 0.35f;
    public float thrustUpDuration = 0.14f;
    public float holdExtendedDuration = 0.85f;
    public float retractDuration = 0.35f;
    [Tooltip("Cycle time offset in seconds so left and right pins alternate.")]
    public float phaseOffset = 0f;

    [Header("Damage (每次受伤扣50血)")]
    public float damage = 50f;
    public float knockbackSpeed = 2.8f;
    public float hitCooldown = 0.75f;

    [Header("References")]
    public Transform spikeRoot;
    public CassettePlayer player;
    public int builtVisualVersion;

    float _timer;
    bool _timerInitialized;
    float _cooldownTimer;
    float _extend01; // 0 = flush inside socket, 1 = fully telescoped upward
    float _pinRadius = 0.21f;
    float _pinSpanZ = 1.95f;
    Transform[] _telescopingNeedles;
    static Mesh _coneMesh;

    void Awake()
    {
        EnsureVisualSpikes();
        if (!player) player = FindFirstObjectByType<CassettePlayer>();
    }

    void Start()
    {
        if (!_timerInitialized)
        {
            _timer = phaseOffset;
            _timerInitialized = true;
        }
    }

    public void EnsureVisualSpikes()
    {
        Transform parent = transform.parent != null ? transform.parent : transform;
        string holderName = gameObject.name + "_Spikes";
        Transform existing = parent.Find(holderName);

        if (existing != null && builtVisualVersion == ObstacleVisualVersion)
        {
            spikeRoot = existing;
            BindNeedleTransforms();
            if (_telescopingNeedles != null && _telescopingNeedles.Length > 0)
                return;
        }

        if (existing != null)
        {
            existing.SetParent(null);
            existing.name = "_DestroyedSpikes";
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        // Extend the guide pin cylinder slightly in radius & Z length so it forms a solid full-width mechanical roller bar
        // that houses all spike sockets cleanly without any spikes floating outside the cylinder ends.
        _pinRadius = 0.21f;
        _pinSpanZ = 1.95f;
        transform.localScale = new Vector3(_pinRadius * 2f, _pinSpanZ * 0.5f, _pinRadius * 2f);

        var rootGo = new GameObject(holderName);
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.localPosition = transform.localPosition;
        rootGo.transform.localRotation = Quaternion.identity;
        spikeRoot = rootGo.transform;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var collarMat = new Material(shader) { name = "PinCollar_Mat", color = new Color(0.42f, 0.44f, 0.48f) };
        if (collarMat.HasProperty("_BaseColor")) collarMat.SetColor("_BaseColor", collarMat.color);

        var darkSlotMat = new Material(shader) { name = "PinSlot_Mat", color = new Color(0.14f, 0.14f, 0.16f) };
        if (darkSlotMat.HasProperty("_BaseColor")) darkSlotMat.SetColor("_BaseColor", darkSlotMat.color);

        var steelNeedleMat = new Material(shader) { name = "NeedleSteel_Mat", color = new Color(0.76f, 0.79f, 0.84f) };
        if (steelNeedleMat.HasProperty("_BaseColor")) steelNeedleMat.SetColor("_BaseColor", steelNeedleMat.color);

        var copperTipMat = new Material(shader) { name = "NeedleTip_Mat", color = new Color(0.92f, 0.28f, 0.16f) };
        if (copperTipMat.HasProperty("_BaseColor")) copperTipMat.SetColor("_BaseColor", copperTipMat.color);

        Mesh coneMesh = GetOrCreateSmoothConeMesh();

        int countZ = 5;
        float usableZ = _pinSpanZ - 0.32f;
        _telescopingNeedles = new Transform[countZ];

        for (int i = 0; i < countZ; i++)
        {
            float z = countZ > 1 ? Mathf.Lerp(-usableZ * 0.5f, usableZ * 0.5f, i / (float)(countZ - 1)) : 0f;

            // 1. Machined metallic collar ring wrapping smoothly around the guide pin cylinder at this spike's Z position
            var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collar.name = "CollarRing_" + i;
            RemoveColliderSafe(collar);
            collar.transform.SetParent(spikeRoot, false);
            collar.transform.localPosition = new Vector3(0f, 0f, z);
            collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float collarRad = _pinRadius * 1.12f;
            collar.transform.localScale = new Vector3(collarRad * 2f, 0.055f, collarRad * 2f);
            collar.GetComponent<Renderer>().sharedMaterial = collarMat;

            // 2. Dark recessed circular socket bevel on the top surface of the collar where the needle emerges
            var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socket.name = "RecessedSocket_" + i;
            RemoveColliderSafe(socket);
            socket.transform.SetParent(spikeRoot, false);
            socket.transform.localPosition = new Vector3(0f, _pinRadius * 0.82f, z);
            socket.transform.localScale = new Vector3(0.19f, 0.04f, 0.19f);
            socket.GetComponent<Renderer>().sharedMaterial = darkSlotMat;

            // 3. Telescoping needle pivot anchored INSIDE the socket (scales in Y so it never pokes out the bottom!)
            var needlePivot = new GameObject("NeedlePivot_" + i);
            needlePivot.transform.SetParent(spikeRoot, false);
            needlePivot.transform.localPosition = new Vector3(0f, _pinRadius * 0.65f, z);

            // Lower cylindrical telescoping sleeve (from Y = 0 to Y = 0.42 in normalized needle space)
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            RemoveColliderSafe(shaft);
            shaft.transform.SetParent(needlePivot.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0.21f, 0f);
            shaft.transform.localScale = new Vector3(0.135f, 0.21f, 0.135f);
            shaft.GetComponent<Renderer>().sharedMaterial = steelNeedleMat;

            // Upper smooth conical needle tip (from Y = 0.40 to Y = 1.0 in normalized needle space)
            var tipGo = new GameObject("ConeTip");
            tipGo.transform.SetParent(needlePivot.transform, false);
            tipGo.transform.localPosition = new Vector3(0f, 0.40f, 0f);
            tipGo.transform.localScale = new Vector3(0.145f, 0.60f, 0.145f);
            var mf = tipGo.AddComponent<MeshFilter>();
            mf.sharedMesh = coneMesh;
            var mr = tipGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = copperTipMat;

            _telescopingNeedles[i] = needlePivot.transform;
        }

        builtVisualVersion = ObstacleVisualVersion;
        SetSpikeExtension(0f);
    }

    static void RemoveColliderSafe(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }

    void BindNeedleTransforms()
    {
        if (!spikeRoot) return;
        var list = new List<Transform>();
        for (int i = 0; i < spikeRoot.childCount; i++)
        {
            Transform ch = spikeRoot.GetChild(i);
            if (ch.name.StartsWith("NeedlePivot_"))
                list.Add(ch);
        }
        _telescopingNeedles = list.ToArray();
    }

    void FixedUpdate()
    {
        if (!_timerInitialized)
        {
            _timer = phaseOffset;
            _timerInitialized = true;
        }

        if (!spikeRoot || _telescopingNeedles == null || _telescopingNeedles.Length == 0)
            EnsureVisualSpikes();
        if (!player) player = FindFirstObjectByType<CassettePlayer>();

        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.fixedDeltaTime;

        float total = retractedDuration + warningDuration + thrustUpDuration + holdExtendedDuration + retractDuration;
        _timer = (_timer + Time.fixedDeltaTime) % Mathf.Max(0.2f, total);
        float t = _timer;

        if (t < retractedDuration)
        {
            _extend01 = 0f;
        }
        else if (t < retractedDuration + warningDuration)
        {
            // Warning peek: needle tips peek slightly out of the sockets and vibrate
            float u = (t - retractedDuration) / Mathf.Max(0.01f, warningDuration);
            _extend01 = 0.16f + Mathf.Sin(u * Mathf.PI * 8f) * 0.04f;
        }
        else if (t < retractedDuration + warningDuration + thrustUpDuration)
        {
            float u = (t - retractedDuration - warningDuration) / Mathf.Max(0.01f, thrustUpDuration);
            _extend01 = Mathf.Lerp(0.16f, 1f, u * u);
        }
        else if (t < retractedDuration + warningDuration + thrustUpDuration + holdExtendedDuration)
        {
            _extend01 = 1f;
        }
        else
        {
            float u = (t - retractedDuration - warningDuration - thrustUpDuration - holdExtendedDuration) / Mathf.Max(0.01f, retractDuration);
            _extend01 = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, u));
        }

        SetSpikeExtension(_extend01);
        CheckPlayerHit();
    }

    void SetSpikeExtension(float ext01)
    {
        if (!spikeRoot) return;
        spikeRoot.localPosition = transform.localPosition;

        if (_telescopingNeedles == null || _telescopingNeedles.Length == 0)
            BindNeedleTransforms();

        // Telescope smoothly from a tiny flush tip (0.045 units) inside the socket up to full spikeHeight
        float currentLen = Mathf.Lerp(0.045f, spikeHeight, Mathf.Clamp01(ext01));
        if (_telescopingNeedles != null)
        {
            for (int i = 0; i < _telescopingNeedles.Length; i++)
            {
                if (_telescopingNeedles[i] != null)
                    _telescopingNeedles[i].localScale = new Vector3(1f, currentLen, 1f);
            }
        }
    }

    void CheckPlayerHit()
    {
        if (_extend01 < 0.30f || !player || player.isDead || _cooldownTimer > 0f)
            return;

        Vector3 pinWorld = transform.position;
        Vector3 pPos = player.transform.position;
        float pRad = player.CapsuleRadius;
        float pHalfH = player.CapsuleHalfHeight;

        float activeTipY = pinWorld.y + _pinRadius * 0.65f + spikeHeight * _extend01;
        float activeBaseY = pinWorld.y;

        float dx = Mathf.Abs(pPos.x - pinWorld.x);
        float dz = Mathf.Abs(pPos.z - pinWorld.z);
        bool xHit = dx < (0.12f + pRad);
        bool zHit = dz < (_pinSpanZ * 0.48f + pRad);
        bool yHit = (pPos.y - pHalfH) < (activeTipY - 0.03f) && (pPos.y + pHalfH) > activeBaseY;

        if (xHit && zHit && yHit)
        {
            _cooldownTimer = hitCooldown;
            player.TakeDamage(damage);

            if (player.Body != null && !player.isDead)
            {
                float dirX = pPos.x < pinWorld.x ? -1f : 1f;
                Vector3 v = player.Body.linearVelocity;
                v.x = dirX * knockbackSpeed;
                v.y = Mathf.Max(v.y, 2.4f);
                player.Body.linearVelocity = v;
            }
        }
    }

    /// <summary>
    /// Creates a smooth 24-sided circular cone mesh with base radius 0.5 at Y = 0 and apex at Y = 1.
    /// </summary>
    static Mesh GetOrCreateSmoothConeMesh()
    {
        if (_coneMesh != null) return _coneMesh;

        const int segs = 24;
        const float r = 0.5f;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        float slopeNy = r;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * Mathf.PI * 2f / segs;
            float a1 = ((i + 1) % segs) * Mathf.PI * 2f / segs;
            float am = (a0 + a1) * 0.5f;

            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            Vector3 apex = new Vector3(0f, 1f, 0f);

            Vector3 n0 = new Vector3(Mathf.Cos(a0), slopeNy, Mathf.Sin(a0)).normalized;
            Vector3 n1 = new Vector3(Mathf.Cos(a1), slopeNy, Mathf.Sin(a1)).normalized;
            Vector3 nm = new Vector3(Mathf.Cos(am), slopeNy, Mathf.Sin(am)).normalized;

            int b = verts.Count;
            verts.Add(p0); norms.Add(n0);
            verts.Add(apex); norms.Add(nm);
            verts.Add(p1); norms.Add(n1);

            tris.Add(b);
            tris.Add(b + 1);
            tris.Add(b + 2);
        }

        _coneMesh = new Mesh { name = "SmoothNeedleCone24" };
        _coneMesh.SetVertices(verts);
        _coneMesh.SetNormals(norms);
        _coneMesh.SetTriangles(tris, 0);
        _coneMesh.RecalculateBounds();
        return _coneMesh;
    }
}
