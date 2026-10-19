using UnityEngine;

/// <summary>
/// Wall-mounted cassette-themed crossbow turret for the bottom parkour runway.
/// It slowly tracks the player while they are in the lower part of the cassette, telegraphs the shot
/// by pulling the string + heating the tip glow, then fires a CassetteCrossbowBolt with a little aim
/// prediction and spread. The model itself is built procedurally by CassetteParkourObstacleBuilder.
/// </summary>
public class CassetteCrossbowTurret : MonoBehaviour
{
    public const int TurretVisualVersion = 4;

    [Header("References")]
    public CassettePlayer player;
    public Transform turretHead;
    public Transform muzzle;
    public Renderer warningRenderer;
    public Transform bowString;
    public Transform loadedBolt;
    [Tooltip("Thin warning line drawn from the muzzle to the locked aim point while charging.")]
    public LineRenderer aimLine;

    [Header("Attack")]
    public float damage = 15f;
    public float boltSpeed = 12.5f;
    public float fireInterval = 3.2f;
    public float firstShotDelay = 1.3f;
    public float chargeTime = 0.6f;
    [Range(0f, 1f)] public float aimLeadFactor = 0.35f;
    public float aimSpreadDeg = 2.2f;
    [Tooltip("Fraction of the charge after which the aim locks. The warning line stops following the " +
             "player from this point on, so stepping off the line dodges the bolt.")]
    [Range(0.1f, 0.95f)] public float aimLockFraction = 0.6f;

    [Header("Activation Zone (bottom parkour only)")]
    public float activeMinY = -7.7f;
    public float activeMaxY = -1.6f;
    public float activeHalfWidthX = 18.2f;

    [Header("Visuals")]
    public Color idleGlow = new Color(0.50f, 0.18f, 0.06f);
    public Color hotGlow = new Color(1f, 0.42f, 0.10f);
    public Vector3 stringRestLocal = new Vector3(0.46f, 0f, 0f);
    public Vector3 stringPulledLocal = new Vector3(0.10f, 0f, 0f);
    [Tooltip("Staggers the firing rhythm between turrets in seconds.")]
    public float firstShotPhase = 0f;

    [Header("State (read-only)")]
    public bool isCharging;
    public int builtVersion = TurretVisualVersion;

    float _timer;
    float _charge;
    float _reloadTimer;
    Material _glowInstance;
    Material _aimLineMat;
    bool _bound;
    bool _aimLocked;
    Vector3 _lockedAim;

    void Start()
    {
        AutoBindPartsIfNeeded();
        _timer = firstShotDelay + firstShotPhase;
        if (loadedBolt != null) loadedBolt.gameObject.SetActive(true);
        SetGlow(0f);
        SetStringPull(0f);
    }

    /// <summary>Re-binds the procedurally built child parts (also used after the turret is reloaded from a scene).</summary>
    public void AutoBindPartsIfNeeded()
    {
        if (turretHead == null)
        {
            var t = transform.Find("HeadPivot");
            if (t != null) turretHead = t;
        }

        if (turretHead != null)
        {
            if (muzzle == null)
            {
                var t = turretHead.Find("Muzzle");
                if (t != null) muzzle = t;
            }
            if (bowString == null)
            {
                var t = turretHead.Find("BowString");
                if (t != null) bowString = t;
            }
            if (loadedBolt == null)
            {
                var t = turretHead.Find("LoadedBolt");
                if (t != null) loadedBolt = t;
            }
            if (warningRenderer == null)
            {
                var t = turretHead.Find("WarningGlow");
                if (t != null) warningRenderer = t.GetComponent<Renderer>();
            }
        }

        if (warningRenderer != null && _glowInstance == null)
        {
            _glowInstance = new Material(warningRenderer.sharedMaterial);
            warningRenderer.sharedMaterial = _glowInstance;
        }

        if (aimLine == null)
        {
            var lineT = transform.Find("AimLine");
            if (lineT != null) aimLine = lineT.GetComponent<LineRenderer>();
        }
        if (aimLine != null)
        {
            aimLine.enabled = false;
            // Materials created at runtime (rather than baked into the scene) so a scene reload
            // never leaves the warning line with a missing material.
            if (_aimLineMat == null)
            {
                var lineShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
                if (lineShader != null)
                {
                    _aimLineMat = new Material(lineShader) { color = new Color(1f, 0.35f, 0.12f, 0.85f) };
                    aimLine.sharedMaterial = _aimLineMat;
                }
            }
        }

        _bound = true;
    }

