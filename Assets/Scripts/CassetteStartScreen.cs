using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Title / start screen shown when the level scene loads (and again after every restart):
///   • freezes the game (Time.timeScale = 0) so the player and the tape do not move,
///   • draws the "Cassette Dash" title, a small cassette illustration and the controls,
///   • offers a language selector with 5 mainstream languages (click a button or press 1-5),
///   • starts the run on any other key press / left click.
///
/// Auto-created by CassetteStartScreenBootstrap at play start and after every scene load,
/// so no scene or prefab wiring is needed.
/// </summary>
[DefaultExecutionOrder(-20000)]
public class CassetteStartScreen : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    [Tooltip("Ignore input for this long after the screen appears (prevents an accidental instant skip).")]
    public float inputDelay = 0.6f;

    float _readyAt;
    bool _inputLatch;
    bool _mouseLatch;
    bool _padLatch;
    int _digitLatch;

    static Texture2D _white;
    static Texture2D _circle;

    void Awake()
    {
        IsActive = true;
        _readyAt = Time.realtimeSinceStartup + Mathf.Max(0f, inputDelay);
        // Freeze the whole game while the title screen is up (physics, tape animation, camera).
        Time.timeScale = 0f;
    }

    void OnDestroy()
    {
        if (IsActive)
        {
            IsActive = false;
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        if (!IsActive) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            // ---- Language hotkeys: number row 1-5 ----
            var digitKeys = new[]
            {
                kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key
            };
            for (int i = 0; i < digitKeys.Length; i++)
            {
                if (digitKeys[i] != null && digitKeys[i].isPressed)
                {
                    if (_digitLatch != i + 1)
                    {
                        _digitLatch = i + 1;
                        SelectLanguage(i);
                    }
                    return; // a number key never starts the game
                }
            }
            _digitLatch = 0;

            // ---- Mouse: language buttons consume the click, anything else starts ----
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                if (!_mouseLatch)
                {
                    _mouseLatch = true;
                    if (Time.realtimeSinceStartup >= _readyAt)
                    {
                        Vector2 mp = mouse.position.ReadValue();
                        Vector2 guiPoint = new Vector2(mp.x, Screen.height - mp.y);
                        int hit = LanguageIndexAt(guiPoint);
                        if (hit >= 0) SelectLanguage(hit);
                        else StartGame();
                    }
                }
                return;
            }
            _mouseLatch = false;

            // ---- Any other key starts the run ----
            if (Time.realtimeSinceStartup < _readyAt) return;
            if (kb.anyKey.isPressed)
            {
                if (!_inputLatch)
                {
                    _inputLatch = true;
                    StartGame();
                }
                return;
            }
            _inputLatch = false;

            // ---- Gamepad ----
            var pad = Gamepad.current;
            if (pad != null && (pad.startButton.isPressed || pad.buttonSouth.isPressed))
            {
                if (!_padLatch)
                {
                    _padLatch = true;
                    StartGame();
                }
                return;
            }
            _padLatch = false;
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectLanguage(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectLanguage(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectLanguage(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectLanguage(3);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) SelectLanguage(4);
        else if (Time.realtimeSinceStartup >= _readyAt && (Input.anyKey || Input.GetMouseButton(0)))
        {
            if (!_inputLatch)
            {
                _inputLatch = true;
                StartGame();
            }
        }
        else if (!Input.anyKey && !Input.GetMouseButton(0))
        {
            _inputLatch = false;
        }
#endif
    }

    void SelectLanguage(int index)
    {
        var langs = CassetteLocalization.AllLanguages;
        if (index < 0 || index >= langs.Length) return;
        CassetteLocalization.Current = langs[index];
    }

    /// <summary>Starts the run (also callable from code/tests).</summary>
    public void StartGame()
    {
        if (!IsActive) return;
        IsActive = false;
        Time.timeScale = 1f;
        enabled = false;
    }

    // ------------------------------------------------------------------------------------------
    // Layout helpers (shared by Update hit-testing and OnGUI drawing)
    // ------------------------------------------------------------------------------------------

    public static Rect[] GetLanguageRects()
    {
        float w = Screen.width;
        float h = Screen.height;
        var rects = new Rect[CassetteLocalization.AllLanguages.Length];

        float bottomY = CassetteBottomY();
        float labelSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.024f, 13f, 22f));
        float rowY = bottomY + h * 0.02f + labelSize + h * 0.012f;
        float btnH = Mathf.Clamp(h * 0.052f, 30f, 54f);
        float btnW = Mathf.Clamp(w * 0.13f, 92f, 190f);
        float gap = Mathf.Clamp(w * 0.008f, 8f, 18f);
        float total = rects.Length * btnW + (rects.Length - 1) * gap;
        float x0 = (w - total) * 0.5f;

        for (int i = 0; i < rects.Length; i++)
            rects[i] = new Rect(x0 + i * (btnW + gap), rowY, btnW, btnH);
        return rects;
    }

    static float CassetteBottomY()
    {
        float w = Screen.width;
        float h = Screen.height;
        float cw = Mathf.Min(w * 0.52f, h * 0.62f);
        float ch = cw * 0.34f;
        return h * 0.30f + ch;
    }

    static int LanguageIndexAt(Vector2 guiPoint)
    {
        var rects = GetLanguageRects();
        for (int i = 0; i < rects.Length; i++)
            if (rects[i].Contains(guiPoint)) return i;
        return -1;
    }

    // ------------------------------------------------------------------------------------------
    // IMGUI title screen
    // ------------------------------------------------------------------------------------------

    static Texture2D GetWhiteTex()
    {
        if (_white == null)
        {
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
        }
        return _white;
    }

    static Texture2D GetCircleTex()
    {
        if (_circle != null) return _circle;
        const int size = 128;
        _circle = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        float r = size * 0.5f - 1f;
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 1f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        _circle.SetPixels32(px);
        _circle.Apply();
        return _circle;
    }

    static void DrawRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, GetWhiteTex());
        GUI.color = prev;
    }

    static void DrawCircle(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, GetCircleTex());
        GUI.color = prev;
    }

    static void DrawSpoke(Vector2 center, float length, float width, float angleDeg, Color c)
    {
        Matrix4x4 prev = GUI.matrix;
        GUIUtility.RotateAroundPivot(angleDeg, center);
        DrawRect(new Rect(center.x - width * 0.5f, center.y - length * 0.5f, width, length), c);
        GUI.matrix = prev;
    }

    static void DrawText(Rect r, string text, GUIStyle style, Color color, Vector2 shadowOffset)
    {
        var shadow = new GUIStyle(style);
        shadow.normal.textColor = new Color(0f, 0f, 0f, 0.55f * color.a);
        GUI.Label(new Rect(r.x + shadowOffset.x, r.y + shadowOffset.y, r.width, r.height), text, shadow);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    void OnGUI()
    {
        if (!IsActive) return;

        float w = Screen.width;
        float h = Screen.height;
        float cx = w * 0.5f;

        // ---- Backdrop (game world is frozen behind it) ----
        DrawRect(new Rect(0f, 0f, w, h), new Color(0.035f, 0.045f, 0.075f, 0.94f));
        DrawRect(new Rect(0f, 0f, w, h * 0.5f), new Color(0.02f, 0.025f, 0.045f, 0.35f));
        DrawRect(new Rect(0f, h * 0.86f, w, h * 0.14f), new Color(0f, 0f, 0f, 0.30f));

        // ---- Title (brand name, not translated) ----
        int titleSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.095f, 44f, 98f));
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = titleSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        var titleRect = new Rect(0f, h * 0.075f, w, titleSize + 20f);
        DrawText(titleRect, "Cassette Dash", titleStyle, new Color(1f, 0.84f, 0.24f), new Vector2(3f, 3f));

        int subSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.031f, 15f, 28f));
        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = subSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        var subRect = new Rect(0f, h * 0.075f + titleSize + 24f, w, subSize + 12f);
        DrawText(subRect, CassetteLocalization.T("start.subtitle"), subStyle, new Color(0.92f, 0.94f, 1f), new Vector2(2f, 2f));

        // ---- Cassette illustration ----
        float cw = Mathf.Min(w * 0.52f, h * 0.62f);
        float ch = cw * 0.34f;
        float bx = cx - cw * 0.5f;
        float by = h * 0.30f;

        DrawRect(new Rect(bx - 6f, by - 6f, cw + 12f, ch + 12f), new Color(0.98f, 0.82f, 0.22f, 0.85f));
        DrawRect(new Rect(bx, by, cw, ch), new Color(0.14f, 0.15f, 0.19f, 0.98f));
        DrawRect(new Rect(bx, by, cw, ch * 0.10f), new Color(0.95f, 0.77f, 0.20f, 0.95f));

        int labelSize = Mathf.RoundToInt(Mathf.Clamp(ch * 0.09f, 10f, 18f));
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = labelSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle.normal.textColor = new Color(0.09f, 0.09f, 0.11f);
        GUI.Label(new Rect(bx, by, cw, ch * 0.10f), "CASSETTE DASH   ·   C-60   ·   STEREO", labelStyle);

        float reelR = ch * 0.27f;
        Vector2 reelL = new Vector2(cx - cw * 0.20f, by + ch * 0.58f);
        Vector2 reelRc = new Vector2(cx + cw * 0.20f, by + ch * 0.58f);
        float spin = Time.unscaledTime * 55f;
        DrawReel(reelL, reelR, spin);
        DrawReel(reelRc, reelR, -spin * 1.15f);

        DrawRect(new Rect(bx + cw * 0.30f, by + ch * 0.88f, cw * 0.40f, ch * 0.055f),
            new Color(0.95f, 0.77f, 0.20f, 0.75f));

        // ---- Language selector ----
        var langRects = GetLanguageRects();
        int langLabelFont = Mathf.RoundToInt(Mathf.Clamp(h * 0.024f, 13f, 22f));
        var langLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = langLabelFont,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        langLabelStyle.normal.textColor = new Color(0.72f, 0.78f, 0.92f);
        GUI.Label(new Rect(0f, langRects[0].y - langLabelFont - h * 0.012f, w, langLabelFont + 8f),
            CassetteLocalization.T("start.language"), langLabelStyle);

        Vector2 mouseGui = Vector2.zero;
        bool hasMouse = false;
