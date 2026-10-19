using UnityEngine;

/// <summary>
/// Cassette-themed overhead magnetic erase/record head & solenoid arm that extends downward
/// from the top ceiling and drops cassette-world mechanical hazards (loose Phillips screws,
/// miniature capstan gears, and copper ferrite inductor coils) that deal 50 HP damage.
/// </summary>
public class CassetteOverheadDropper : MonoBehaviour
{
    public const int DropperVisualVersion = 3;

    [Header("Extension & Drop Timing (上方延伸与投放参数)")]
    [Tooltip("How far the solenoid head arm extends downward from the ceiling (world units).")]
    public float extendDistance = 5.2f;
    public float retractedArmLength = 1.6f;
    public float idleRetractedDuration = 0.70f;
    public float extendDownDuration = 0.42f;
    public float aimChargeDuration = 0.32f;
    public float retractUpDuration = 0.48f;
    public float phaseOffset = 0f;

    [Header("Projectile Settings (投放磁带零件伤害参数)")]
    public float projectileDamage = 50f;
    public float projectileGravity = 8.2f;
    public float floorDestroyY = -7.35f;

    [Header("Visual Parts")]
    public Transform telescopingArm;
    public Transform nozzleTip;
    public Renderer warningRenderer;
    public CassettePlayer player;
    public int builtVersion = DropperVisualVersion;

    float _timer;
    bool _timerInitialized;
    bool _droppedThisCycle;
    int _dropCounter;
    Material _warningMat;

    void Awake()
    {
        if (!player) player = FindFirstObjectByType<CassettePlayer>();
        AutoBindPartsIfNeeded();
    }

    void Start()
    {
        if (!_timerInitialized)
        {
            _timer = phaseOffset;
            _timerInitialized = true;
        }
        AutoBindPartsIfNeeded();
    }

    public void AutoBindPartsIfNeeded()
    {
        if (telescopingArm == null)
            telescopingArm = transform.Find("TelescopingArm");
        if (nozzleTip == null)
            nozzleTip = transform.Find("NozzleTip");
        if (warningRenderer == null && nozzleTip != null)
        {
            Transform coil = nozzleTip.Find("CopperCoilBand");
            if (coil != null) warningRenderer = coil.GetComponent<Renderer>();
            if (warningRenderer == null) warningRenderer = nozzleTip.GetComponentInChildren<Renderer>();
        }
    }

    void FixedUpdate()
    {
        if (!_timerInitialized)
        {
            _timer = phaseOffset;
            _timerInitialized = true;
        }

        if (!player) player = FindFirstObjectByType<CassettePlayer>();
        if (telescopingArm == null || nozzleTip == null)
            AutoBindPartsIfNeeded();

        float total = idleRetractedDuration + extendDownDuration + aimChargeDuration + retractUpDuration;
        float prevTimer = _timer;
        _timer = (_timer + Time.fixedDeltaTime) % Mathf.Max(0.3f, total);
        if (_timer < prevTimer)
            _droppedThisCycle = false;

        float t = _timer;
        float ext01 = 0f;
        float glow01 = 0f;

        if (t < idleRetractedDuration)
        {
            ext01 = 0f;
            glow01 = 0f;
        }
        else if (t < idleRetractedDuration + extendDownDuration)
        {
            float u = (t - idleRetractedDuration) / Mathf.Max(0.01f, extendDownDuration);
            ext01 = Mathf.SmoothStep(0f, 1f, u);
            glow01 = u * 0.6f;
        }
        else if (t < idleRetractedDuration + extendDownDuration + aimChargeDuration)
        {
            ext01 = 1f;
            float u = (t - idleRetractedDuration - extendDownDuration) / Mathf.Max(0.01f, aimChargeDuration);
            glow01 = 0.6f + 0.4f * Mathf.Sin(u * Mathf.PI * 4f);

            if (!_droppedThisCycle && u >= 0.45f)
            {
                _droppedThisCycle = true;
                SpawnCassetteWorldHazard();
            }
        }
        else
        {
            float u = (t - idleRetractedDuration - extendDownDuration - aimChargeDuration) / Mathf.Max(0.01f, retractUpDuration);
            ext01 = Mathf.SmoothStep(1f, 0f, u);
            glow01 = Mathf.Lerp(0.5f, 0f, u);
        }

        ApplyExtension(ext01, glow01);
        CheckDirectArmHit();
    }