    void Update()
    {
        if (!_bound) AutoBindPartsIfNeeded();
        if (player == null) player = FindAnyObjectByType<CassettePlayer>();
        if (player == null) return;

        if (_reloadTimer > 0f)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f && loadedBolt != null) loadedBolt.gameObject.SetActive(true);
        }

        Vector3 pCenter = player.transform.position;
        bool inZone = !player.isDead &&
                      pCenter.y >= activeMinY && pCenter.y <= activeMaxY &&
                      Mathf.Abs(pCenter.x) <= activeHalfWidthX;
        if (!inZone)
        {
            if (isCharging)
            {
                isCharging = false;
                _charge = 0f;
            }
            _aimLocked = false;
            if (aimLine != null) aimLine.enabled = false;
            SetGlow(0f);
            SetStringPull(0f);
            return;
        }

        // ---- Aim the bow head at the (predicted) player position until the shot locks ----
        Vector3 aim = ComputeAimPoint(pCenter);
        bool canTrack = !isCharging || !_aimLocked;
        if (canTrack && turretHead != null && muzzle != null)
        {
            Vector2 dir = (Vector2)(aim - muzzle.position);
            if (dir.sqrMagnitude > 1e-4f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                turretHead.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        // ---- Charge / fire cycle with a clearly telegraphed, then LOCKED aim ----
        _timer += Time.deltaTime;
        if (!isCharging && _timer >= fireInterval)
        {
            isCharging = true;
            _charge = 0f;
            _aimLocked = false;
            _lockedAim = aim;
        }

        if (isCharging)
        {
            _charge += Time.deltaTime;
            float t = Mathf.Clamp01(_charge / Mathf.Max(0.05f, chargeTime));

            if (!_aimLocked)
            {
                _lockedAim = aim;
                if (t >= aimLockFraction) _aimLocked = true;
            }

            UpdateAimLine(t);
            SetGlow(t);
            SetStringPull(t);
            if (_charge >= chargeTime) Fire(_lockedAim);
        }
    }

    void UpdateAimLine(float t)
    {
        if (aimLine == null || muzzle == null) return;
        aimLine.enabled = true;
        aimLine.SetPosition(0, muzzle.position);
        aimLine.SetPosition(1, _lockedAim);
        Color c = Color.Lerp(new Color(0.95f, 0.68f, 0.18f, 0.28f),
                             new Color(1f, 0.16f, 0.08f, 0.95f),
                             Mathf.Clamp01(t));
        aimLine.startColor = c;
        aimLine.endColor = c;
    }

    Vector3 ComputeAimPoint(Vector3 playerCenter)
    {
        Vector3 vel = player.Body != null ? player.Body.linearVelocity : Vector3.zero;
        float distance = muzzle != null ? Vector3.Distance(muzzle.position, playerCenter) : 10f;
        float travel = distance / Mathf.Max(4f, boltSpeed);
        Vector3 aim = playerCenter + new Vector3(vel.x, vel.y * 0.55f, vel.z) * (travel * aimLeadFactor);
        // Keep the vertical aim near the player's torso, never aiming at the feet or far above the head.
        aim.y = Mathf.Clamp(aim.y, playerCenter.y - 0.08f, playerCenter.y + 0.18f);
        return aim;
    }

    void Fire(Vector3 aim)
    {
        if (muzzle == null)
        {
            isCharging = false;
            return;
        }

        Vector3 dir = aim - muzzle.position;
        if (dir.sqrMagnitude < 1e-4f) dir = transform.right;
        dir.Normalize();
        dir = Quaternion.Euler(0f, 0f, Random.Range(-aimSpreadDeg, aimSpreadDeg)) * dir;

        CassetteCrossbowBolt.Spawn(muzzle.position, dir, boltSpeed, damage, player);

        if (loadedBolt != null) loadedBolt.gameObject.SetActive(false);
        _reloadTimer = 0.55f;

        isCharging = false;
        _charge = 0f;
        _timer = 0f;
        _aimLocked = false;
        if (aimLine != null) aimLine.enabled = false;
        fireInterval = Mathf.Max(1.2f, fireInterval + Random.Range(-0.35f, 0.55f));
        SetGlow(0f);
        SetStringPull(0f);
    }

    void SetGlow(float t)
    {
        if (_glowInstance == null) return;
        Color c = Color.Lerp(idleGlow, hotGlow, Mathf.Clamp01(t));
        _glowInstance.color = c;
        if (_glowInstance.HasProperty("_BaseColor")) _glowInstance.SetColor("_BaseColor", c);
    }

    void SetStringPull(float t)
    {
        if (bowString == null) return;
        bowString.localPosition = Vector3.Lerp(stringRestLocal, stringPulledLocal, Mathf.Clamp01(t));
    }
}
