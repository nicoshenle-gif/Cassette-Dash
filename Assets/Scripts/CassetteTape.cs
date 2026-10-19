using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animated cassette tape with Verlet rope physics on both spans.
/// Left span (Hold E): the left reel pays out extra tape, forming a sagging hammock that sways under
/// the player's weight and gradually tightens back up when E is released.
/// Right span (Hold Q): the right reel pays out slack in the same way, and when Q is released the
/// take-up conveyor carries the player up the span and over the wound tape onto the right reel deck
/// (see the right-span support block in StepVerletAndPlayer).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CassetteTape : MonoBehaviour
{
    [Header("References (local to cassette root)")]
    public Transform leftHub;
    public Transform rightHub;
    public Transform leftRoller;
    public Transform rightRoller;
    public Transform leftPack;
    public Transform rightPack;
    public CassettePlayer player;
    public Transform shellFloor;

    [Header("Geometry (world units)")]
    public float coreRadius = 1.19f;
    public float fullRadius = 2.465f;
    public float rollerRadius = 0.68f;
    public float tapeWidth = 1.7f;
    public float tapeThickness = 0.042f;
    [Range(4, 48)] public int arcSegments = 16;

    [Header("Motion")]
    public float transferDuration = 110f;
    public float tapeLength = 90f;
    public float pauseAtEnd = 0.8f;
    public bool animateInEditMode = false;

    [Header("Slack & Verlet Hammock Physics (Left Span - Press E)")]
    [Tooltip("True while the player holds E to spin the left reel faster.")]
    public bool boostLeftReel = false;
    [Tooltip("Extra linear tape speed paid out by the left reel when boosting.")]
    public float boostExtraSpeed = 6.5f;
    [Tooltip("Speed at which the right reel takes up accumulated slack when not boosting.")]
    public float slackTakeupSpeed = 4.5f;
    [Tooltip("Current extra tape length accumulated in the left span (world units).")]
    [Range(0f, 10f)] public float currentSlack = 0f;
    public float maxSlack = 6.2f;
    [Range(12, 48)] public int leftSpanNodes = 28;

    [Header("Slack & Verlet Hammock Physics (Right Span - Press Q)")]
    [Tooltip("True while the player holds Q to spin the right reel clockwise and feed slack.")]
    public bool boostRightReel = false;
    [Tooltip("Speed at which accumulated right-span slack is retracted when Q is released.")]
    public float rightSlackTakeupSpeed = 4.8f;
    [Tooltip("Current extra tape length accumulated in the right span (world units).")]
    [Range(0f, 10f)] public float currentRightSlack = 0f;
    public float maxRightSlack = 6.2f;
    [Range(12, 48)] public int rightSpanNodes = 28;

    [Header("Hammock Rope Physics Settings")]
    public float ropeGravity = 22f;
    [Range(0.85f, 0.999f)] public float ropeDamping = 0.96f;
    [Range(8, 60)] public int constraintIterations = 32;
    [Tooltip("Downward sag applied by the player's weight when standing on the hammock.")]
    public float playerWeightSag = 18f;

    [Header("State")]
    [Range(0f, 1f)] public float fractionOnLeft = 1f;
    public int direction = 1;

    public Color tapeColor = new Color(0.32f, 0.2f, 0.11f);

    float _leftAngle, _rightAngle, _leftRollerAngle, _rightRollerAngle;
    float _pauseTimer;
    double _lastEditorTime;
    Mesh _mesh;
    Material _material;

    // Surface velocities for carrying the player along with the moving tape & reels
    float _currentLeftSurfaceSpeed;
    float _currentRightSurfaceSpeed;
    Vector2 _lastRopeCarryVel;
    Vector2 _lastRightRopeCarryVel;

    // Dynamic hammock state for the left span (in local XY space of this object)
    Vector2[] _ropePos;
    Vector2[] _ropePrev;
    Vector2[] _ropeDisp;
    Vector2[] _ropeVel;
    float _prevSlack;
    bool _ropeInitialized;

    // Dynamic hammock state for the right span (in local XY space of this object)
    Vector2[] _rightRopePos;
    Vector2[] _rightRopePrev;
    Vector2[] _rightRopeDisp;
    Vector2[] _rightRopeVel;
    float _prevRightSlack;
    bool _rightRopeInitialized;

    // Scratch buffer for the tape that keeps wrapping around the right reel pack past the span
    // endpoint b1. While the player rides the right span, this wound-tape arc is treated as an
    // extension of the support surface so the take-up conveyor can deposit them on the reel deck.
    Vector2[] _rightWrapPos;
    int _rightWrapCount;
    const int RightWrapSegments = 12;
    const float RightWrapSweepDeg = 150f;

    public float LeftPackRadius => PackRadius(fractionOnLeft);
    public float RightPackRadius => PackRadius(1f - fractionOnLeft);

    // ------------------------------------------------------------------------------------------

    void OnEnable()
    {
        if (maxSlack < 5.8f) maxSlack = 6.2f;
        if (maxRightSlack < 5.8f) maxRightSlack = 6.2f;
        if (boostExtraSpeed < 5.5f) boostExtraSpeed = 6.5f;
        EnsureMesh();
        CacheInitialAngles();
        _ropeInitialized = false;
        _rightRopeInitialized = false;
        Rebuild();
#if UNITY_EDITOR
        _lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        if (_mesh != null) { DestroySafe(_mesh); _mesh = null; }
        if (_material != null) { DestroySafe(_material); _material = null; }
    }

    void OnValidate()
    {
        fractionOnLeft = Mathf.Clamp01(fractionOnLeft);
        direction = direction >= 0 ? 1 : -1;
        coreRadius = Mathf.Max(0.01f, coreRadius);
        fullRadius = Mathf.Max(coreRadius + 0.01f, fullRadius);
        currentSlack = Mathf.Clamp(currentSlack, 0f, maxSlack);
        currentRightSlack = Mathf.Clamp(currentRightSlack, 0f, maxRightSlack);
    }

    void FixedUpdate()
    {
        if (!Application.isPlaying) return;
        StepReels(Time.fixedDeltaTime);
        StepVerletAndPlayer(Time.fixedDeltaTime);
    }

    void Update()
    {
        if (Application.isPlaying)
            Rebuild();
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (this == null || Application.isPlaying) return;
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp((float)(now - _lastEditorTime), 0f, 0.05f);
        _lastEditorTime = now;
        if (animateInEditMode)
        {
            StepReels(dt);
            StepVerletAndPlayer(dt);
            Rebuild();
            UnityEditor.SceneView.RepaintAll();
        }
    }
#endif

    // ------------------------------------------------------------------------------------------

    void CacheInitialAngles()
    {
        if (leftHub) _leftAngle = leftHub.localEulerAngles.z;
        if (rightHub) _rightAngle = rightHub.localEulerAngles.z;
        if (leftRoller) _leftRollerAngle = 0f;
        if (rightRoller) _rightRollerAngle = 0f;
    }

    void StepReels(float dt)
    {
        if (dt <= 0f) return;

        if (_pauseTimer > 0f)
        {
            _pauseTimer -= dt;
            _currentLeftSurfaceSpeed = 0f;
            _currentRightSurfaceSpeed = 0f;
            return;
        }

        float baseSpeed = tapeLength / Mathf.Max(0.01f, transferDuration);
        float dsRight = baseSpeed * dt;

        float remaining = (direction > 0 ? fractionOnLeft : 1f - fractionOnLeft) * tapeLength;
        bool reachedEnd = false;
        if (dsRight >= remaining)
        {
            dsRight = remaining;
            reachedEnd = true;
        }

        float rL = PackRadius(fractionOnLeft);
        float rR = PackRadius(1f - fractionOnLeft);

        float sign = direction;

        // Left reel payout: when boosting, left reel turns CCW faster, feeding slack into the left span.
        float dsLeft = sign * dsRight;
        if (boostLeftReel && fractionOnLeft > 0.01f)
        {
            float extra = boostExtraSpeed * dt;
            float room = Mathf.Max(0f, maxSlack - currentSlack);
            float actualExtra = Mathf.Min(extra, room);
            currentSlack += actualExtra;
            // Keep visual/physical spin feedback when holding E
            dsLeft += extra * 0.55f;
        }
        else if (currentSlack > 0f)
        {
            // Right reel continues pulling tape while left reel slows down so slack is gently absorbed
            float takeup = Mathf.Min(currentSlack, slackTakeupSpeed * dt);
            currentSlack -= takeup;
            dsLeft = Mathf.Max(0f, dsLeft - takeup * 0.8f);
        }

        // Right reel payout: when boosting Q, right reel turns CLOCKWISE faster, feeding slack into the right span.
        float extraRightRot = 0f;
        if (boostRightReel)
        {
            float extraR = boostExtraSpeed * dt;
            float roomR = Mathf.Max(0f, maxRightSlack - currentRightSlack);
            float actualExtraR = Mathf.Min(extraR, roomR);
            currentRightSlack += actualExtraR;
            // Clockwise rotation feeds tape into the right span
            extraRightRot = -(extraR / rR) * Mathf.Rad2Deg;
        }
        else if (currentRightSlack > 0f)
        {
            // When Q is released, tape contracts and winds back CCW onto the right reel
            float takeupR = Mathf.Min(currentRightSlack, rightSlackTakeupSpeed * dt);
            currentRightSlack -= takeupR;
            extraRightRot = +(takeupR / rR) * Mathf.Rad2Deg;
        }

        _currentLeftSurfaceSpeed = dsLeft / dt;
        _currentRightSurfaceSpeed = (sign * dsRight) / dt;

        _leftAngle += (dsLeft / rL) * Mathf.Rad2Deg;
        _rightAngle += (sign * dsRight / rR) * Mathf.Rad2Deg + extraRightRot;
        _leftRollerAngle += (sign * dsRight / rollerRadius) * Mathf.Rad2Deg;
        _rightRollerAngle += (sign * dsRight / rollerRadius) * Mathf.Rad2Deg;

        fractionOnLeft = Mathf.Clamp01(fractionOnLeft - sign * dsRight / tapeLength);

        if (reachedEnd)
        {
            fractionOnLeft = direction > 0 ? 0f : 1f;
            direction = -direction;
            _pauseTimer = pauseAtEnd;
        }

        ApplyRotation(leftHub, _leftAngle);
        ApplyRotation(rightHub, _rightAngle);
        ApplyRotation(leftRoller, _leftRollerAngle, true);
        ApplyRotation(rightRoller, _rightRollerAngle, true);
    }

    /// <summary>
    /// Returns the world-space XY velocity of the tape or reel surface directly under the player's feet.
    /// </summary>
    public Vector2 GetSurfaceVelocityForPlayer(Vector3 worldFeetPos, Collider groundCol, bool onTapeRope)
    {
        Vector3 localFeet3 = transform.InverseTransformPoint(worldFeetPos);
        Vector2 p = (Vector2)localFeet3;

        if (onTapeRope)
            return p.x <= 0f ? _lastRopeCarryVel : _lastRightRopeCarryVel;
        bool withinTapeZ = Mathf.Abs(localFeet3.z) <= (tapeWidth * 0.5f + 0.16f);

        if (groundCol != null)
        {
            Transform gt = groundCol.transform;

            // 1. Left Reel Pack / Hub (rotates CCW when _currentLeftSurfaceSpeed > 0 -> top surface moves LEFT)
            if (gt == leftPack || (leftHub != null && (gt == leftHub || gt.IsChildOf(leftHub))))
            {
                Vector2 hl = ToLocal(leftHub.position);
                float rL = Mathf.Max(0.1f, PackRadius(fractionOnLeft));
                Vector2 rel = p - hl;
                float omegaL = _currentLeftSurfaceSpeed / rL;
                return new Vector2(-omegaL * rel.y, omegaL * rel.x);
            }

            // 2. Right Reel Pack / Hub
            if (gt == rightPack || (rightHub != null && (gt == rightHub || gt.IsChildOf(rightHub))))
            {
                Vector2 hr = ToLocal(rightHub.position);
                float rR = Mathf.Max(0.1f, PackRadius(1f - fractionOnLeft));
                Vector2 rel = p - hr;
                float omegaR = _currentRightSurfaceSpeed / rR;
                return new Vector2(-omegaR * rel.y, omegaR * rel.x);
            }

            // 3. Left Guide Roller
            if (leftRoller != null && (gt == leftRoller || gt.IsChildOf(leftRoller)))
            {
                Vector2 cl = ToLocal(leftRoller.position);
                Vector2 rel = p - cl;
                float omega = _currentRightSurfaceSpeed / Mathf.Max(0.1f, rollerRadius);
                return new Vector2(-omega * rel.y, omega * rel.x);
            }

            // 4. Right Guide Roller
            if (rightRoller != null && (gt == rightRoller || gt.IsChildOf(rightRoller)))
            {
                Vector2 cr = ToLocal(rightRoller.position);
                Vector2 rel = p - cr;
                float omega = _currentRightSurfaceSpeed / Mathf.Max(0.1f, rollerRadius);
                return new Vector2(-omega * rel.y, omega * rel.x);
            }

            // 5. Bottom Horizontal Tape Run (moves from left roller bottom to right roller bottom: +X when direction > 0)
            float bottomThresholdY = leftRoller != null ? ToLocal(leftRoller.position).y + rollerRadius + 0.15f : -3.8f;
            if (groundCol.name.Contains("BottomTapeRun") || (withinTapeZ && p.y < bottomThresholdY))
            {
                return new Vector2(_currentRightSurfaceSpeed, 0f);
            }
        }

        return Vector2.zero;
    }

    static void ApplyRotation(Transform t, float angleDeg, bool zAlignedCylinder = false)
    {
        if (!t) return;
        t.localRotation = zAlignedCylinder
            ? Quaternion.Euler(0f, 0f, angleDeg) * Quaternion.Euler(90f, 0f, 0f)
            : Quaternion.Euler(0f, 0f, angleDeg);
    }

    float PackRadius(float fraction)
    {
        float r0 = coreRadius, r1 = fullRadius;
        return Mathf.Sqrt(r0 * r0 + Mathf.Clamp01(fraction) * (r1 * r1 - r0 * r0));
    }

    // ------------------------------------------------------------------------------------------
    // Verlet rope simulation + two-way player coupling
    // ------------------------------------------------------------------------------------------

    void GetLeftSpanEndpoints(out Vector2 a0, out Vector2 a1, out Vector2 hl, out float rL, out Vector2 cl, out float rr)
    {
        rL = PackRadius(fractionOnLeft);
        hl = ToLocal(leftHub.position);
        cl = ToLocal(leftRoller.position);
        float rLFlush = Mathf.Max(0.1f, rL - tapeThickness * 0.5f);
        rr = rollerRadius + tapeThickness * 0.5f;
        Tangent(hl, rLFlush, cl, rr, true, out a0, out a1);

        float s01 = Mathf.Clamp01(currentSlack / Mathf.Max(0.01f, maxSlack));
        if (s01 > 0.001f)
        {
            float curveS = Mathf.SmoothStep(0f, 1f, Mathf.Pow(s01, 0.75f));
            float baseAngleHub = Angle(hl, a0);
            float slackAngleHub = Mathf.LerpAngle(baseAngleHub, 182f, curveS) * Mathf.Deg2Rad;
            a0 = hl + new Vector2(Mathf.Cos(slackAngleHub), Mathf.Sin(slackAngleHub)) * rLFlush;

            float baseAngleRol = Angle(cl, a1);
            // Roller tangent rotates towards the interior (75 deg) so tape leaves pointing down towards the runway
            float slackAngleRol = Mathf.LerpAngle(baseAngleRol, 75f, curveS) * Mathf.Deg2Rad;
            a1 = cl + new Vector2(Mathf.Cos(slackAngleRol), Mathf.Sin(slackAngleRol)) * rr;
        }
    }

    void GetRightSpanEndpoints(out Vector2 b0, out Vector2 b1, out Vector2 hr, out float rR, out Vector2 cr, out float rr)
    {
        rR = PackRadius(1f - fractionOnLeft);
        hr = ToLocal(rightHub.position);
        cr = ToLocal(rightRoller.position);
        float rRFlush = Mathf.Max(0.1f, rR - tapeThickness * 0.5f);
        rr = rollerRadius + tapeThickness * 0.5f;
        Tangent(cr, rr, hr, rRFlush, false, out b0, out b1);

        float s01 = Mathf.Clamp01(currentRightSlack / Mathf.Max(0.01f, maxRightSlack));
        if (s01 > 0.001f)
        {
            float curveS = Mathf.SmoothStep(0f, 1f, Mathf.Pow(s01, 0.75f));
            // b1 on right hub shifts to outermost equator (358 deg) so tape unwinds cleanly
            float baseAngleHub = Angle(hr, b1);
            float slackAngleHub = Mathf.LerpAngle(baseAngleHub, 358f, curveS) * Mathf.Deg2Rad;
            b1 = hr + new Vector2(Mathf.Cos(slackAngleHub), Mathf.Sin(slackAngleHub)) * rRFlush;

            // b0 on right roller shifts over crown to inside (105 deg) so tape leaves pointing down towards the runway
            float baseAngleRol = Angle(cr, b0);
            float slackAngleRol = Mathf.LerpAngle(baseAngleRol, 105f, curveS) * Mathf.Deg2Rad;
            b0 = cr + new Vector2(Mathf.Cos(slackAngleRol), Mathf.Sin(slackAngleRol)) * rr;
        }
    }

    /// <summary>
    /// Evaluates a downward-concave Bezier hammock curve between a hub point and roller point.
    /// tFromHub = 0 at hub, 1 at roller.
    /// Designed with deep catenary sag so the lowest belly sags down to just 0.45 units above the bottom runway,
    /// allowing a player standing on the floor to easily jump and stand directly in the hammock cradle!
    /// </summary>
    Vector2 EvaluateSpanPoint(float tFromHub, Vector2 pHub, Vector2 pRol, float slack, float maxS)
    {
        float s01 = Mathf.Clamp01(slack / Mathf.Max(0.01f, maxS));
        if (s01 <= 0.001f)
            return Vector2.Lerp(pHub, pRol, tFromHub);

        float curveS = Mathf.SmoothStep(0f, 1f, Mathf.Pow(s01, 0.65f));

        Vector2 p1Taut = Vector2.Lerp(pHub, pRol, 1f / 3f);
        Vector2 p2Taut = Vector2.Lerp(pHub, pRol, 2f / 3f);

        float dx = pRol.x - pHub.x;
        // Floor of the cassette shell where the bottom runway lies
        float floorRunwayY = -7.88f;
        // Target lowest dip of the hammock: just 0.45 units above the bottom runway (chest/waist height)!
        float targetValleyY = floorRunwayY + 0.45f;

        // P1 drops steeply down from the elevated hub towards the lower cradle
        float p1SlackY = Mathf.Lerp(p1Taut.y, targetValleyY + 0.85f, curveS);
        Vector2 p1Slack = new Vector2(pHub.x + dx * 0.28f, p1SlackY);

        // P2 plunges deep down directly in front of the roller to form the low valley
        // Using an undershoot in the control point ensures the cubic Bezier valley dips right to targetValleyY
        float p2SlackY = Mathf.Lerp(p2Taut.y, targetValleyY - 1.35f, curveS);
        Vector2 p2Slack = new Vector2(pHub.x + dx * 0.74f, p2SlackY);

        Vector2 p1 = Vector2.Lerp(p1Taut, p1Slack, curveS);
        Vector2 p2 = Vector2.Lerp(p2Taut, p2Slack, curveS);

        float u = 1f - tFromHub;
        return (u * u * u) * pHub
             + (3f * u * u * tFromHub) * p1
             + (3f * u * tFromHub * tFromHub) * p2
             + (tFromHub * tFromHub * tFromHub) * pRol;
    }

    Vector2 EvaluateHammockRestPoint(float t, Vector2 a0, Vector2 a1)
    {
        return EvaluateSpanPoint(t, a0, a1, currentSlack, maxSlack);
    }

    Vector2 EvaluateRightHammockRestPoint(float tFromRoller, Vector2 b0, Vector2 b1)
    {
        return EvaluateSpanPoint(1f - tFromRoller, b1, b0, currentRightSlack, maxRightSlack);
    }

    void EnsureRope(Vector2 a0, Vector2 a1)
    {
        if (_ropePos == null || _ropePos.Length != leftSpanNodes || !_ropeInitialized)
        {
            _ropePos = new Vector2[leftSpanNodes];
            _ropePrev = new Vector2[leftSpanNodes];
            _ropeDisp = new Vector2[leftSpanNodes];
            _ropeVel = new Vector2[leftSpanNodes];
            for (int i = 0; i < leftSpanNodes; i++)
            {
                float t = i / (float)(leftSpanNodes - 1);
                Vector2 p = EvaluateHammockRestPoint(t, a0, a1);
                _ropePos[i] = p;
                _ropePrev[i] = p;
                _ropeDisp[i] = Vector2.zero;
                _ropeVel[i] = Vector2.zero;
            }
            _prevSlack = currentSlack;
            _ropeInitialized = true;
        }
    }

    void EnsureRightRope(Vector2 b0, Vector2 b1)
    {
        if (_rightRopePos == null || _rightRopePos.Length != rightSpanNodes || !_rightRopeInitialized)
        {
            _rightRopePos = new Vector2[rightSpanNodes];
            _rightRopePrev = new Vector2[rightSpanNodes];
            _rightRopeDisp = new Vector2[rightSpanNodes];
            _rightRopeVel = new Vector2[rightSpanNodes];
            for (int i = 0; i < rightSpanNodes; i++)
            {
                float t = i / (float)(rightSpanNodes - 1);
                Vector2 p = EvaluateRightHammockRestPoint(t, b0, b1);
                _rightRopePos[i] = p;
                _rightRopePrev[i] = p;
                _rightRopeDisp[i] = Vector2.zero;
                _rightRopeVel[i] = Vector2.zero;
            }
            _prevRightSlack = currentRightSlack;
            _rightRopeInitialized = true;
        }
    }

    /// <summary>
    /// Builds the arc of tape wound around the right reel pack, starting exactly at the span's hub
    /// endpoint b1 and running counter-clockwise (the take-up direction) over the top of the reel.
    /// Because it starts at b1 with the same flush radius it is a seamless continuation of the right
    /// span: while the player is already riding the span, this arc lets the retracting tape carry them
    /// up the span, across the wound tape and onto the right reel deck instead of stranding them on
    /// the tangent point (and instead of the old unbounded upward launch).
    /// </summary>
    void BuildRightWrapArc(Vector2 hub, Vector2 spanHubEnd, float packRadius)
    {
        int count = RightWrapSegments + 1;
        if (_rightWrapPos == null || _rightWrapPos.Length != count)
            _rightWrapPos = new Vector2[count];
        _rightWrapCount = count;

        float startDeg = Angle(hub, spanHubEnd);
        float arcRadius = Mathf.Max(0.05f, packRadius - tapeThickness * 0.5f);
        for (int i = 0; i < count; i++)
        {
            float deg = (startDeg + RightWrapSweepDeg * (i / (float)RightWrapSegments)) * Mathf.Deg2Rad;
            _rightWrapPos[i] = hub + new Vector2(Mathf.Cos(deg), Mathf.Sin(deg)) * arcRadius;
        }
    }

    public void ApplyImpulseAtWorldPos(Vector3 worldPos, Vector2 impulseVelocity)
    {
        Vector2 localP = ToLocal(worldPos);
        if (localP.x <= 0f)
        {
            if (!_ropeInitialized || _ropePos == null || _ropeVel == null) return;
            for (int i = 1; i < _ropePos.Length - 1; i++)
            {
                float d = Vector2.Distance(_ropePos[i], localP);
                if (d < 1.3f)
                {
                    float w = 1f - d / 1.3f;
                    _ropeVel[i] += impulseVelocity * (w * 0.55f);
                }
            }
        }
        else
        {
            if (!_rightRopeInitialized || _rightRopePos == null || _rightRopeVel == null) return;
            for (int i = 1; i < _rightRopePos.Length - 1; i++)
            {
                float d = Vector2.Distance(_rightRopePos[i], localP);
                if (d < 1.3f)
                {
                    float w = 1f - d / 1.3f;
                    _rightRopeVel[i] += impulseVelocity * (w * 0.55f);
                }
            }
        }
    }

    void StepVerletAndPlayer(float dt)
    {
        if (!leftHub || !leftRoller || !rightHub || !rightRoller || dt <= 0f) return;

        GetLeftSpanEndpoints(out var a0, out var a1, out var hl, out var rL, out var cl, out var rr);
        GetRightSpanEndpoints(out var b0, out var b1, out var hr, out var rR, out var cr, out _);
        EnsureRope(a0, a1);
        EnsureRightRope(b0, b1);

        int nL = _ropePos.Length;
        for (int i = 0; i < nL; i++)
            _ropePrev[i] = _ropePos[i];

        int nR = _rightRopePos.Length;
        for (int i = 0; i < nR; i++)
            _rightRopePrev[i] = _rightRopePos[i];

        float s01 = Mathf.Clamp01(currentSlack / Mathf.Max(0.01f, maxSlack));
        float slackDelta = (currentSlack - _prevSlack) / dt;
        _prevSlack = currentSlack;

        float s01R = Mathf.Clamp01(currentRightSlack / Mathf.Max(0.01f, maxRightSlack));
        float slackDeltaR = (currentRightSlack - _prevRightSlack) / dt;
        _prevRightSlack = currentRightSlack;

        // 1. Coupled spring-wave dynamics around the downward-concave Bezier hammock curves
        // --- Left rope dynamics ---
        float stiffnessL = Mathf.Lerp(55f, 24f, s01);
        float damping = Mathf.Exp(-4.8f * dt);
        for (int i = 1; i < nL - 1; i++)
        {
            float t = i / (float)(nL - 1);
            float bell = Mathf.Sin(t * Mathf.PI);
            Vector2 slackImpulse = new Vector2(0.08f, -0.32f) * (slackDelta * bell * dt);
            Vector2 laplacian = (_ropeDisp[i - 1] + _ropeDisp[i + 1] - 2f * _ropeDisp[i]);
            Vector2 accel = -_ropeDisp[i] * stiffnessL + laplacian * 42f;
            _ropeVel[i] = (_ropeVel[i] + accel * dt + slackImpulse) * damping;
        }

        // --- Right rope dynamics ---
        float stiffnessR = Mathf.Lerp(55f, 24f, s01R);
        for (int i = 1; i < nR - 1; i++)
        {
            float t = i / (float)(nR - 1);
            float bell = Mathf.Sin(t * Mathf.PI);
            Vector2 slackImpulseR = new Vector2(-0.08f, -0.32f) * (slackDeltaR * bell * dt);
            Vector2 laplacianR = (_rightRopeDisp[i - 1] + _rightRopeDisp[i + 1] - 2f * _rightRopeDisp[i]);
            Vector2 accelR = -_rightRopeDisp[i] * stiffnessR + laplacianR * 42f;
            _rightRopeVel[i] = (_rightRopeVel[i] + accelR * dt + slackImpulseR) * damping;
        }

        // 2. Two-way coupling with player
        if (!player && Application.isPlaying)
            player = FindFirstObjectByType<CassettePlayer>();

        bool playerSupportedOnLeft = false;
        bool playerSupportedOnRight = false;
        int bestIdxL = -1, bestIdxR = -1;
        float bestTL = 0f, bestTR = 0f;
        Vector2 bestPtL = Vector2.zero, bestPtR = Vector2.zero;

        if (player && Application.isPlaying && player.Body)
        {
            Rigidbody rb = player.Body;
            Vector3 pFeet3 = transform.InverseTransformPoint(player.BottomPoint);
            Vector2 pFeet = (Vector2)pFeet3;
            float pRad = player.CapsuleRadius;

            bool withinTapeZ = Mathf.Abs(pFeet3.z) <= (tapeWidth * 0.5f + pRad * 0.75f);
            float catchRadius = pRad + 0.55f;
            float prevFeetY = pFeet.y - rb.linearVelocity.y * dt * 1.6f;
            float topSnapMargin = player.isOnTape ? 0.70f : 0.50f;
            // Support state from the previous physics step. The right span is a take-up conveyor that
            // must keep "catching" the player while the slack is retracted (see B below), so it needs
            // to know whether the player was already standing on the strip.
            bool wasOnTapeBeforeStep = player.isOnTape;

            // --- Test Left Span (pFeet.x <= 0) ---
            if (withinTapeZ && pFeet.x <= 0f)
            {
                float bestDistSqL = float.MaxValue;
                for (int i = 0; i < nL - 1; i++)
                {
                    Vector2 s0 = _ropePos[i];
                    Vector2 s1 = _ropePos[i + 1];
                    Vector2 seg = s1 - s0;
                    float lenSq = seg.sqrMagnitude;
                    float t = lenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(pFeet - s0, seg) / lenSq) : 0f;
                    Vector2 proj = s0 + seg * t;
                    float dSq = (pFeet - proj).sqrMagnitude;
                    if (dSq < bestDistSqL)
                    {
                        bestDistSqL = dSq;
                        bestIdxL = i;
                        bestTL = t;
                        bestPtL = proj;
                    }
                }

                bool onSpanSideOfLeftHub = pFeet.x <= hl.x - 0.1f;
                bool descendingOrRestingL = rb.linearVelocity.y <= 3.8f;
                float normPosAlongSpanL = (bestIdxL + bestTL) / Mathf.Max(1f, nL - 1f);
                // Sticky catch (mirrors the right span): the strip must support the player anywhere
                // along its length even when it is taut (no E held). Previously the taut left strip
                // only caught near the reel hub, so simply standing on the middle of the belt made the
                // player fall straight through it ("直接穿模掉下去").
                bool hammockCanCatchL = currentSlack >= 0.08f || wasOnTapeBeforeStep ||
                                        (rb.linearVelocity.y <= 0.5f && normPosAlongSpanL <= 0.99f) ||
                                        (normPosAlongSpanL <= 0.18f && rb.linearVelocity.y >= -2.5f);
                bool verticalHitL = Mathf.Max(pFeet.y, prevFeetY) >= bestPtL.y - 0.65f && pFeet.y <= bestPtL.y + topSnapMargin;

                if (onSpanSideOfLeftHub && bestIdxL >= 0 && descendingOrRestingL &&
                    hammockCanCatchL && Mathf.Abs(pFeet.x - bestPtL.x) <= catchRadius && verticalHitL)
                {
                    playerSupportedOnLeft = true;
                    float sagFactor = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(currentSlack / 0.25f));
                    float impact = Mathf.Clamp(-rb.linearVelocity.y, 0f, 10f);
                    float weightAccelY = -(playerWeightSag * 0.65f + impact * 6.5f) * sagFactor;
                    for (int i = 1; i < nL - 1; i++)
                    {
                        float distNodes = Mathf.Abs(i - (bestIdxL + bestTL));
                        if (distNodes < 5.5f)
                        {
                            float w = Mathf.Cos(distNodes / 5.5f * Mathf.PI * 0.5f);
                            float endFade = Mathf.Sin(i / (float)(nL - 1) * Mathf.PI);
                            _ropeVel[i].y += weightAccelY * w * endFade * dt;
                            _ropeVel[i].x += rb.linearVelocity.x * 0.6f * w * endFade * dt;
                        }
                    }
                }
            }

            // --- Test Right Span (pFeet.x > 0) ---
            if (withinTapeZ && pFeet.x > 0f)
            {
                float bestDistSqR = float.MaxValue;
                bool bestIsWrapR = false;
                for (int i = 0; i < nR - 1; i++)
                {
                    Vector2 s0 = _rightRopePos[i];
                    Vector2 s1 = _rightRopePos[i + 1];
                    Vector2 seg = s1 - s0;
                    float lenSq = seg.sqrMagnitude;
                    float t = lenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(pFeet - s0, seg) / lenSq) : 0f;
                    Vector2 proj = s0 + seg * t;
                    float dSq = (pFeet - proj).sqrMagnitude;
                    if (dSq < bestDistSqR)
                    {
                        bestDistSqR = dSq;
                        bestIdxR = i;
                        bestTR = t;
                        bestPtR = proj;
                    }
                }

                // While the player is already riding the right span, include the strip wound around the
                // right reel pack as extra support surface. The take-up conveyor uses it to deposit the
                // player on top of the right reel rather than leaving them hanging on the tangent point.
                if (wasOnTapeBeforeStep)
                {
                    BuildRightWrapArc(hr, b1, rR);
                    for (int i = 0; i < _rightWrapCount - 1; i++)
                    {
                        Vector2 s0 = _rightWrapPos[i];
                        Vector2 s1 = _rightWrapPos[i + 1];
                        Vector2 seg = s1 - s0;
                        float lenSq = seg.sqrMagnitude;
                        float t = lenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(pFeet - s0, seg) / lenSq) : 0f;
                        Vector2 proj = s0 + seg * t;
                        float dSq = (pFeet - proj).sqrMagnitude;
                        if (dSq < bestDistSqR)
                        {
                            bestDistSqR = dSq;
                            bestPtR = proj;
                            bestIsWrapR = true;
                        }
                    }
                }

                bool onSpanSideOfRightHub = bestIsWrapR
                    ? pFeet.x >= hr.x - 1.0f   // wound tape wraps past the hub: stay supported on its upper-left part
                    : pFeet.x >= hr.x - 0.25f;
                bool descendingOrRestingR = rb.linearVelocity.y <= 3.8f;
                float normPosAlongSpanR = bestIsWrapR ? 1f : (bestIdxR + bestTR) / Mathf.Max(1f, nR - 1f);
                // B) Sticky catch: while the player is already standing on the right strip the hammock
                // keeps catching them even when currentRightSlack drops below 0.08. Before this, the
                // Q take-up released the player as soon as the slack ran out mid-span, so they fell
                // straight through the strip (the reported "穿模掉下去" bug).
                // The right span is also deliberately a little more permissive than the verified left
                // span (which only catches near the hub when taut): a player landing on the mid part of
                // the right strip while descending/resting must be caught, because this is the conveyor
                // up to the right gear and must never drop them through the strip. The roller end
                // (norm < 0.1) is excluded: there the guide roller's own cylinder collider is directly
                // under the strip, so catching there would only make the player stick to the roller.
                bool hammockCanCatchR = currentRightSlack >= 0.08f || wasOnTapeBeforeStep ||
                                        (rb.linearVelocity.y <= 0.5f && normPosAlongSpanR >= 0.10f) ||
                                        (normPosAlongSpanR >= 0.75f && rb.linearVelocity.y >= -2.5f);
                bool verticalHitR = Mathf.Max(pFeet.y, prevFeetY) >= bestPtR.y - 0.65f && pFeet.y <= bestPtR.y + topSnapMargin;

                if (onSpanSideOfRightHub && (bestIdxR >= 0 || bestIsWrapR) && descendingOrRestingR &&
                    hammockCanCatchR && Mathf.Abs(pFeet.x - bestPtR.x) <= catchRadius && verticalHitR)
                {
                    playerSupportedOnRight = true;

                    // The wound-tape arc is kinematic (there are no Verlet nodes on it), so the player's
                    // weight sag is only applied while they stand on the free span itself.
                    if (!bestIsWrapR)
                    {
                        float sagFactorR = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(currentRightSlack / 0.25f));
                        float impactR = Mathf.Clamp(-rb.linearVelocity.y, 0f, 10f);
                        float weightAccelYR = -(playerWeightSag * 0.65f + impactR * 6.5f) * sagFactorR;
                        for (int i = 1; i < nR - 1; i++)
                        {
                            float distNodes = Mathf.Abs(i - (bestIdxR + bestTR));
                            if (distNodes < 5.5f)
                            {
                                float w = Mathf.Cos(distNodes / 5.5f * Mathf.PI * 0.5f);
                                float endFade = Mathf.Sin(i / (float)(nR - 1) * Mathf.PI);
                                _rightRopeVel[i].y += weightAccelYR * w * endFade * dt;
                                _rightRopeVel[i].x += rb.linearVelocity.x * 0.6f * w * endFade * dt;
                            }
                        }
                    }
                }
            }
        }

        // 3. Integrate displacements & evaluate final node positions with keep-out bounds
        // --- Left rope integration ---
        float innerWallLeftX = cl.x - rr - 0.85f;
        // C) Keep the sagging strip above the bottom horizontal tape run. The old clamp sat 0.18 units
        // below the roller bottom, so a weighted hammock (player standing in the dent) visually cut
        // through the bottom tape run and almost touched the shell floor. Clamping the strip centreline
        // to the top surface of that run keeps both strips stacked without visual clipping.
        float bottomRunY = cl.y - rr;
        float floorY = bottomRunY + tapeThickness;
        float packKeepOutL = Mathf.Max(0.1f, rL - tapeThickness * 0.5f);

        _ropePos[0] = a0;
        _ropePos[nL - 1] = a1;
        for (int i = 1; i < nL - 1; i++)
        {
            _ropeDisp[i] += _ropeVel[i] * dt;
            float maxDisp = Mathf.Lerp(0.04f, 0.75f, Mathf.Clamp01(currentSlack / 0.35f));
            _ropeDisp[i].x = Mathf.Clamp(_ropeDisp[i].x, -maxDisp * 0.45f, maxDisp * 0.45f);
            _ropeDisp[i].y = Mathf.Clamp(_ropeDisp[i].y, -maxDisp, maxDisp * 0.45f);

            float t = i / (float)(nL - 1);
            Vector2 p = EvaluateHammockRestPoint(t, a0, a1) + _ropeDisp[i];

            Vector2 toHub = p - hl;
            float dHub = toHub.magnitude;
            if (dHub < packKeepOutL && dHub > 1e-5f) p = hl + toHub / dHub * packKeepOutL;

            Vector2 toRol = p - cl;
            float dRol = toRol.magnitude;
            if (dRol < rr && dRol > 1e-5f) p = cl + toRol / dRol * rr;

            if (p.x < innerWallLeftX) p.x = innerWallLeftX;
            if (p.y < floorY) p.y = floorY;

            _ropePos[i] = p;
        }

        // --- Right rope integration ---
        float innerWallRightX = cr.x + rr + 0.85f;
        float packKeepOutR = Mathf.Max(0.1f, rR - tapeThickness * 0.5f);

        _rightRopePos[0] = b0;
        _rightRopePos[nR - 1] = b1;
        for (int i = 1; i < nR - 1; i++)
        {
            _rightRopeDisp[i] += _rightRopeVel[i] * dt;
            float maxDispR = Mathf.Lerp(0.04f, 0.75f, Mathf.Clamp01(currentRightSlack / 0.35f));
            _rightRopeDisp[i].x = Mathf.Clamp(_rightRopeDisp[i].x, -maxDispR * 0.45f, maxDispR * 0.45f);
            _rightRopeDisp[i].y = Mathf.Clamp(_rightRopeDisp[i].y, -maxDispR, maxDispR * 0.45f);

            float t = i / (float)(nR - 1);
            Vector2 p = EvaluateRightHammockRestPoint(t, b0, b1) + _rightRopeDisp[i];

            Vector2 toHubR = p - hr;
            float dHubR = toHubR.magnitude;
            if (dHubR < packKeepOutR && dHubR > 1e-5f) p = hr + toHubR / dHubR * packKeepOutR;

            Vector2 toRolR = p - cr;
            float dRolR = toRolR.magnitude;
            if (dRolR < rr && dRolR > 1e-5f) p = cr + toRolR / dRolR * rr;

            if (p.x > innerWallRightX) p.x = innerWallRightX;
            if (p.y < floorY) p.y = floorY;

            _rightRopePos[i] = p;
        }

        // 4. Support the player on the hammock curves and lift them smoothly
        if (player && Application.isPlaying && player.Body)
        {
            bool wasOnTape = player.isOnTape;
            bool supported = playerSupportedOnLeft || playerSupportedOnRight;
            player.isOnTape = supported;

            if (playerSupportedOnLeft)
            {
                Rigidbody rb = player.Body;
                Vector3 pFeet3 = transform.InverseTransformPoint(player.BottomPoint);
                Vector2 pFeet = (Vector2)pFeet3;

                float bestDistSq = float.MaxValue;
                Vector2 bestPt = pFeet;
                Vector2 bestTangent = Vector2.right;
                Vector2 bestNodeVel = Vector2.zero;

                for (int i = 0; i < nL - 1; i++)
                {
                    Vector2 s0 = _ropePos[i];
                    Vector2 s1 = _ropePos[i + 1];
                    Vector2 seg = s1 - s0;
                    float lenSq = seg.sqrMagnitude;
                    float t = lenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(pFeet - s0, seg) / lenSq) : 0f;
                    Vector2 proj = s0 + seg * t;
                    float dSq = (pFeet - proj).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestPt = proj;
                        bestTangent = seg.normalized;
                        Vector2 v0 = (_ropePos[i] - _ropePrev[i]) / dt;
                        Vector2 v1 = (_ropePos[i + 1] - _ropePrev[i + 1]) / dt;
                        bestNodeVel = Vector2.Lerp(v0, v1, t);
                    }
                }

                float carryVx = Mathf.Clamp(bestNodeVel.x, -1.2f, 1.2f) + bestTangent.x * _currentRightSurfaceSpeed * 0.7f;
                _lastRopeCarryVel = new Vector2(carryVx, 0f);

                float targetFeetY = bestPt.y + tapeThickness * 0.5f;
                float penetration = targetFeetY - pFeet.y;

                Vector3 vel = rb.linearVelocity;
                Vector2 pCenter = (Vector2)transform.InverseTransformPoint(player.transform.position);
                bool clearOfLeftPack = Vector2.Distance(pCenter, hl) > rL + player.CapsuleRadius + 0.01f;

                if (penetration > 0f || (wasOnTape && clearOfLeftPack && penetration > -0.70f && vel.y <= 1.5f))
                {
                    float ropeVy = Mathf.Clamp(bestNodeVel.y, -1.5f, 0.55f);
                    float maxSupportVy = ropeVy + Mathf.Clamp(penetration * 5f, 0f, 0.65f);
                    if (vel.y < maxSupportVy)
                        vel.y = maxSupportVy;

                    if (clearOfLeftPack)
                        rb.position += transform.TransformVector(new Vector3(0f, penetration, 0f));
                    else if (penetration > 0.015f)
                    {
                        float stepUp = Mathf.Min(penetration * 0.45f, 0.04f);
                        rb.position += transform.TransformVector(new Vector3(0f, stepUp, 0f));
                    }
                }

                if (currentSlack > 0.25f)
                    vel.x += bestTangent.x * bestTangent.y * -3.5f * dt;

                rb.linearVelocity = vel;
            }
            else if (playerSupportedOnRight)
            {
                Rigidbody rb = player.Body;
                Vector3 pFeet3 = transform.InverseTransformPoint(player.BottomPoint);
                Vector2 pFeet = (Vector2)pFeet3;

                float bestDistSq = float.MaxValue;
                Vector2 bestPt = pFeet;
                Vector2 bestTangent = Vector2.right;
                Vector2 bestNodeVel = Vector2.zero;
                bool bestIsWrap = false;

                for (int i = 0; i < nR - 1; i++)
                {
                    Vector2 s0 = _rightRopePos[i];
                    Vector2 s1 = _rightRopePos[i + 1];
                    Vector2 seg = s1 - s0;
                    float lenSq = seg.sqrMagnitude;
                    float t = lenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(pFeet - s0, seg) / lenSq) : 0f;
                    Vector2 proj = s0 + seg * t;
                    float dSq = (pFeet - proj).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestPt = proj;
                        bestTangent = seg.normalized;
                        Vector2 v0 = (_rightRopePos[i] - _rightRopePrev[i]) / dt;
                        Vector2 v1 = (_rightRopePos[i + 1] - _rightRopePrev[i + 1]) / dt;
                        bestNodeVel = Vector2.Lerp(v0, v1, t);
                    }
                }

                // Wound tape over the right reel pack: the same continuation surface the detection
                // step uses, so the take-up can ride the player from the span onto the reel deck.
                if (wasOnTape)
                {
                    BuildRightWrapArc(hr, b1, rR);
                    for (int i = 0; i < _rightWrapCount - 1; i++)
                    {
                        Vector2 s0 = _rightWrapPos[i];
                        Vector2 s1 = _rightWrapPos[i + 1];
                        Vector2 seg = s1 - s0;
                        float lenSq = seg.sqrMagnitude;
                        float t = lenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(pFeet - s0, seg) / lenSq) : 0f;
                        Vector2 proj = s0 + seg * t;
                        float dSq = (pFeet - proj).sqrMagnitude;
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            bestPt = proj;
                            bestTangent = seg.normalized;
                            bestNodeVel = Vector2.zero; // kinematic packed tape, no Verlet nodes
                            bestIsWrap = true;
                        }
                    }
                }

                // Tape-run carry velocity. On the right span the strip runs roller -> hub while the
                // slack is taken up (and during the normal transfer animation), so it conveys the player
                // towards the right reel. Keep this conveyor running after the slack is exhausted, which
                // is what actually delivers the player onto the right reel. While Q pays tape out, hold
                // the carry at zero so the freshly fed dent stays stable to stand in - this mirrors the
                // gentle left-span carry instead of dragging the player towards the hub.
                float rightCarrySpeed = boostRightReel
                    ? 0f
                    : _currentRightSurfaceSpeed + rightSlackTakeupSpeed;
                float carryVx = Mathf.Clamp(bestNodeVel.x, -3.5f, 3.5f) + bestTangent.x * rightCarrySpeed * 0.95f;
                _lastRightRopeCarryVel = new Vector2(Mathf.Clamp(carryVx, -3.5f, 3.5f), 0f);

                float targetFeetY = bestPt.y + tapeThickness * 0.5f;
                float penetration = targetFeetY - pFeet.y;

                Vector3 vel = rb.linearVelocity;

                // A) Bounded vertical support, mirroring the verified left span. The old right-span code
                // used an unbounded Mathf.Max(0, nodeVel.y) plus up to 9.5 units of penetration lift,
                // which launched the player off the hammock (and off the reel) and left them hovering in
                // the air - the reported "浮空". Clamp the rope velocity and the penetration correction
                // exactly like the left span does.
                float ropeVy = Mathf.Clamp(bestNodeVel.y, -1.5f, 0.55f);
                float maxSupportVy = ropeVy + Mathf.Clamp(penetration * 5f, 0f, 0.65f);

                Vector2 pCenter = (Vector2)transform.InverseTransformPoint(player.transform.position);
                bool clearOfRightPack = Vector2.Distance(pCenter, hr) > rR + player.CapsuleRadius + 0.01f;

                // B) Continuous support while already on the tape. The downward window matches
                // topSnapMargin, so a player who ends up slightly above the strip is pulled back down
                // instead of hovering in an "on tape but unsupported" state. vY <= 1.5 still lets a
                // jump (jumpSpeed 4.8) leave the tape cleanly.
                if (penetration > 0f || (wasOnTape && clearOfRightPack &&
                                          penetration > -0.70f && vel.y <= 1.5f))
                {
                    // Keep the rb velocity in line with the bounded support target in both directions.
                    // The position snap does the real carrying; this only stops a stale upward velocity
                    // from "popping" the player off the strip when they leave it. A jump is unaffected:
                    // playerSupportedOnRight can never be true while vY > 3.8 (see descendingOrRestingR).
                    if (vel.y < maxSupportVy)
                        vel.y = maxSupportVy;
                    else if (vel.y > maxSupportVy + 0.05f)
                        vel.y = maxSupportVy;

                    if (clearOfRightPack)
                    {
                        // Snap both ways (up out of the strip, down onto it while it sinks) so the
                        // player rides the hammock instead of floating above it.
                        rb.position += transform.TransformVector(new Vector3(0f, penetration, 0f));
                    }
                    else if (penetration > 0.015f)
                    {
                        // Capsule still overlapping the reel pack: only step up gently, exactly like
                        // the left span's clearOfLeftPack guard.
                        float stepUp = Mathf.Min(penetration * 0.45f, 0.04f);
                        rb.position += transform.TransformVector(new Vector3(0f, stepUp, 0f));
                    }
                }

                // Guide the player towards the right reel deck once the take-up conveyor has carried
                // them to the hub end. The wound-tape arc handles the last stretch; the conveyor lift
                // below keeps the take-up moving them along that arc too.
                if (wasOnTape && !boostRightReel)
                {
                    float normR = bestIsWrap ? 1f : (bestIdxR + bestTR) / Mathf.Max(1f, nR - 1f);

                    // Arrived on top of the right reel: stop the conveyor there so the player settles
                    // on the reel deck in front of the ladder instead of being thrown off the reel's
                    // left side by the horizontal carry. The player keeps full control afterwards.
                    bool arrivedOnReelDeck = bestIsWrap && pFeet.x <= hr.x + 0.10f;
                    if (arrivedOnReelDeck)
                    {
                        _lastRightRopeCarryVel = Vector2.zero;
                        vel.x = Mathf.MoveTowards(vel.x, 0f, 8f * dt);
                    }
                    else if (normR >= 0.78f)
                    {
                        // Conveyor climb: the bare horizontal carry is blocked by the right reel pack
                        // collider on the last (steep) stretch of the span, which left the player stuck
                        // against the side of the reel instead of being deposited on top of it. Add the
                        // belt's upward tangential velocity over that last stretch so the take-up really
                        // does carry them over the tangent point and around the wound tape on the reel.
                        // The value is bounded (2.4 max) so it cannot reproduce the old unbounded
                        // launch/float bug.
                        float conveyorVy = Mathf.Clamp(bestTangent.y * rightCarrySpeed * 0.95f, 0f, 2.4f);
                        if (vel.y < conveyorVy)
                            vel.y = conveyorVy;
                    }

                    if (!bestIsWrap && normR >= 0.78f && vel.x > -2.2f)
                        vel.x -= 4.5f * dt;
                }

                rb.linearVelocity = vel;
            }
            else
            {
                _lastRopeCarryVel = Vector2.zero;
                _lastRightRopeCarryVel = Vector2.zero;
            }
        }
    }

    // ------------------------------------------------------------------------------------------

    public void Rebuild()
    {
        if (!leftHub || !rightHub || !leftRoller || !rightRoller) return;
        EnsureMesh();

        float rL = PackRadius(fractionOnLeft);
        float rR = PackRadius(1f - fractionOnLeft);
        UpdatePack(leftPack, leftHub, rL);
        UpdatePack(rightPack, rightHub, rR);

        GetLeftSpanEndpoints(out var a0, out var a1, out var hl, out _, out var cl, out var rr);
        GetRightSpanEndpoints(out var b0, out var b1, out var hr, out _, out var cr, out _);
        EnsureRope(a0, a1);
        EnsureRightRope(b0, b1);

        var pts = new List<Vector2>(leftSpanNodes + rightSpanNodes + arcSegments * 2 + 16);

        // 0. Tuck start of tape smoothly inside leftPack along a short 12-deg lead-in arc so there is zero step at a0
        float rLFlush = Mathf.Max(0.1f, rL - tapeThickness * 0.5f);
        float a0Deg = Angle(hl, a0);
        const int leadSteps = 4;
        for (int i = leadSteps; i >= 1; i--)
        {
            float frac = i / (float)leadSteps;
            float ang = (a0Deg - frac * 12f) * Mathf.Deg2Rad;
            float rIn = rLFlush - frac * frac * tapeThickness * 0.8f;
            pts.Add(hl + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rIn);
        }

        // 1. Left span: evaluate Bezier hammock in Edit Mode or use dynamic Verlet rope in Play Mode
        if (!Application.isPlaying)
        {
            for (int i = 0; i < leftSpanNodes; i++)
            {
                float t = i / (float)(leftSpanNodes - 1);
                pts.Add(EvaluateHammockRestPoint(t, a0, a1));
            }
        }
        else
        {
            for (int i = 0; i < _ropePos.Length; i++)
                pts.Add(_ropePos[i]);
        }

        // 2. Wrap left roller CCW to bottom (270 deg)
        AddArc(pts, cl, rr, Angle(cl, a1), 270f, skipFirst: true);

        // 3. Bottom run straight to right roller bottom, then wrap CCW up to b0
        float endAngle = Angle(cr, b0);
        if (endAngle < 270f) endAngle += 360f;
        AddArc(pts, cr, rr, 270f, endAngle, skipFirst: false);

        // 4. Right span: evaluate Bezier hammock in Edit Mode or use dynamic Verlet rope in Play Mode
        if (!Application.isPlaying)
        {
            for (int i = 1; i < rightSpanNodes; i++)
            {
                float t = i / (float)(rightSpanNodes - 1);
                pts.Add(EvaluateRightHammockRestPoint(t, b0, b1));
            }
        }
        else
        {
            for (int i = 1; i < _rightRopePos.Length; i++)
                pts.Add(_rightRopePos[i]);
        }

        // 5. Tuck end of tape smoothly inside rightPack along a short 12-deg lead-out arc
        float rRFlush = Mathf.Max(0.1f, rR - tapeThickness * 0.5f);
        float b1Deg = Angle(hr, b1);
        for (int i = 1; i <= leadSteps; i++)
        {
            float frac = i / (float)leadSteps;
            float ang = (b1Deg + frac * 12f) * Mathf.Deg2Rad;
            float rIn = rRFlush - frac * frac * tapeThickness * 0.8f;
            pts.Add(hr + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rIn);
        }

        BuildStrip(pts);
    }

    void UpdatePack(Transform pack, Transform hub, float radius)
    {
        if (!pack) return;
        pack.position = hub.position;
        pack.localScale = new Vector3(radius * 2f, tapeWidth * 0.5f, radius * 2f);
        pack.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    Vector2 ToLocal(Vector3 world) => (Vector2)transform.InverseTransformPoint(world);

    static float Angle(Vector2 c, Vector2 p)
    {
        float a = Mathf.Atan2(p.y - c.y, p.x - c.x) * Mathf.Rad2Deg;
        return a < 0f ? a + 360f : a;
    }

    void AddArc(List<Vector2> pts, Vector2 c, float r, float fromDeg, float toDeg, bool skipFirst)
    {
        if (toDeg < fromDeg) toDeg += 360f;
        int n = Mathf.Max(2, Mathf.CeilToInt(arcSegments * (toDeg - fromDeg) / 90f));
        int start = skipFirst ? 1 : 0;
        for (int i = start; i <= n; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)n) * Mathf.Deg2Rad;
            pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
    }

    static void Tangent(Vector2 c1, float r1, Vector2 c2, float r2, bool leftSide, out Vector2 p1, out Vector2 p2)
    {
        Vector2 d = c2 - c1;
        float dist = d.magnitude;
        Vector2 u = d / dist;
        float k = Mathf.Clamp((r1 - r2) / dist, -1f, 1f);
        float h = Mathf.Sqrt(1f - k * k);
        Vector2 perp = new Vector2(-u.y, u.x);

        Vector2 nA = u * k + perp * h;
        Vector2 nB = u * k - perp * h;
        Vector2 n = (leftSide ? nA.x < nB.x : nA.x > nB.x) ? nA : nB;

        p1 = c1 + n * r1;
        p2 = c2 + n * r2;
    }

    // ------------------------------------------------------------------------------------------

    void BuildStrip(List<Vector2> pts)
    {
        int n = pts.Count;
        if (n < 2) return;

        var verts = new Vector3[n * 8];
        var norms = new Vector3[n * 8];
        var tris = new int[(n - 1) * 24];
        float h = tapeThickness * 0.5f;
        float zf = -tapeWidth * 0.5f, zb = tapeWidth * 0.5f;

        for (int i = 0; i < n; i++)
        {
            Vector2 dir = i == 0 ? pts[1] - pts[0]
                : i == n - 1 ? pts[n - 1] - pts[n - 2]
                : (pts[i + 1] - pts[i - 1]);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector2.down;
            else dir.Normalize();
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            Vector3 o = pts[i] + nrm * h, inn = pts[i] - nrm * h;
            Vector3 n3 = new Vector3(nrm.x, nrm.y, 0f);

            int b = i * 8;
            verts[b + 0] = new Vector3(o.x, o.y, zf); norms[b + 0] = Vector3.back;
            verts[b + 1] = new Vector3(inn.x, inn.y, zf); norms[b + 1] = Vector3.back;
            verts[b + 2] = new Vector3(o.x, o.y, zb); norms[b + 2] = Vector3.forward;
            verts[b + 3] = new Vector3(inn.x, inn.y, zb); norms[b + 3] = Vector3.forward;
            verts[b + 4] = new Vector3(o.x, o.y, zf); norms[b + 4] = n3;
            verts[b + 5] = new Vector3(o.x, o.y, zb); norms[b + 5] = n3;
            verts[b + 6] = new Vector3(inn.x, inn.y, zf); norms[b + 6] = -n3;
            verts[b + 7] = new Vector3(inn.x, inn.y, zb); norms[b + 7] = -n3;
        }

        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 8, c = (i + 1) * 8;
            t = Quad(tris, t, a + 0, c + 0, c + 1, a + 1);
            t = Quad(tris, t, a + 3, c + 3, c + 2, a + 2);
            t = Quad(tris, t, a + 5, c + 5, c + 4, a + 4);
            t = Quad(tris, t, a + 6, c + 6, c + 7, a + 7);
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.normals = norms;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }

    static int Quad(int[] tris, int t, int a, int b, int c, int d)
    {
        tris[t++] = a; tris[t++] = b; tris[t++] = c;
        tris[t++] = a; tris[t++] = c; tris[t++] = d;
        return t;
    }

    void EnsureMesh()
    {
        var mf = GetComponent<MeshFilter>();
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "CassetteTapeStrip", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
        }
        mf.sharedMesh = _mesh;

        var mr = GetComponent<MeshRenderer>();
        if (_material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _material = new Material(shader) { name = "CassetteTape", hideFlags = HideFlags.DontSave };
            if (_material.HasProperty("_Cull")) _material.SetFloat("_Cull", 0f);
        }
        _material.color = tapeColor;
        if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", tapeColor);
        mr.sharedMaterial = _material;
        if (leftPack) SetPackColor(leftPack);
        if (rightPack) SetPackColor(rightPack);
    }

    void SetPackColor(Transform pack)
    {
        var r = pack.GetComponent<Renderer>();
        if (r) r.sharedMaterial = _material;
    }

    static void DestroySafe(Object o)
    {
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