    void ApplyExtension(float ext01, float glow01)
    {
        float armLen = retractedArmLength + extendDistance * ext01;
        if (telescopingArm != null)
        {
            Vector3 s = telescopingArm.localScale;
            s.y = armLen;
            telescopingArm.localScale = s;
            telescopingArm.localPosition = new Vector3(0f, -armLen * 0.5f, 0f);
        }
        if (nozzleTip != null)
        {
            nozzleTip.localPosition = new Vector3(0f, -armLen, 0f);
        }

        if (_warningMat == null && warningRenderer != null)
            _warningMat = warningRenderer.material;

        if (_warningMat != null)
        {
            // Copper electromagnetic coil glows warm orange-red when charging to drop a part
            Color c = Color.Lerp(new Color(0.76f, 0.43f, 0.20f), new Color(1.0f, 0.22f, 0.08f), glow01);
            _warningMat.color = c;
            if (_warningMat.HasProperty("_BaseColor")) _warningMat.SetColor("_BaseColor", c);
        }
    }

    void CheckDirectArmHit()
    {
        if (!nozzleTip || !player || player.isDead) return;
        Vector3 tipWorld = nozzleTip.position;
        Vector3 pPos = player.transform.position;
        if (Vector3.Distance(tipWorld, pPos) < player.CapsuleRadius + 0.35f)
        {
            player.TakeDamage(projectileDamage);
        }
    }

    /// <summary>
    /// Spawns a cassette-world mechanical hazard part (alternating between a loose Phillips cassette screw,
    /// a miniature toothed capstan gear, and a copper-wound ferrite inductor core).
    /// </summary>
    void SpawnCassetteWorldHazard()
    {
        Vector3 spawnPos = nozzleTip != null ? nozzleTip.position + Vector3.down * 0.32f : transform.position + Vector3.down * 2.5f;
        if (player != null)
        {
            spawnPos.z = Mathf.Clamp(player.transform.position.z, -0.75f, 0.75f);
        }

        int partType = (_dropCounter++) % 3;
        GameObject projGo = BuildCassetteHazardModel(partType, spawnPos);

        var proj = projGo.AddComponent<CassetteHazardProjectile>();
        proj.player = player;
        proj.damage = projectileDamage;
        proj.gravity = projectileGravity;
        proj.floorY = floorDestroyY;
    }

    static void RemoveColliderSafe(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }

