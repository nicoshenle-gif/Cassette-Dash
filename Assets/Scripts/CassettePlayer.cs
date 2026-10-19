using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 2.5D side-scroller player controller:
///   A / D       : Left / Right movement (X axis)
///   Left / Right: In / Out depth movement (Z axis)
///   Space       : Jump (Y axis)
///   Hold E      : Accelerate left reel to feed slack hammock
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class CassettePlayer : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.2f;
    public float depthSpeed = 1.9f;
    public float groundAccel = 26f;
    public float airAccel = 12f;
    public float jumpSpeed = 4.8f;
    public float extraGravity = 8f;
    [Tooltip("Hold Ctrl to crouch: movement speed multiplier while crouching (jumping is disabled while crouched).")]
    [Range(0.1f, 1f)]
    public float crouchSpeedMultiplier = 0.45f;
    public bool isCrouching;

    [Header("Crouch Collider (碰撞体随真实蹲姿变小)")]
    [Tooltip("Capsule height while crouching. Measured from the real crouch animation pose: the model's " +
             "crouched bounding-box height is ~0.518 while the standing pose (capsule height) is 0.62.")]
    public float crouchCapsuleHeight = 0.52f;
    [Tooltip("How far to lower the character model while crouching. The crouch animation keeps its feet " +
             "~0.097 above the standing soles, so dropping the model by that amount plants the feet on the " +
             "ground and keeps the visible pose inside the shrunken collider.")]
    public float crouchModelDropY = 0.097f;
    [Tooltip("Blend speed (1/s) for the stand <-> crouch collider/model transition.")]
    public float crouchStanceBlendSpeed = 12f;

    [Header("Depth Bounds (Z axis inside the cassette shell)")]
    public float minZ = -0.85f;
    public float maxZ = 0.85f;

    [Header("Visual Model & Animation")]
    public Transform modelRoot;
    public Animator animator;
    [Tooltip("If enabled, automatically aligns the character's shoe soles to the bottom of the CapsuleCollider on Start.")]
    public bool autoAlignFeetOnStart = false;

    [Header("Tape & Reel Control")]
    public CassetteTape tape;
    [Tooltip("Hold E to spin the left reel faster and create a slack hammock on the left span.")]
    public bool holdEToFeedSlack = true;
    [Tooltip("Hold Q to spin the right reel clockwise and create a slack hammock on the right span.")]
    public bool holdQToFeedRightSlack = true;
    [Tooltip("How strongly the moving tape and spinning reels carry the player along (1 = 100% match tape speed).")]
    [Range(0f, 2f)]
    public float tapeCarryMultiplier = 1.0f;

    [Header("Health & Fall Damage (血量与摔落伤害 - 按坠落高度判定)")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    [Tooltip("Minimum vertical drop distance (in units) required to play the landing animation (ignores ordinary ground jumps).")]
    public float minLandAnimHeight = 1.25f;
    [Tooltip("Vertical drop distance (in units) below which no fall damage is taken.")]
    public float safeFallHeight = 5.6f;
    [Tooltip("Vertical drop distance (in units) that deals 100% lethal fall damage.")]
    public float lethalFallHeight = 8.2f;
    [Tooltip("Extra seconds to stay on the ground AFTER the knockdown animation finishes playing 100% (or press R anytime to respawn immediately).")]
    public float postFallHoldTime = 1.0f;
    [Tooltip("Fallback seconds before respawning if no Animator is present.")]
    public float autoRespawnDelay = 4.5f;
    [Tooltip("Show floating health bar above the character's head in addition to the top-left HUD bar.")]
    public bool showOverheadHealthBar = true;

    [Header("Character Info (Read-Only)")]
    public bool isGrounded;
    public bool isOnTape;
    public bool isDead;
    public bool isClimbingLadder;
    public bool isBoardedOnTrain;
    public Vector2 currentTapeCarryVelocity;
    public float lastFallDistance;

    [Header("Nearby Interactive Objects")]
    public CassetteLadder currentNearbyLadder;
    public CassetteRoofTrain currentNearbyTrain;

    Rigidbody _rb;
    CapsuleCollider _col;
    Collider _groundCollider;
    float _moveInputX;
    float _moveInputZ;
    bool _jumpPressed;
    bool _feedSlackHeld;
    bool _feedRightSlackHeld;
    bool _respawnPressed;
    bool _interactPressed;
    float _ladderInputY;
    float _jumpCooldown;
    float _targetYaw = 180f; // facing camera (-Z) by default
    // Ladder currently being climbed. Kept separately from currentNearbyLadder (which is driven by the
    // ladder's trigger volume) so a trigger exit during a teleport can never corrupt the climb state.
    CassetteLadder _activeLadder;

    // Fall tracking & health UI state
    Vector3 _spawnPosition;
    bool _wasSupportedLastStep = true;
    float _fallPeakY;
    float _maxDownwardSpeed;
    float _displayedHealth = 100f;
    float _deadTimer;
    float _postAnimHoldTimer;
    bool _fallAnimCompleted;
    float _footstepTimer;
    bool _feedLoopOn;
    float _damageFlashTimer;
    float _hitInvulnTimer;
    float _landAnimCooldown;
    float _lastDamageAmount;
    Vector3 _lastDamageWorldPos;
    static Texture2D _whiteTex;
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    static readonly int FallStateHash = Animator.StringToHash("Fall");
    static readonly int LandTriggerHash = Animator.StringToHash("Land");
    static readonly int HitTriggerHash = Animator.StringToHash("Hit");
    static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    bool _crouchHeld;
    Renderer[] _modelRenderers;
    Transform _boardedTrain;
    static readonly Vector3 TrainSeatLocal = new Vector3(-0.85f, 0.42f, 0f);

    // Crouch stance state (collider height + model vertical offset)
    bool _stanceInitialized;
    float _standCapsuleHeight = 0.62f;
    float _standCapsuleCenterY;
    float _modelRootBaseY;
    float _crouchBlend;

    public Rigidbody Body => _rb;
    public float CapsuleRadius => _col ? _col.radius * Mathf.Max(transform.localScale.x, transform.localScale.z) : 0.14f;
    public float CapsuleHalfHeight => _col ? Mathf.Max(_col.height * 0.5f * transform.localScale.y, CapsuleRadius) : 0.31f;
    /// <summary>World-space centre of the capsule's bottom cap, honouring the collider's centre offset
    /// (the crouch stance keeps this point pinned to the ground while the capsule shrinks).</summary>
    public Vector3 BottomPoint => _col
        ? transform.TransformPoint(_col.center + Vector3.down * (_col.height * 0.5f))
        : transform.position + Vector3.down * CapsuleHalfHeight;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<CapsuleCollider>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.maxDepenetrationVelocity = 2.5f;
        if (!tape) tape = FindFirstObjectByType<CassetteTape>();
        if (!animator) animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
        _displayedHealth = maxHealth;

        // Upgrade stale serialized thresholds from earlier shorter cassette shell versions
        if (lethalFallHeight < 7.0f)
        {
            safeFallHeight = 5.6f;
            lethalFallHeight = 8.2f;
        }

        if (jumpSpeed < 4.0f)
            jumpSpeed = 4.8f;

        // Ensure the black TapeHeadPad has its moving obstacle component attached even in already-generated scenes
        if (FindFirstObjectByType<CassetteHeadObstacle>() == null)
        {
            var padGo = GameObject.Find("TapeHeadPad");
            if (padGo != null)
            {
                var obs = padGo.AddComponent<CassetteHeadObstacle>();
                obs.player = this;
                obs.AutoBindReferences();
            }
        }

        if (transform.parent != null)
        {
            CassetteParkourObstacleBuilder.BuildOrUpdateParkourObstacles(transform.parent, this);
            EnsureLadderAndRoofTrain(transform.parent);
        }

        InitCrouchStance();
    }

    // ------------------------------------------------------------------------------------------
    // Crouch stance: shrink the capsule to the real crouched model height and keep the feet pinned.
    // ------------------------------------------------------------------------------------------

    void InitCrouchStance()
    {
        if (!_col) return;
        _standCapsuleHeight = Mathf.Max(0.1f, _col.height);
        _standCapsuleCenterY = _col.center.y;
        if (modelRoot != null) _modelRootBaseY = modelRoot.localPosition.y;
        _stanceInitialized = true;
        ApplyCrouchStance(0f);
    }

    /// <summary>
    /// Applies the current stand/crouch blend:
    ///   • capsule height lerps down to <see cref="crouchCapsuleHeight"/> while its bottom cap stays
    ///     exactly where it was (the centre is shifted by half the height difference),
    ///   • the model is dropped by <see cref="crouchModelDropY"/> so the crouch animation's feet
    ///     (which float above the standing soles) stay planted and inside the shrunken capsule.
    /// </summary>
    void ApplyCrouchStance(float blend01)
    {
        if (!_stanceInitialized) InitCrouchStance();
        blend01 = Mathf.Clamp01(blend01);

        if (_col)
        {
            float crouchH = Mathf.Clamp(crouchCapsuleHeight, 2f * _col.radius + 0.02f, _standCapsuleHeight);
            float h = Mathf.Lerp(_standCapsuleHeight, crouchH, blend01);
            _col.height = h;
            Vector3 c = _col.center;
            c.y = _standCapsuleCenterY + (h - _standCapsuleHeight) * 0.5f;
            _col.center = c;
        }

        if (modelRoot != null)
        {
            Vector3 lp = modelRoot.localPosition;
            lp.y = _modelRootBaseY - crouchModelDropY * blend01;
            modelRoot.localPosition = lp;
        }
    }

    /// <summary>Immediately returns to the standing stance (ladder mount / train boarding / respawn).</summary>
    public void ForceStandStance()
    {
        isCrouching = false;
        _crouchBlend = 0f;
        if (_stanceInitialized) ApplyCrouchStance(0f);
    }

    public void EnsureLadderAndRoofTrain(Transform root)
    {
        var oldLadder = FindFirstObjectByType<CassetteLadder>();
        if (oldLadder != null)
        {
            if (Application.isPlaying) Destroy(oldLadder.gameObject);
            else DestroyImmediate(oldLadder.gameObject);
        }

        float hubX = 9.52f;
        float ladderBotY = 5.25f;
        float ladderTopY = 8.65f;
        float roofY = 8.16f;
        var ladderGo = new GameObject("Cassette_Ladder");
        var ladder = ladderGo.AddComponent<CassetteLadder>();
        ladder.AutoSetup(root, hubX, ladderBotY, ladderTopY, roofY);

        // Cut hole in Wall_Top if Wall_Top exists and has no opening
        var wallTop = GameObject.Find("Wall_Top");
        if (wallTop != null)
        {
            Transform shell = wallTop.transform.parent;
            var ren = wallTop.GetComponent<Renderer>();
            Material shellMat = ren != null ? ren.sharedMaterial : null;

            if (Application.isPlaying) Destroy(wallTop);
            else DestroyImmediate(wallTop);

            float halfW = 18.734f;
            float halfH = 8.16f;
            float wallH = 0.51f;
            float depth = 2.38f;
            float hatchHalfW = 0.45f;

            float leftW = (hubX - hatchHalfW) - (-halfW);
            float leftCenterX = (-halfW + (hubX - hatchHalfW)) * 0.5f;
            var boxL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxL.name = "Wall_Top_Left";
            boxL.transform.SetParent(shell, false);
            boxL.transform.localPosition = new Vector3(leftCenterX, halfH - wallH * 0.5f, 0f);
            boxL.transform.localScale = new Vector3(leftW, wallH, depth);
            if (shellMat != null) boxL.GetComponent<Renderer>().sharedMaterial = shellMat;

            float rightW = halfW - (hubX + hatchHalfW);
            float rightCenterX = ((hubX + hatchHalfW) + halfW) * 0.5f;
            var boxR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxR.name = "Wall_Top_Right";
            boxR.transform.SetParent(shell, false);
            boxR.transform.localPosition = new Vector3(rightCenterX, halfH - wallH * 0.5f, 0f);
            boxR.transform.localScale = new Vector3(rightW, wallH, depth);
            if (shellMat != null) boxR.GetComponent<Renderer>().sharedMaterial = shellMat;
        }

        // Recreate clean train and railway to the right of the hole
        var oldTrain = FindFirstObjectByType<CassetteRoofTrain>();
        if (oldTrain != null)
        {
            if (Application.isPlaying) Destroy(oldTrain.gameObject);
            else DestroyImmediate(oldTrain.gameObject);
        }
        var oldRailway = GameObject.Find("Roof_Railway");
        if (oldRailway != null)
        {
            if (Application.isPlaying) Destroy(oldRailway);
            else DestroyImmediate(oldRailway);
        }

        var trainRootGo = new GameObject("Roof_Train_System");
        var roofTrain = trainRootGo.AddComponent<CassetteRoofTrain>();
#if UNITY_EDITOR
        var railPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Railway.fbx");
        var trainPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Train.fbx");
        roofTrain.Setup(root, railPrefab, trainPrefab);
#else
        roofTrain.Setup(root, null, null);
#endif
    }

    public void HandleInteract()
    {
        if (isBoardedOnTrain) return;

        if (isClimbingLadder)
        {
            DismountLadder(false);
            return;
        }

        if (currentNearbyTrain != null && !currentNearbyTrain.isDeparting)
        {
            currentNearbyTrain.StartDeparture(this);
            return;
        }

        if (currentNearbyLadder != null)
        {
            MountLadder(currentNearbyLadder);
            return;
        }

        // Fallback: trigger callbacks are dispatched during the physics step, so right after a teleport
        // (or on the very first frame of contact) currentNearbyLadder can still be null. Evaluate the
        // mount volume analytically so F never silently fails while standing next to the ladder.
        var ladder = FindFirstObjectByType<CassetteLadder>();
        if (ladder != null && ladder.IsCapsuleInMountRange(transform.position, CapsuleHalfHeight))
        {
            MountLadder(ladder);
        }
    }

    public void MountLadder(CassetteLadder ladder)
    {
        if (!ladder) return;
        ForceStandStance();
        isClimbingLadder = true;
        isGrounded = false;
        isOnTape = false;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _targetYaw = 0f;
        if (modelRoot != null)
            modelRoot.localRotation = Quaternion.Euler(0f, 0f, 0f);

        _activeLadder = ladder;
        currentNearbyLadder = ladder;

        // F must work while standing on any part of the right reel pack. When the pack shrinks towards
        // coreRadius the player's feet end up ~1.3 units below bottomY, so lift the capsule to the
        // bottom of the climb range. Doing this here (instead of only inside FixedUpdate) prevents the
        // climb loop from reading "still below bottom" and auto-dismounting on the very first step.
        _rb.position = ladder.GetBottomMountPosition(_rb.position, CapsuleHalfHeight);
        _jumpPressed = false;
    }

    public void DismountLadder(bool jumpOff)
    {
        isClimbingLadder = false;
        _rb.useGravity = true;
        _activeLadder = null;
        if (jumpOff)
            _rb.linearVelocity = new Vector3(_moveInputX * moveSpeed, jumpSpeed * 0.75f, 0f);
    }

    public void BoardTrain(Transform trainTransform)
    {
        ForceStandStance();
        _feedLoopOn = false;
        CassetteAudio.StopLoop();
        isBoardedOnTrain = true;
        isClimbingLadder = false;
        _activeLadder = null;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;
        if (_col) _col.enabled = false;
        // Kinematic so the character actually rides with the moving train (a non-kinematic Rigidbody
        // would stay behind in world space while the train drives away).
        _rb.isKinematic = true;
        transform.SetParent(trainTransform, true);
        transform.localPosition = TrainSeatLocal;
        _boardedTrain = trainTransform;
        _targetYaw = 90f;
        if (modelRoot != null)
            modelRoot.localRotation = Quaternion.Euler(0f, 90f, 0f);
        if (animator != null)
        {
            animator.SetBool(IsMovingHash, false);
            animator.SetBool(IsCrouchingHash, false);
        }

        // The player rides INSIDE the train cab: hide the whole character so the camera sees only
        // the locomotive, and stop the overhead health bar / damage popups from floating in the cab.
        SetPlayerVisible(false);
        if (animator != null) animator.enabled = false;
    }

    /// <summary>
    /// Shows/hides every character renderer (used when boarding the train, the player is inside it).
    /// Keeps the GameObject active so the Rigidbody/camera-follow state stays valid.
    /// </summary>
    public void SetPlayerVisible(bool visible)
    {
        if (_modelRenderers == null || _modelRenderers.Length == 0)
            _modelRenderers = GetComponentsInChildren<Renderer>(true);
        if (_modelRenderers == null) return;
        for (int i = 0; i < _modelRenderers.Length; i++)
        {
            if (_modelRenderers[i] != null)
                _modelRenderers[i].enabled = visible;
        }
    }

    void Start()
    {
        if (autoAlignFeetOnStart)
            AlignModelFeetToCapsuleBottom();

        _spawnPosition = transform.position;
        _fallPeakY = BottomPoint.y;
        _wasSupportedLastStep = true;
    }

    /// <summary>
    /// Evaluates the Animator's initial Idle pose and bakes the SkinnedMeshRenderer vertices
    /// so the character's shoe soles and center align with the CapsuleCollider.
    /// </summary>
    [ContextMenu("自动对齐模型到胶囊体 (Auto Fit Model To Capsule)")]
    public void AlignModelFeetToCapsuleBottom()
    {
        if (!_col) _col = GetComponent<CapsuleCollider>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator != null && Application.isPlaying)
            animator.Update(0f);

        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0) return;

        bool hasAny = false;
        Bounds b = default;
        Vector3 rootScale = animator != null ? animator.transform.lossyScale : Vector3.one;
        var baked = new Mesh();
        foreach (var smr in smrs)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            smr.BakeMesh(baked, true);
            var verts = baked.vertices;
            Transform t = smr.transform;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 wp = t.position + t.rotation * Vector3.Scale(verts[i], rootScale);
                if (!hasAny) { b = new Bounds(wp, Vector3.zero); hasAny = true; }
                else b.Encapsulate(wp);
            }
        }
        if (Application.isPlaying) Destroy(baked);
        else DestroyImmediate(baked);

        if (hasAny && animator != null)
        {
            Vector3 capWorldCenter = transform.TransformPoint(_col ? _col.center : Vector3.zero);
            float targetSoleY = capWorldCenter.y - CapsuleHalfHeight;
            Vector3 delta = new Vector3(
                capWorldCenter.x - b.center.x,
                targetSoleY - b.min.y,
                capWorldCenter.z - b.center.z);
            animator.transform.position += delta;
        }
    }

    void Update()
    {
        ReadInput();

        if (_interactPressed)
        {
            _interactPressed = false;
            HandleInteract();
        }

        if (isBoardedOnTrain)
        {
            if (animator != null)
            {
                animator.SetBool(IsDeadHash, false);
                animator.SetBool(IsMovingHash, false);
                animator.SetBool(IsCrouchingHash, false);
            }
            return;
        }

        if (isClimbingLadder)
        {
            if (animator != null)
            {
                animator.SetBool(IsDeadHash, false);
                animator.SetBool(IsMovingHash, Mathf.Abs(_ladderInputY) > 0.05f);
                animator.SetBool(IsCrouchingHash, false);
            }
            if (modelRoot != null)
            {
                _targetYaw = 0f;
                Quaternion targetRot = Quaternion.Euler(0f, _targetYaw, 0f);
                modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, targetRot, Time.deltaTime * 12f);
            }
            return;
        }

        // Smooth trailing health bar & damage flash timer
        _displayedHealth = Mathf.MoveTowards(_displayedHealth, currentHealth, maxHealth * 1.4f * Time.deltaTime);
        if (_damageFlashTimer > 0f)
            _damageFlashTimer -= Time.deltaTime;
        if (_hitInvulnTimer > 0f)
            _hitInvulnTimer -= Time.deltaTime;
        if (_landAnimCooldown > 0f)
            _landAnimCooldown -= Time.deltaTime;

        if (_respawnPressed)
        {
            _respawnPressed = false;
            Respawn();
            return;
        }

        if (isDead)
        {
            if (tape)
            {
                tape.boostLeftReel = false;
                tape.boostRightReel = false;
            }
            isCrouching = false;
            if (animator != null)
            {
                animator.SetBool(IsMovingHash, false);
                animator.SetBool(IsCrouchingHash, false);
                animator.SetBool(IsDeadHash, true);
            }

            if (modelRoot != null)
            {
                Quaternion uprightRot = Quaternion.Euler(0f, _targetYaw, 0f);
                modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, uprightRot, Time.deltaTime * 10f);
            }

            _deadTimer += Time.deltaTime;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                bool inFallState = stateInfo.shortNameHash == FallStateHash || stateInfo.IsName("Fall");
                if (!animator.IsInTransition(0) && inFallState && stateInfo.normalizedTime >= 1.0f)
                {
                    _fallAnimCompleted = true;
                }
                else if (_deadTimer > 8.0f)
                {
                    // Safety fallback if Animator has no Fall state
                    _fallAnimCompleted = true;
                }

                if (_fallAnimCompleted)
                {
                    _postAnimHoldTimer += Time.deltaTime;
                    if (_postAnimHoldTimer >= Mathf.Max(0.4f, postFallHoldTime))
                        Respawn();
                }
            }
            else if (autoRespawnDelay > 0f && _deadTimer >= autoRespawnDelay)
            {
                Respawn();
            }
            return;
        }

        if (tape)
        {
            if (holdEToFeedSlack) tape.boostLeftReel = _feedSlackHeld;
            if (holdQToFeedRightSlack) tape.boostRightReel = _feedRightSlackHeld;
        }

        // ---- "出带加速" loop while a reel is being spun to feed tape (SFX_14) ----
        bool feedingTape = (holdEToFeedSlack && _feedSlackHeld) || (holdQToFeedRightSlack && _feedRightSlackHeld);
        if (feedingTape && !isClimbingLadder)
        {
            if (!_feedLoopOn)
            {
                _feedLoopOn = true;
                CassetteAudio.StartLoop(CassetteSfx.TapeFeedLoop);
            }
            else
            {
                CassetteAudio.SetLoopVolume(1f);
            }
        }
        else if (_feedLoopOn)
        {
            _feedLoopOn = false;
            CassetteAudio.StopLoop();
        }

        Vector2 moveDir = new Vector2(_moveInputX, _moveInputZ);
        bool moving = moveDir.sqrMagnitude > 0.01f;

        isCrouching = _crouchHeld && (isGrounded || isOnTape);

        if (animator != null)
        {
            animator.SetBool(IsDeadHash, false);
            animator.SetBool(IsMovingHash, moving);
            animator.SetBool(IsCrouchingHash, isCrouching);
        }

        // Shrink/grow the capsule (and plant the crouched model) while Ctrl is held.
        float targetCrouchBlend = isCrouching ? 1f : 0f;
        if (!Mathf.Approximately(_crouchBlend, targetCrouchBlend))
        {
            _crouchBlend = Mathf.MoveTowards(_crouchBlend, targetCrouchBlend,
                Mathf.Max(0.01f, crouchStanceBlendSpeed) * Time.deltaTime);
            ApplyCrouchStance(_crouchBlend);
        }

        // Smoothly rotate character model to face movement direction
        if (modelRoot != null)
        {
            if (moving)
            {
                _targetYaw = Mathf.Atan2(_moveInputX, _moveInputZ) * Mathf.Rad2Deg;
            }
            Quaternion targetRot = Quaternion.Euler(0f, _targetYaw, 0f);
            modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, targetRot, Time.deltaTime * 12f);
        }
    }

    void LateUpdate()
    {
        // Riding the train: the Rigidbody is kinematic and writes its own world pose during the
        // physics step, so force the visual transform onto the moving cab seat every frame. This
        // keeps the (hidden) character genuinely inside the train instead of being left behind.
        if (isBoardedOnTrain && _boardedTrain != null)
            transform.position = _boardedTrain.TransformPoint(TrainSeatLocal);
    }

    void FixedUpdate()
    {
        if (_jumpCooldown > 0f) _jumpCooldown -= Time.fixedDeltaTime;

        if (isBoardedOnTrain)
        {
            _rb.linearVelocity = Vector3.zero;
            return;
        }

        if (isClimbingLadder)
        {
            _wasSupportedLastStep = true;
            _fallPeakY = BottomPoint.y;
            _maxDownwardSpeed = 0f;

            // Prefer the cached ladder: it stays valid even if the trigger's OnTriggerExit/Enter fires
            // during the climb teleports.
            CassetteLadder ladder = _activeLadder != null ? _activeLadder : currentNearbyLadder;

            float climbSpeed = 3.2f;
            Vector3 posL = _rb.position;
            float targetX = ladder != null ? ladder.ladderX : 9.52f;
            posL.x = Mathf.MoveTowards(posL.x, targetX, 5f * Time.fixedDeltaTime);
            posL.y += _ladderInputY * climbSpeed * Time.fixedDeltaTime;
            posL.z = Mathf.MoveTowards(posL.z, 0f, 5f * Time.fixedDeltaTime);

            float topY = ladder != null ? ladder.ladderTopY : 8.65f;
            float botY = ladder != null ? ladder.ladderBotY : 5.25f;

            // Reached roof top: step out onto the railway walking deck.
            // The Roof_Railway BoxCollider top (8.29) sits 0.13 above the roof plate (8.16); landing on
            // the plate wedges the capsule against that lip so pressing D could not move past it.
            if (posL.y >= topY - 0.22f)
            {
                Vector3 exitPos = ladder != null
                    ? ladder.GetRoofExitPosition(CapsuleHalfHeight)
                    // Fallback mirrors CassetteLadder defaults: rail deck top 8.29 + halfHeight 0.31 + 0.05.
                    : new Vector3(10.55f, topY, 0f);
                posL = exitPos;
                _rb.position = posL;
                _rb.linearVelocity = Vector3.zero;
                DismountLadder(false);
                return;
            }
            // Reached bottom: step onto right reel
            else if (posL.y <= botY + 0.05f)
            {
                posL.y = botY + CapsuleHalfHeight + 0.02f;
                _rb.position = posL;
                _rb.linearVelocity = Vector3.zero;
                DismountLadder(false);
                return;
            }

            _rb.position = posL;
            _rb.linearVelocity = Vector3.zero;

            if (_jumpPressed)
            {
                _jumpPressed = false;
                DismountLadder(true);
            }
            return;
        }

        CheckGround();

        bool groundedAny = isGrounded || isOnTape;
        float feetY = BottomPoint.y;

        // ---- Track Airborne Fall Distance & Apply Fall Damage on Landing ----
        if (!groundedAny)
        {
            if (_wasSupportedLastStep)
            {
                _fallPeakY = feetY;
                _maxDownwardSpeed = 0f;
            }
            else
            {
                if (feetY > _fallPeakY) _fallPeakY = feetY;
            }

            if (_rb.linearVelocity.y < _maxDownwardSpeed)
                _maxDownwardSpeed = _rb.linearVelocity.y;
        }
        else
        {
            if (!_wasSupportedLastStep && !isDead)
            {
                EvaluateLandingImpact(feetY);
            }
            _fallPeakY = feetY;
            _maxDownwardSpeed = 0f;
        }
        _wasSupportedLastStep = groundedAny;

        if (isDead)
        {
            Vector3 deadVel = _rb.linearVelocity;
            deadVel.x = Mathf.MoveTowards(deadVel.x, 0f, 18f * Time.fixedDeltaTime);
            deadVel.z = Mathf.MoveTowards(deadVel.z, 0f, 18f * Time.fixedDeltaTime);
            if (!groundedAny) deadVel.y -= extraGravity * Time.fixedDeltaTime;
            _rb.linearVelocity = deadVel;
            return;
        }

        float accel = groundedAny ? groundAccel : airAccel;

        // Calculate tangential velocity of the tape / reel under the player's feet
        currentTapeCarryVelocity = (groundedAny && tape != null)
            ? tape.GetSurfaceVelocityForPlayer(BottomPoint, _groundCollider, isOnTape) * tapeCarryMultiplier
            : Vector2.zero;

        Vector3 v = _rb.linearVelocity;
        float speedMul = isCrouching ? crouchSpeedMultiplier : 1f;
        float targetVx = currentTapeCarryVelocity.x + _moveInputX * moveSpeed * speedMul;
        v.x = Mathf.MoveTowards(v.x, targetVx, accel * Time.fixedDeltaTime);
        v.z = Mathf.MoveTowards(v.z, _moveInputZ * depthSpeed * speedMul, accel * Time.fixedDeltaTime);

        if (_jumpPressed && groundedAny && _jumpCooldown <= 0f && !isCrouching)
        {
            v.y = jumpSpeed;
            CassetteAudio.Play(CassetteSfx.Jump, 1f, Random.Range(0.97f, 1.04f));
            if (tape && isOnTape)
                tape.ApplyImpulseAtWorldPos(BottomPoint, Vector2.down * 2.5f);
            if (animator != null)
                animator.ResetTrigger(LandTriggerHash);
            isOnTape = false;
            isGrounded = false;
            _groundCollider = null;
            _wasSupportedLastStep = false;
            _fallPeakY = feetY;
            _maxDownwardSpeed = 0f;
            _jumpCooldown = 0.15f;
        }
        else if (!groundedAny)
        {
            v.y -= extraGravity * Time.fixedDeltaTime;
        }
        _jumpPressed = false;
        _rb.linearVelocity = v;

        UpdateFootstepAudio(groundedAny);

        // Clamp Z softly within the cassette interior
        Vector3 pos = _rb.position;
        if (pos.z < minZ) { pos.z = minZ; _rb.position = pos; if (v.z < 0f) v.z = 0f; _rb.linearVelocity = v; }
        if (pos.z > maxZ) { pos.z = maxZ; _rb.position = pos; if (v.z > 0f) v.z = 0f; _rb.linearVelocity = v; }
    }

    void EvaluateLandingImpact(float landingFeetY)
    {
        float fallDist = _fallPeakY - landingFeetY;
        lastFallDistance = Mathf.Max(0f, fallDist);

        // Heavy landing thud (SFX_08) on any notable drop – plays before fall damage is applied.
        if (!isDead && fallDist >= minLandAnimHeight)
            CassetteAudio.Play(CassetteSfx.HardLand, 1f, Random.Range(0.97f, 1.05f));

        // Pure height-based fall damage (applies equally whether landing on floor, rollers, or the tape):
        if (fallDist >= lethalFallHeight)
        {
            TakeDamage(maxHealth);
            return;
        }
        else if (fallDist > safeFallHeight)
        {
            float t = Mathf.InverseLerp(safeFallHeight, lethalFallHeight, fallDist);
            float dmg = Mathf.Round(Mathf.Lerp(25f, maxHealth, t));
            TakeDamage(dmg);
            return;
        }

        // Play landing animation (Player_Land.fbx) only when dropping from a noticeable height (ignores normal flat-ground jumps)
        if (!isDead && _landAnimCooldown <= 0f && animator != null && fallDist >= minLandAnimHeight)
        {
            _landAnimCooldown = 0.30f;
            animator.ResetTrigger(LandTriggerHash);
            animator.SetTrigger(LandTriggerHash);
        }
    }

    /// <summary>
    /// Surface-aware footsteps: tape film on the tape spans (SFX_01) and plastic shell everywhere else (SFX_04).
    /// Only ticks while the player is actually walking, so being carried by the tape stays silent.
    /// </summary>
    void UpdateFootstepAudio(bool groundedAny)
    {
        if (!groundedAny || isDead || isClimbingLadder || isBoardedOnTrain || isCrouching ||
            (Mathf.Abs(_moveInputX) < 0.01f && Mathf.Abs(_moveInputZ) < 0.01f))
        {
            _footstepTimer = 0f;
            return;
        }

        Vector3 vel = _rb.linearVelocity;
        float speed = new Vector2(vel.x, vel.z).magnitude;
        if (speed < 0.30f)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer -= Time.fixedDeltaTime;
        if (_footstepTimer > 0f) return;

        _footstepTimer = Mathf.Clamp(0.46f - speed * 0.055f, 0.24f, 0.44f);
        CassetteAudio.Play(isOnTape ? CassetteSfx.FootstepTape : CassetteSfx.FootstepShell,
            1f, Random.Range(0.92f, 1.08f));
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;
        if (amount < maxHealth && _hitInvulnTimer > 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        _lastDamageAmount = amount;
        _lastDamageWorldPos = transform.position + Vector3.up * (CapsuleHalfHeight + 0.25f);
        _damageFlashTimer = 1.5f;
        _hitInvulnTimer = 0.50f;
        bool fatalHit = currentHealth <= 0f;
        if (fatalHit)
            CassetteAudio.Play(CassetteSfx.Death);      // SFX_12 击倒死亡
        else
            CassetteAudio.Play(CassetteSfx.Hurt, 1f, Random.Range(0.95f, 1.06f)); // SFX_09 受击

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            _deadTimer = 0f;
            _postAnimHoldTimer = 0f;
            _fallAnimCompleted = false;
            isOnTape = false;
            _feedLoopOn = false;
            CassetteAudio.StopLoop();
            if (animator != null)
            {
                animator.ResetTrigger(LandTriggerHash);
                animator.ResetTrigger(HitTriggerHash);
                animator.SetBool(IsMovingHash, false);
                animator.SetBool(IsDeadHash, true);
            }
        }
        else if (animator != null)
        {
            animator.ResetTrigger(LandTriggerHash);
            animator.ResetTrigger(HitTriggerHash);
            animator.SetTrigger(HitTriggerHash);
        }
    }

    public void Respawn()
    {
        CassetteAudio.Play(CassetteSfx.Respawn);
        isDead = false;
        _deadTimer = 0f;
        _postAnimHoldTimer = 0f;
        _fallAnimCompleted = false;
        _damageFlashTimer = 0f;
        _hitInvulnTimer = 0f;
        _landAnimCooldown = 0f;
        currentHealth = maxHealth;
        _displayedHealth = maxHealth;
        isOnTape = false;
        isGrounded = false;
        _groundCollider = null;

        // Never respawn glued to the ladder: if the player died while climbing (hazard hit), restore
        // gravity and clear the climb state so the character actually falls back onto the reels.
        isClimbingLadder = false;
        _activeLadder = null;
        _rb.useGravity = true;
        ForceStandStance();

        // In case the player respawns after a previous boarding/hide, make the character visible again.
        SetPlayerVisible(true);
        if (animator != null) animator.enabled = true;
        _rb.isKinematic = false;
        _boardedTrain = null;

        if (animator != null)
        {
            animator.ResetTrigger(LandTriggerHash);
            animator.ResetTrigger(HitTriggerHash);
            animator.SetBool(IsDeadHash, false);
            animator.SetBool(IsMovingHash, false);
        }

        // Place player safely above the current top of the left reel (or initial spawn point)
        Vector3 targetSpawn = _spawnPosition;
        if (tape != null && tape.leftHub != null)
        {
            float rL = tape.LeftPackRadius;
            targetSpawn = new Vector3(
                tape.leftHub.position.x,
                tape.leftHub.position.y + rL + CapsuleHalfHeight + 0.04f,
                0f);
        }

        _rb.position = targetSpawn;
        transform.position = targetSpawn;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _fallPeakY = targetSpawn.y - CapsuleHalfHeight;
        _maxDownwardSpeed = 0f;
        _wasSupportedLastStep = true;

        if (modelRoot != null)
            modelRoot.localRotation = Quaternion.Euler(0f, _targetYaw, 0f);
    }

    void CheckGround()
    {
        if (_jumpCooldown > 0f)
        {
            isGrounded = false;
            _groundCollider = null;
            return;
        }
        float r = CapsuleRadius * 0.9f;
        // Start at the (possibly centre-offset) capsule bottom cap so the crouched stance still
        // detects the ground correctly even though the capsule centre is lowered.
        Vector3 origin = BottomPoint + Vector3.up * (r + 0.05f);
        if (Physics.SphereCast(origin, r, Vector3.down, out RaycastHit hit, 0.12f, ~0, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            _groundCollider = hit.collider;
        }
        else
        {
            isGrounded = false;
            _groundCollider = null;
        }
    }

    void ReadInput()
    {
        float hx = 0f;
        float hz = 0f;
        float ly = 0f;
        bool jump = false;
        bool feed = false;
        bool feedRight = false;
        bool respawn = false;
        bool crouch = false;
        bool interact = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            // A / D : Left / Right (X axis)
            if (kb.aKey.isPressed) hx -= 1f;
            if (kb.dKey.isPressed) hx += 1f;

            // Left / Right Arrow : In / Out depth (Z axis)
            if (kb.leftArrowKey.isPressed) hz -= 1f;
            if (kb.rightArrowKey.isPressed) hz += 1f;

            // W / S or Up / Down Arrow : Climb ladder (Y axis) or Depth (Z axis)
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            {
                ly += 1f;
                if (!isClimbingLadder) hz += 1f;
            }
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            {
                ly -= 1f;
                if (!isClimbingLadder) hz -= 1f;
            }

            if (kb.spaceKey.wasPressedThisFrame) jump = true;
            if (kb.eKey.isPressed) feed = true;
            if (kb.qKey.isPressed) feedRight = true;
            if (kb.rKey.wasPressedThisFrame) respawn = true;
            if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) crouch = true;
            if (kb.fKey.wasPressedThisFrame) interact = true;
        }
