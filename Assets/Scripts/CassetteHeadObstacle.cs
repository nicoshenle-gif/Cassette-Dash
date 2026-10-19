using UnityEngine;

/// <summary>
/// Moves the black TapeHeadPad (along with its brown felt strip and V-shaped leaf springs)
/// up and down like a reciprocating piston obstacle on the bottom tape run.
/// </summary>
public class CassetteHeadObstacle : MonoBehaviour
{
    [Header("Vertical Stroke (World / Local Y)")]
    [Tooltip("Y position of the bottom tape surface.")]
    public float tapeSurfaceY = -4.675f;
    [Tooltip("Clearance above the bottom tape at the top of the stroke (player height is ~0.62).")]
    public float topClearance = 0.76f;
    [Tooltip("Clearance above the bottom tape at the bottom of the stroke (0 = slams flush onto the tape).")]
    public float bottomClearance = 0.0f;

    [Header("Timing & Speed")]
    [Tooltip("Time in seconds to slam down from top to bottom.")]
    public float moveDownDuration = 0.45f;
    [Tooltip("Time in seconds to pause at the bottom on the tape.")]
    public float holdBottomDuration = 0.40f;
    [Tooltip("Time in seconds to rise back up from bottom to top.")]
    public float moveUpDuration = 0.75f;
    [Tooltip("Time in seconds to pause at the top (safe window to pass underneath).")]
    public float holdTopDuration = 0.65f;

    [Header("Obstacle Damage (障碍物伤害 - 每次扣50血)")]
    [Tooltip("If true, getting squashed underneath deals 100% damage; if false, deals contactDamage (50 HP).")]
    public bool lethalCrushUnderneath = false;
    [Tooltip("Damage dealt when hit by the moving head pad obstacle (50 HP per hit).")]
    public float contactDamage = 50f;
    [Tooltip("Horizontal knockback speed when hit by the obstacle.")]
    public float knockbackSpeed = 3.2f;
    public float hitCooldown = 0.75f;

    [Header("Linked Visual Parts (Auto-Detected)")]
    public Transform feltTransform;
    public Transform springLeft;
    public Transform springRight;
    public CassettePlayer player;

    float _padHeight = 0.425f;
    float _padWidth = 1.19f;
    float _feltLocalOffsetY = -0.1615f;
    float _springAnchorTopY = -3.32f;
    float _springThickness = 0.085f;
    float _timer;
    float _cooldownTimer;
    int _lastSoundPhase = -1;
    float _currentBottomY;
    bool _isMovingDown;
    Rigidbody _rb;

    void Awake()
    {
        AutoBindReferences();

        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _currentBottomY = tapeSurfaceY + topClearance;
        ApplyPositions(_currentBottomY);
    }

    public void AutoBindReferences()
    {
        if (!player) player = FindFirstObjectByType<CassettePlayer>();

        _padHeight = transform.localScale.y > 0.01f ? transform.localScale.y : 0.425f;
        _padWidth = transform.localScale.x > 0.01f ? transform.localScale.x : 1.19f;

        Transform parent = transform.parent;
        if (parent != null)
        {
            if (!feltTransform) feltTransform = parent.Find("TapeHeadPad_Felt");
            if (!springLeft) springLeft = parent.Find("PadSpring_L");
            if (!springRight) springRight = parent.Find("PadSpring_R");

            Transform bottomTape = parent.Find("BottomTapeRun_Collider");
            if (bottomTape != null)
                tapeSurfaceY = bottomTape.localPosition.y;
        }

        _springAnchorTopY = tapeSurfaceY + 1.35f;

        if (feltTransform != null)
        {
            _springThickness = springLeft != null ? springLeft.localScale.y : 0.085f;
        }
    }

    void FixedUpdate()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.fixedDeltaTime;

        float downDur = Mathf.Max(0.08f, moveDownDuration);
        float botDur = Mathf.Max(0f, holdBottomDuration);
        float upDur = Mathf.Max(0.08f, moveUpDuration);
        float topDur = Mathf.Max(0f, holdTopDuration);
        float totalCycle = downDur + botDur + upDur + topDur;

        _timer = (_timer + Time.fixedDeltaTime) % totalCycle;

        float topBottomY = tapeSurfaceY + topClearance;
        float lowBottomY = tapeSurfaceY + bottomClearance;

        float t = _timer;
        _isMovingDown = false;

        // ---- SFX: magnetic head slamming down (SFX_15) at the start of every down stroke ----
        int soundPhase = t < downDur ? 0 : (t < downDur + botDur ? 1 : (t < downDur + botDur + upDur ? 2 : 3));
        if (soundPhase != _lastSoundPhase)
        {
            if (soundPhase == 0)
                CassetteAudio.PlayAt(CassetteSfx.HeadSlam, transform.position, 1f, Random.Range(0.97f, 1.04f));
            _lastSoundPhase = soundPhase;
        }

