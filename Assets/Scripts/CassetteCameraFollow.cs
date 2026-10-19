using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Smoothly follows the player inside the cassette level and supports mouse-wheel zoom.
/// </summary>
public class CassetteCameraFollow : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform target;

    [Header("Framing & Zoom")]
    [Tooltip("Camera distance along -Z from the player. Smaller = closer/larger view (15.5 = full cassette).")]
    [Range(2.2f, 17f)]
    public float distance = 6.8f;
    public float minDistance = 2.5f;
    public float maxDistance = 16.0f;
    public float zoomScrollSensitivity = 1.8f;
    public Vector2 planarOffset = new Vector2(0f, -0.45f);

    [Header("Mouse Look-Ahead (Peek Terrain)")]
    [Tooltip("Move the mouse toward screen edges (e.g., downward) to pan the camera and inspect surrounding terrain.")]
    public bool enableMouseLook = true;
    [Tooltip("Maximum world-unit offset the camera shifts when the mouse moves toward screen edges.")]
    public Vector2 maxMouseLookOffset = new Vector2(4.2f, 4.0f);
    [Tooltip("Normalized center deadzone (0..0.5) where the camera stays centered on the player.")]
    [Range(0f, 0.4f)]
    public float mouseDeadZone = 0.12f;
    [Range(2f, 20f)]
    public float mouseLookSmoothing = 8f;

    [Header("Right-Click Rotation Control (俯仰角 & 左右偏航角)")]
    [Tooltip("Hold Right Mouse Button and move mouse to rotate camera pitch (up/down) and yaw (left/right).")]
    public bool enableRightClickPitch = false;
    public float pitchSensitivity = 0.25f;
    public float yawSensitivity = 0.25f;
    public float minPitch = -35f;
    public float maxPitch = 45f;
    public float minYaw = -45f;
    public float maxYaw = 45f;
    [Tooltip("If enabled, camera angle returns to 0 when the right mouse button is released. If false, it keeps your chosen angle (Middle-Click to reset).")]
    public bool resetPitchOnRelease = false;

    [Header("Smoothing")]
    [Range(0.01f, 0.5f)]
    public float smoothTime = 0.14f;

    [Header("Cassette Bounds Clamping")]
    public bool clampToCassette = true;
    public Vector2 cassetteHalfSize = new Vector2(8.5f, 5.4f);

    Vector3 _velocity;
    Vector2 _currentMouseOffset;
    float _targetPitch;
    float _currentPitch;
    float _targetYaw;
    float _currentYaw;
    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (!target)
        {
            var p = FindFirstObjectByType<CassettePlayer>();
            if (p) target = p.transform;
        }
    }

    void LateUpdate()
    {
        if (!target)
        {
            var p = FindFirstObjectByType<CassettePlayer>();
            if (p) target = p.transform;
            if (!target) return;
        }

        // Mouse wheel zoom
        float scroll = ReadScroll();
        if (Mathf.Abs(scroll) > 0.001f)
            distance = Mathf.Clamp(distance - scroll * zoomScrollSensitivity, minDistance, maxDistance);

        // Right-click drag to adjust camera pitch (up/down) & yaw (left/right)
        bool rightHeld = false;
        if (enableRightClickPitch)
        {
            rightHeld = ReadRightMouseDelta(out float pitchDelta, out float yawDelta, out bool middleReset);
            if (middleReset)
            {
                _targetPitch = 0f;
                _targetYaw = 0f;
            }
            else if (rightHeld)
            {
                _targetPitch = Mathf.Clamp(_targetPitch + pitchDelta, minPitch, maxPitch);
                _targetYaw = Mathf.Clamp(_targetYaw + yawDelta, minYaw, maxYaw);
            }
            else if (resetPitchOnRelease)
            {
                _targetPitch = 0f;
                _targetYaw = 0f;
            }
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, Time.deltaTime * 12f);
            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, Time.deltaTime * 12f);
        }
        else
        {
            _targetPitch = 0f;
            _targetYaw = 0f;
            _currentPitch = 0f;
            _currentYaw = 0f;
        }

        // Mouse look-ahead / peek offset (paused while holding right-click to rotate angle)
        Vector2 targetMouseOffset = (enableMouseLook && !rightHeld) ? ReadMouseLookOffset() : _currentMouseOffset;
        _currentMouseOffset = Vector2.Lerp(_currentMouseOffset, targetMouseOffset, Time.deltaTime * mouseLookSmoothing);

        Vector3 desired = new Vector3(
            target.position.x + planarOffset.x + _currentMouseOffset.x,
            target.position.y + planarOffset.y + _currentMouseOffset.y,
            -distance);

        bool isDepartingOrRoof = false;
        var roofTrain = FindFirstObjectByType<CassetteRoofTrain>();
        if (roofTrain != null && (roofTrain.isDeparting || (roofTrain.player != null && roofTrain.player.isBoardedOnTrain)))
            isDepartingOrRoof = true;
        if (target != null && target.position.y > cassetteHalfSize.y - 0.5f)
            isDepartingOrRoof = true;

        if (clampToCassette && !isDepartingOrRoof && _cam != null)
        {
            float halfH = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            float halfW = halfH * _cam.aspect;

            float maxX = Mathf.Max(0f, cassetteHalfSize.x + 2.4f - halfW);
            float maxY = Mathf.Max(0f, cassetteHalfSize.y + 2.2f - halfH);

            desired.x = Mathf.Clamp(desired.x, -maxX, maxX);
            desired.y = Mathf.Clamp(desired.y, -maxY, maxY);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        transform.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
    }

    bool ReadRightMouseDelta(out float pitchDelta, out float yawDelta, out bool middleReset)
    {
        pitchDelta = 0f;
        yawDelta = 0f;
        middleReset = false;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;
        if (mouse.middleButton.wasPressedThisFrame)
            middleReset = true;
        if (mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            // Moving mouse down (d.y < 0) tilts camera downward (+pitch); moving right (d.x > 0) turns camera right (+yaw)
            pitchDelta = -d.y * pitchSensitivity;
            yawDelta = d.x * yawSensitivity;
            return true;
        }
        return false;
#else
        if (Input.GetMouseButtonDown(2))
            middleReset = true;
        if (Input.GetMouseButton(1))
        {
            float dx = Input.GetAxis("Mouse X") * 10f;
            float dy = Input.GetAxis("Mouse Y") * 10f;
            pitchDelta = -dy * pitchSensitivity;
            yawDelta = dx * yawSensitivity;
            return true;
        }
        return false;
#endif
    }

    Vector2 ReadMouseLookOffset()
    {
        if (Screen.width <= 1 || Screen.height <= 1) return Vector2.zero;

        Vector2 mousePos;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;
        mousePos = mouse.position.ReadValue();
#else
        mousePos = Input.mousePosition;
#endif
        // Normalized screen coordinates in [-1, 1] where (0, 0) is screen center
        float nx = (mousePos.x / Screen.width - 0.5f) * 2f;
        float ny = (mousePos.y / Screen.height - 0.5f) * 2f;

        // If cursor is outside the Game view window, return smoothly to center
        if (Mathf.Abs(nx) > 1.05f || Mathf.Abs(ny) > 1.05f)
            return Vector2.zero;

        nx = ApplyDeadZone(Mathf.Clamp(nx, -1f, 1f), mouseDeadZone);
        ny = ApplyDeadZone(Mathf.Clamp(ny, -1f, 1f), mouseDeadZone);

        return new Vector2(nx * maxMouseLookOffset.x, ny * maxMouseLookOffset.y);
    }

    static float ApplyDeadZone(float val, float dz)
    {
        float abs = Mathf.Abs(val);
        if (abs <= dz) return 0f;
        return Mathf.Sign(val) * ((abs - dz) / Mathf.Max(0.001f, 1f - dz));
    }

    static float ReadScroll()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float sy = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(sy) > 0.01f)
                return Mathf.Sign(sy) * 0.5f;
        }
#else
        float s = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(s) > 0.001f) return s * 5f;
#endif
        return 0f;
    }
}