#else
        if (Input.GetKey(KeyCode.A)) hx -= 1f;
        if (Input.GetKey(KeyCode.D)) hx += 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) hz -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) hz += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            ly += 1f;
            if (!isClimbingLadder) hz += 1f;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            ly -= 1f;
            if (!isClimbingLadder) hz -= 1f;
        }
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)) jump = true;
        if (Input.GetKey(KeyCode.E)) feed = true;
        if (Input.GetKey(KeyCode.Q)) feedRight = true;
        if (Input.GetKeyDown(KeyCode.R)) respawn = true;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) crouch = true;
        if (Input.GetKeyDown(KeyCode.F)) interact = true;
#endif
        _moveInputX = Mathf.Clamp(hx, -1f, 1f);
        _moveInputZ = Mathf.Clamp(hz, -1f, 1f);
        _ladderInputY = Mathf.Clamp(ly, -1f, 1f);
        if (jump) _jumpPressed = true;
        if (interact) _interactPressed = true;
        _feedSlackHeld = feed;
        _feedRightSlackHeld = feedRight;
        _crouchHeld = crouch;
        if (respawn) _respawnPressed = true;
    }

    static Texture2D GetWhiteTex()
    {
        if (_whiteTex == null)
        {
            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }
        return _whiteTex;
    }

    static void DrawRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, GetWhiteTex());
        GUI.color = prev;
    }

    void DrawHealthBarWidget(Rect barRect, float hp, float trailHp, float maxHp, bool showText, int fontSize)
    {
        float ratio = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp));
        float trailRatio = Mathf.Clamp01(trailHp / Mathf.Max(1f, maxHp));

        // Outer dark border & background
        DrawRect(barRect, new Color(0.08f, 0.08f, 0.10f, 0.92f));
        Rect inner = new Rect(barRect.x + 2f, barRect.y + 2f, barRect.width - 4f, barRect.height - 4f);
        DrawRect(inner, new Color(0.22f, 0.10f, 0.10f, 0.90f));

        // Trailing damage bar (orange/red)
        if (trailRatio > ratio)
        {
            Rect trailRect = new Rect(inner.x, inner.y, inner.width * trailRatio, inner.height);
            DrawRect(trailRect, new Color(0.95f, 0.45f, 0.15f, 0.95f));
        }

        // Current health fill (Green -> Yellow -> Red)
        if (ratio > 0f)
        {
            Color fillCol = ratio > 0.5f
                ? Color.Lerp(new Color(0.95f, 0.80f, 0.15f), new Color(0.20f, 0.88f, 0.35f), (ratio - 0.5f) * 2f)
                : Color.Lerp(new Color(0.92f, 0.18f, 0.18f), new Color(0.95f, 0.80f, 0.15f), ratio * 2f);
            Rect fillRect = new Rect(inner.x, inner.y, inner.width * ratio, inner.height);
            DrawRect(fillRect, fillCol);
        }

        if (showText)
        {
            var txtStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            txtStyle.normal.textColor = Color.white;
            string label;
            if (isDead)
            {
                float holdDur = Mathf.Max(0.4f, postFallHoldTime);
                label = _fallAnimCompleted
                    ? CassetteLocalization.T("hud.hpDownWait", maxHp, Mathf.Max(0f, holdDur - _postAnimHoldTimer))
                    : CassetteLocalization.T("hud.hpDownFalling", maxHp);
            }
            else
            {
                label = CassetteLocalization.T("hud.hpLabel", Mathf.CeilToInt(hp), maxHp);
            }
            GUI.Label(barRect, label, txtStyle);
        }
    }

    void OnGUI()
    {
        // Title screen / ending cinematic own the screen while active.
        if (CassetteStartScreen.IsActive || CassetteEndingSequence.EndingActive) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(12, 12, 8, 8)
        };
        style.normal.textColor = Color.white;
        string slackInfo = tape
            ? CassetteLocalization.T("hud.slack",
                tape.currentSlack.ToString("F2"), tape.maxSlack.ToString("F1"),
                tape.currentRightSlack.ToString("F2"), tape.maxRightSlack.ToString("F1"),
                transform.position.z.ToString("F2"))
            : "";
        string state = isDead
            ? CassetteLocalization.T("hud.dead")
            : (_feedRightSlackHeld
                ? CassetteLocalization.T("hud.feedRight")
                : (_feedSlackHeld
                    ? CassetteLocalization.T("hud.feedLeft")
                    : CassetteLocalization.T("hud.tips")));

        GUI.Box(new Rect(16, 16, 750, 120),
            CassetteLocalization.T("hud.controls") + "\n" + state + "\n" + slackInfo, style);

        // Top-left HUD Health Bar inside the status box
        DrawHealthBarWidget(new Rect(28, 92, 726, 28), currentHealth, _displayedHealth, maxHealth, true, 14);

        // Overhead Floating Health Bar following the character in screen space
        Camera cam = Camera.main;
        if (showOverheadHealthBar && cam != null && !isBoardedOnTrain)
        {
            Vector3 headWorld = transform.position + Vector3.up * (CapsuleHalfHeight + 0.22f);
            Vector3 sp = cam.WorldToScreenPoint(headWorld);
            if (sp.z > 0f)
            {
                float guiX = sp.x;
                float guiY = Screen.height - sp.y;
                Rect overRect = new Rect(guiX - 52f, guiY - 18f, 104f, 13f);
                DrawHealthBarWidget(overRect, currentHealth, _displayedHealth, maxHealth, false, 10);
            }
        }

        // Floating Damage Popup
        if (_damageFlashTimer > 0f && cam != null)
        {
            float alpha = Mathf.Clamp01(_damageFlashTimer / 1.2f);
            if (isDead)
            {
                DrawRect(new Rect(0, 0, Screen.width, 6), new Color(0.95f, 0.15f, 0.15f, alpha));
            }

            Vector3 popupWorld = _lastDamageWorldPos + Vector3.up * ((1.5f - _damageFlashTimer) * 0.45f);
            Vector3 sp = cam.WorldToScreenPoint(popupWorld);
            if (sp.z > 0f)
            {
                var popStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                popStyle.normal.textColor = new Color(1f, 0.22f, 0.22f, alpha);
                string dmgText = _lastDamageAmount >= maxHealth
                    ? CassetteLocalization.T("hud.damageLethal", _lastDamageAmount)
                    : CassetteLocalization.T("hud.damage", _lastDamageAmount);
                GUI.Label(new Rect(sp.x - 120f, Screen.height - sp.y - 42f, 240f, 28f), dmgText, popStyle);
            }
        }

        // ---- Interaction Hints (Ladder / Train) ----
        if (cam != null)
        {
            var hintBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hintBoxStyle.normal.textColor = Color.yellow;

            if (isBoardedOnTrain)
            {
                var victoryStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                victoryStyle.normal.textColor = new Color(1f, 0.9f, 0.2f);
                float bw = 560f;
                float bh = 54f;
                GUI.Box(new Rect((Screen.width - bw) * 0.5f, 60f, bw, bh), CassetteLocalization.T("hud.victory"), victoryStyle);
            }
            else if (isClimbingLadder)
            {
                float bw = 460f;
                float bh = 42f;
                GUI.Box(new Rect((Screen.width - bw) * 0.5f, Screen.height - 80f, bw, bh), CassetteLocalization.T("hud.ladderClimb"), hintBoxStyle);
            }
            else if (currentNearbyTrain != null)
            {
                Vector3 trainPos = currentNearbyTrain.transform.position + Vector3.up * 1.2f;
                Vector3 sp = cam.WorldToScreenPoint(trainPos);
                if (sp.z > 0f)
                {
                    GUI.Box(new Rect(sp.x - 110f, Screen.height - sp.y - 30f, 220f, 36f), CassetteLocalization.T("hud.pressTrain"), hintBoxStyle);
                }
            }
            else if (currentNearbyLadder != null)
            {
                Vector3 ladderPos = new Vector3(currentNearbyLadder.ladderX, transform.position.y + 0.35f, 0f);
                Vector3 sp = cam.WorldToScreenPoint(ladderPos);
                if (sp.z > 0f)
                {
                    GUI.Box(new Rect(sp.x - 90f, Screen.height - sp.y - 30f, 180f, 36f), CassetteLocalization.T("hud.pressLadder"), hintBoxStyle);
                }
            }
        }
    }
}