        if (t < downDur)
        {
            // Slamming down (fast ease-in)
            float u = t / downDur;
            float curved = u * u;
            _currentBottomY = Mathf.Lerp(topBottomY, lowBottomY, curved);
            _isMovingDown = true;
        }
        else if (t < downDur + botDur)
        {
            // Holding at the bottom
            _currentBottomY = lowBottomY;
        }
        else if (t < downDur + botDur + upDur)
        {
            // Rising back up (smooth step)
            float u = (t - downDur - botDur) / upDur;
            float curved = Mathf.SmoothStep(0f, 1f, u);
            _currentBottomY = Mathf.Lerp(lowBottomY, topBottomY, curved);
        }
        else
        {
            // Holding at the top
            _currentBottomY = topBottomY;
        }

        ApplyPositions(_currentBottomY);
        CheckPlayerCrushAndCollision();
    }

    void ApplyPositions(float padBottomY)
    {
        float padCenterY = padBottomY + _padHeight * 0.5f;
        float padTopY = padBottomY + _padHeight;

        Vector3 localPos = transform.localPosition;
        localPos.y = padCenterY;

        if (_rb != null && Application.isPlaying)
        {
            Vector3 worldTarget = transform.parent != null
                ? transform.parent.TransformPoint(localPos)
                : localPos;
            _rb.MovePosition(worldTarget);
        }
        transform.localPosition = localPos;

        // Move the brown felt strip with the bottom of the pad
        if (feltTransform != null)
        {
            float feltHeight = feltTransform.localScale.y;
            Vector3 feltPos = feltTransform.localPosition;
            feltPos.x = localPos.x;
            feltPos.y = padBottomY + feltHeight * 0.5f;
            feltTransform.localPosition = feltPos;
        }

        // Update the V-shaped leaf springs so they compress/extend between ScrewPost_Center and the top of TapeHeadPad
        Vector2 topAnchor = new Vector2(localPos.x, _springAnchorTopY);
        if (springLeft != null)
            UpdateSpringSegment(springLeft, topAnchor, new Vector2(localPos.x - _padWidth * 0.4f, padTopY));
        if (springRight != null)
            UpdateSpringSegment(springRight, topAnchor, new Vector2(localPos.x + _padWidth * 0.4f, padTopY));
    }

    void UpdateSpringSegment(Transform seg, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        Vector2 mid = (a + b) * 0.5f;
        Vector3 lp = seg.localPosition;
        seg.localPosition = new Vector3(mid.x, mid.y, lp.z);

        Vector3 ls = seg.localScale;
        ls.x = d.magnitude + _springThickness;
        seg.localScale = ls;

        seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    void CheckPlayerCrushAndCollision()
    {
        if (!player || player.isDead || _cooldownTimer > 0f) return;

        Vector3 padWorld = transform.position;
        Vector3 padHalf = transform.lossyScale * 0.5f;
        Vector3 pPos = player.transform.position;
        float pRad = player.CapsuleRadius;
        float pHalfH = player.CapsuleHalfHeight;

        // Check Z overlap with the narrow head pad
        if (Mathf.Abs(pPos.z - padWorld.z) > padHalf.z + pRad * 0.75f)
            return;

        // Check X & Y overlap
        float dx = Mathf.Abs(pPos.x - padWorld.x);
        float playerTopY = pPos.y + pHalfH;
        float playerBotY = pPos.y - pHalfH;
        float padBotY = padWorld.y - padHalf.y;
        float padTopY = padWorld.y + padHalf.y;

        // 1. Player is directly underneath the descending pad and gets squashed between the pad and the floor/tape
        bool horizontallyUnderPad = dx < (padHalf.x + pRad * 0.55f);
        if (horizontallyUnderPad && _isMovingDown && padBotY <= playerTopY + 0.04f && playerBotY <= tapeSurfaceY + 0.25f)
        {
            _cooldownTimer = hitCooldown;
            player.TakeDamage(lethalCrushUnderneath ? player.maxHealth : contactDamage);
            return;
        }

        // 2. General collision with the moving obstacle (e.g. side impact while moving)
        bool xOverlap = dx < (padHalf.x + pRad + 0.03f);
        bool yOverlap = playerTopY > (padBotY + 0.02f) && playerBotY < (padTopY - 0.04f);
        if (xOverlap && yOverlap && contactDamage > 0f)
        {
            _cooldownTimer = hitCooldown;
            player.TakeDamage(contactDamage);

            if (player.Body != null && !player.isDead)
            {
                float pushDir = pPos.x < padWorld.x ? -1f : 1f;
                Vector3 v = player.Body.linearVelocity;
                v.x = pushDir * knockbackSpeed;
                v.y = Mathf.Max(v.y, 1.8f);
                player.Body.linearVelocity = v;
            }
        }
    }
}