#if ENABLE_INPUT_SYSTEM
        var mouseNow = Mouse.current;
        if (mouseNow != null)
        {
            Vector2 mp = mouseNow.position.ReadValue();
            mouseGui = new Vector2(mp.x, Screen.height - mp.y);
            hasMouse = true;
        }
#else
        mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        hasMouse = true;
#endif

        int langFont = Mathf.RoundToInt(Mathf.Clamp(h * 0.022f, 12f, 20f));
        var langStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = langFont,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        var langs = CassetteLocalization.AllLanguages;
        for (int i = 0; i < langRects.Length && i < langs.Length; i++)
        {
            bool selected = CassetteLocalization.Current == langs[i];
            bool hovered = hasMouse && langRects[i].Contains(mouseGui);
            DrawRect(langRects[i], selected
                ? new Color(0.98f, 0.80f, 0.22f, 0.96f)
                : (hovered ? new Color(0.24f, 0.28f, 0.38f, 0.95f) : new Color(0.13f, 0.15f, 0.20f, 0.92f)));
            DrawRect(new Rect(langRects[i].x, langRects[i].y, langRects[i].width, 2f),
                selected ? new Color(1f, 0.95f, 0.65f, 1f) : new Color(0.55f, 0.60f, 0.72f, 0.6f));
            DrawRect(new Rect(langRects[i].x, langRects[i].yMax - 2f, langRects[i].width, 2f),
                selected ? new Color(1f, 0.95f, 0.65f, 1f) : new Color(0.55f, 0.60f, 0.72f, 0.6f));
            langStyle.normal.textColor = selected ? new Color(0.08f, 0.08f, 0.10f) : new Color(0.88f, 0.90f, 0.96f);
            GUI.Label(langRects[i], CassetteLocalization.NativeName(langs[i]), langStyle);
        }

        int hintSizeSmall = Mathf.RoundToInt(Mathf.Clamp(h * 0.019f, 11f, 17f));
        var langHintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = hintSizeSmall,
            alignment = TextAnchor.MiddleCenter
        };
        langHintStyle.normal.textColor = new Color(0.55f, 0.60f, 0.72f);
        GUI.Label(new Rect(0f, langRects[0].yMax + h * 0.008f, w, hintSizeSmall + 8f),
            CassetteLocalization.T("start.languageHint"), langHintStyle);

        // ---- Start prompt ----
        float promptY = langRects[0].yMax + h * 0.008f + hintSizeSmall + h * 0.038f;
        float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.6f));
        int promptSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.034f, 18f, 34f));
        var promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = promptSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        DrawText(new Rect(0f, promptY, w, promptSize + 12f),
            CassetteLocalization.T("start.pressAnyKey"), promptStyle,
            new Color(1f, 0.86f, 0.28f, pulse), new Vector2(2f, 2f));

        // ---- Controls ----
        int hintSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.021f, 12f, 20f));
        var hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = hintSize,
            alignment = TextAnchor.MiddleCenter
        };
        hintStyle.normal.textColor = new Color(0.70f, 0.75f, 0.86f);
        GUI.Label(new Rect(0f, promptY + promptSize + h * 0.025f, w, hintSize + 12f),
            CassetteLocalization.T("start.controls"), hintStyle);

        var footerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.018f, 10f, 16f)),
            alignment = TextAnchor.MiddleRight
        };
        footerStyle.normal.textColor = new Color(0.55f, 0.60f, 0.72f);
        GUI.Label(new Rect(0f, h - 32f, w - 18f, 24f), "Cassette Dash  ·  v1.0", footerStyle);
    }

    void DrawReel(Vector2 center, float radius, float angleDeg)
    {
        Rect outer = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        DrawCircle(outer, new Color(0.88f, 0.90f, 0.95f, 0.96f));
        DrawCircle(new Rect(center.x - radius * 0.72f, center.y - radius * 0.72f, radius * 1.44f, radius * 1.44f),
            new Color(0.20f, 0.22f, 0.27f, 1f));
        DrawCircle(new Rect(center.x - radius * 0.30f, center.y - radius * 0.30f, radius * 0.60f, radius * 0.60f),
            new Color(0.95f, 0.77f, 0.20f, 0.95f));
        DrawCircle(new Rect(center.x - radius * 0.12f, center.y - radius * 0.12f, radius * 0.24f, radius * 0.24f),
            new Color(0.09f, 0.09f, 0.12f, 1f));
        // Two spokes so the reels visibly spin while the title screen is up (unscaled time).
        DrawSpoke(center, radius * 1.45f, Mathf.Max(2f, radius * 0.07f), angleDeg,
            new Color(0.96f, 0.80f, 0.24f, 0.9f));
        DrawSpoke(center, radius * 1.45f, Mathf.Max(2f, radius * 0.07f), angleDeg + 90f,
            new Color(0.96f, 0.80f, 0.24f, 0.55f));
    }
}

/// <summary>
/// Creates the title screen at play start and re-creates it after every scene load (e.g. the
/// restart performed by the ending screen), without any scene or prefab wiring.
/// </summary>
public static class CassetteStartScreenBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();
    }

    static void Ensure()
    {
        if (Object.FindAnyObjectByType<CassetteStartScreen>() != null) return;
        var go = new GameObject("Cassette_StartScreen");
        go.AddComponent<CassetteStartScreen>();
    }
}