    static GameObject BuildCassetteHazardModel(int partType, Vector3 worldPos)
    {
        var root = new GameObject(partType == 0 ? "LooseCassetteScrew" : (partType == 1 ? "DroppedCapstanGear" : "FerriteCopperCoil"));
        root.transform.position = worldPos;
        // Scale 1.65x so falling cassette parts are bold, unmistakable hazards against the background
        root.transform.localScale = Vector3.one * 1.65f;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var brassMat = new Material(shader) { color = new Color(0.86f, 0.64f, 0.24f) };
        if (brassMat.HasProperty("_BaseColor")) brassMat.SetColor("_BaseColor", brassMat.color);

        var darkMat = new Material(shader) { color = new Color(0.16f, 0.17f, 0.20f) };
        if (darkMat.HasProperty("_BaseColor")) darkMat.SetColor("_BaseColor", darkMat.color);

        var copperHazardMat = new Material(shader) { color = new Color(0.95f, 0.28f, 0.12f) };
        if (copperHazardMat.HasProperty("_BaseColor")) copperHazardMat.SetColor("_BaseColor", copperHazardMat.color);

        var gearMat = new Material(shader) { color = new Color(0.95f, 0.88f, 0.72f) };
        if (gearMat.HasProperty("_BaseColor")) gearMat.SetColor("_BaseColor", gearMat.color);

        if (partType == 0)
        {
            // ---- Part 0: Loose Brass/Copper Cassette Phillips Screw (脱落的磁带黄铜十字螺丝) ----
            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(head);
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            head.transform.localScale = new Vector3(0.46f, 0.08f, 0.46f);
            head.GetComponent<Renderer>().sharedMaterial = brassMat;

            var slot1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            RemoveColliderSafe(slot1);
            slot1.transform.SetParent(root.transform, false);
            slot1.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            slot1.transform.localScale = new Vector3(0.32f, 0.03f, 0.06f);
            slot1.GetComponent<Renderer>().sharedMaterial = darkMat;

            var slot2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            RemoveColliderSafe(slot2);
            slot2.transform.SetParent(root.transform, false);
            slot2.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            slot2.transform.localScale = new Vector3(0.06f, 0.03f, 0.32f);
            slot2.GetComponent<Renderer>().sharedMaterial = darkMat;

            var washer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(washer);
            washer.transform.SetParent(root.transform, false);
            washer.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            washer.transform.localScale = new Vector3(0.52f, 0.03f, 0.52f);
            washer.GetComponent<Renderer>().sharedMaterial = copperHazardMat;

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(shaft);
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            shaft.transform.localScale = new Vector3(0.20f, 0.18f, 0.20f);
            shaft.GetComponent<Renderer>().sharedMaterial = brassMat;

            for (int t = 0; t < 3; t++)
            {
                var thread = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                RemoveColliderSafe(thread);
                thread.transform.SetParent(root.transform, false);
                thread.transform.localPosition = new Vector3(0f, -0.04f - t * 0.09f, 0f);
                thread.transform.localRotation = Quaternion.Euler(8f, 0f, 8f);
                thread.transform.localScale = new Vector3(0.27f, 0.02f, 0.27f);
                thread.GetComponent<Renderer>().sharedMaterial = darkMat;
            }
        }
        else if (partType == 1)
        {
            // ---- Part 1: Miniature Toothed Cassette Capstan Gear (掉落的走带齿轮) ----
            var disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(disk);
            disk.transform.SetParent(root.transform, false);
            disk.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disk.transform.localScale = new Vector3(0.42f, 0.09f, 0.42f);
            disk.GetComponent<Renderer>().sharedMaterial = gearMat;

            var copperHub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(copperHub);
            copperHub.transform.SetParent(root.transform, false);
            copperHub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            copperHub.transform.localScale = new Vector3(0.26f, 0.105f, 0.26f);
            copperHub.GetComponent<Renderer>().sharedMaterial = copperHazardMat;

            var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(hole);
            hole.transform.SetParent(root.transform, false);
            hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hole.transform.localScale = new Vector3(0.11f, 0.115f, 0.11f);
            hole.GetComponent<Renderer>().sharedMaterial = darkMat;

            const int teeth = 10;
            for (int i = 0; i < teeth; i++)
            {
                float a = i * Mathf.PI * 2f / teeth;
                var tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                RemoveColliderSafe(tooth);
                tooth.transform.SetParent(root.transform, false);
                tooth.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.23f, Mathf.Sin(a) * 0.23f, 0f);
                tooth.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                tooth.transform.localScale = new Vector3(0.12f, 0.07f, 0.16f);
                tooth.GetComponent<Renderer>().sharedMaterial = copperHazardMat;
            }
        }
        else
        {
            // ---- Part 2: Magnetic Head Copper Inductor Spool / Ferrite Core (磁头铜线圈磁芯) ----
            var core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(core);
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            core.GetComponent<Renderer>().sharedMaterial = darkMat;

            var winding = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(winding);
            winding.transform.SetParent(root.transform, false);
            winding.transform.localScale = new Vector3(0.42f, 0.16f, 0.42f);
            winding.GetComponent<Renderer>().sharedMaterial = copperHazardMat;

            var topCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(topCap);
            topCap.transform.SetParent(root.transform, false);
            topCap.transform.localPosition = new Vector3(0f, 0.19f, 0f);
            topCap.transform.localScale = new Vector3(0.46f, 0.04f, 0.46f);
            topCap.GetComponent<Renderer>().sharedMaterial = brassMat;

            var botCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RemoveColliderSafe(botCap);
            botCap.transform.SetParent(root.transform, false);
            botCap.transform.localPosition = new Vector3(0f, -0.19f, 0f);
            botCap.transform.localScale = new Vector3(0.46f, 0.04f, 0.46f);
            botCap.GetComponent<Renderer>().sharedMaterial = brassMat;
        }

        return root;
    }
}
