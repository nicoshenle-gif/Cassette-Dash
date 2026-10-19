using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ending sequence for the roof-train escape:
///   1. The player presses F near the train and boards it (the character model is hidden
///      by CassettePlayer.BoardTrain, so the player is literally "inside" the train).
///   2. The train accelerates along the roof rails for a few seconds.
///   3. Once it has driven away, the screen fades to black and the GAME OVER / STAGE CLEAR
///      screen is shown with the run stats and a restart prompt.
///
/// This component is added automatically to the runtime-built "Roof_Train_System" by
/// CassetteRoofTrain.SetupComponents(), so no scene or generator change is required.
/// </summary>
[DefaultExecutionOrder(30000)]
public class CassetteEndingSequence : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds after the train starts departing before the fade-out begins.")]
    public float rideAwayTime = 3.2f;
    [Tooltip("Duration of the black fade-out.")]
    public float fadeDuration = 1.3f;
    [Tooltip("Delay between the fade finishing and the title panel fading in.")]
    public float panelFadeDelay = 0.2f;
    [Tooltip("Duration of the title panel fade-in.")]
    public float panelFadeDuration = 0.45f;

    [Header("References (auto-resolved)")]
    public CassetteRoofTrain train;
    public CassettePlayer player;

    [Header("State (read-only)")]
    public bool isEnding;
    public bool isEndScreen;
    public float endingElapsed;

    /// <summary>Level-clear time frozen at the moment the player boards/departs (seconds).</summary>
    public float finishElapsed => _finishElapsed;
    float _finishElapsed = -1f;

    /// <summary>True from the moment the fade-out starts: the in-game HUD is suppressed.</summary>
    public static bool EndingActive { get; private set; }

    enum Phase { Idle, RideAway, FadeOut, EndScreen }

    Phase _phase = Phase.Idle;
    float _phaseStart;
    float _runStart;
    float _panelStart;
    bool _sceneReloading;

    static Texture2D _white;

    void Awake()
    {
        EndingActive = false;
        isEnding = false;
        isEndScreen = false;
        _phase = Phase.Idle;
        _runStart = Time.time;
        if (!train) train = GetComponent<CassetteRoofTrain>();
    }

    void Start()
    {
        if (!train) train = FindAnyObjectByType<CassetteRoofTrain>();
        if (!player) player = FindAnyObjectByType<CassettePlayer>();
    }

    void Update()
    {
        if (train == null)
        {
            train = FindAnyObjectByType<CassetteRoofTrain>();
            if (train == null) return;
        }

        switch (_phase)
        {
            case Phase.Idle:
                if (train.isDeparting)
                {
                    _phase = Phase.RideAway;
                    _phaseStart = Time.time;
                    // Freeze the clear time at the boarding moment: it must not keep counting up
                    // during the departure cinematic or on the ending screen.
                    _finishElapsed = Mathf.Max(0f, Time.time - _runStart);
                    if (!player) player = FindAnyObjectByType<CassettePlayer>();
                }
                break;

            case Phase.RideAway:
                endingElapsed = Time.time - _phaseStart;
                if (endingElapsed >= rideAwayTime)
                {
                    _phase = Phase.FadeOut;
                    _phaseStart = Time.time;
                    EndingActive = true;
                }
                break;

            case Phase.FadeOut:
                if (Time.time - _phaseStart >= fadeDuration)
                {
                    _phase = Phase.EndScreen;
                    _phaseStart = Time.time;
                    _panelStart = Time.time + Mathf.Max(0f, panelFadeDelay);
                    isEndScreen = true;
                }
                break;

            case Phase.EndScreen:
                if (!_sceneReloading && Time.time >= _panelStart + 0.45f && RestartPressed())
                    RestartGame();
                break;
        }

        isEnding = _phase != Phase.Idle;
    }

    bool _restartKeyLatch;

    bool RestartPressed()
    {
        bool pressed;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        pressed = kb != null && (kb.rKey.isPressed || kb.spaceKey.isPressed);
#else
        pressed = Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.Space);
#endif
        // Poll the key level with a rising-edge latch instead of wasPressedThisFrame: this is robust
        // even if the press is delivered during a frame where the game loop was throttled/unfocused.
        if (pressed && !_restartKeyLatch)
        {
            _restartKeyLatch = true;
            return true;
        }
        if (!pressed) _restartKeyLatch = false;
        return false;
    }

    /// <summary>Reloads the active scene, which rebuilds the whole cassette level from scratch.</summary>
    public void RestartGame()
    {
        if (_sceneReloading) return;
        _sceneReloading = true;
        EndingActive = false;
        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            SceneManager.LoadScene(scene.buildIndex);
        else if (!string.IsNullOrEmpty(scene.name))
            SceneManager.LoadScene(scene.name);
    }

    // ------------------------------------------------------------------------------------------
    // IMGUI ending screen
    // ------------------------------------------------------------------------------------------

    static Texture2D GetWhiteTex()
    {
        if (_white == null)
        {
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
        }
        return _white;
    }

    static void DrawRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, GetWhiteTex());
        GUI.color = prev;
    }

    void OnGUI()
    {
        if (_phase == Phase.Idle) return;
        if (player == null) player = FindAnyObjectByType<CassettePlayer>();

        // ---- Full-screen fade ----
        float blackAlpha = 0f;
        if (_phase == Phase.FadeOut)
            blackAlpha = Mathf.Clamp01((Time.time - _phaseStart) / Mathf.Max(0.01f, fadeDuration));
        else if (_phase == Phase.EndScreen)
            blackAlpha = 1f;

        if (blackAlpha > 0.001f)
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.015f, 0.015f, 0.022f, blackAlpha));

        // ---- GAME OVER / STAGE CLEAR panel ----
        if (_phase != Phase.EndScreen) return;

        float panelAlpha = Mathf.Clamp01((Time.time - _panelStart) / Mathf.Max(0.01f, panelFadeDuration));
        if (panelAlpha <= 0.001f) return;

        float w = Screen.width;
        float h = Screen.height;
        float cx = w * 0.5f;

        // Soft dark card behind the text
        float cardW = Mathf.Min(w * 0.72f, 900f);
        float cardH = Mathf.Min(h * 0.62f, 530f);
        Rect card = new Rect(cx - cardW * 0.5f, h * 0.5f - cardH * 0.5f, cardW, cardH);
        DrawRect(card, new Color(0.055f, 0.06f, 0.085f, 0.92f * panelAlpha));
        DrawRect(new Rect(card.x, card.y, card.width, 3f),
            new Color(0.96f, 0.80f, 0.22f, 0.95f * panelAlpha));
        DrawRect(new Rect(card.x, card.yMax - 3f, card.width, 3f),
            new Color(0.96f, 0.80f, 0.22f, 0.55f * panelAlpha));

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, panelAlpha);

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.075f, 34f, 72f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.86f, 0.28f);

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.030f, 15f, 26f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(0.95f, 0.96f, 1f);

        var statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.024f, 13f, 21f)),
            alignment = TextAnchor.MiddleCenter
        };
        statStyle.normal.textColor = new Color(0.72f, 0.76f, 0.86f);

        var promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.028f, 15f, 24f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        float pulse = 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f));
        promptStyle.normal.textColor = new Color(1f, 0.86f, 0.28f, pulse);

        GUI.Label(new Rect(card.x, card.y + card.height * 0.17f, card.width, titleStyle.fontSize + 14f),
            CassetteLocalization.T("end.title"), titleStyle);
        GUI.Label(new Rect(card.x, card.y + card.height * 0.17f + titleStyle.fontSize + 22f, card.width, 34f),
            CassetteLocalization.T("end.subtitle"), subStyle);

        float shownElapsed = _finishElapsed >= 0f ? _finishElapsed : Mathf.Max(0f, Time.time - _runStart);
        int elapsed = Mathf.Max(0, Mathf.FloorToInt(shownElapsed));
        string timeText = string.Format("{0:00}:{1:00}", elapsed / 60, elapsed % 60);
        string hpText = (player != null)
            ? Mathf.CeilToInt(player.currentHealth) + " / " + Mathf.CeilToInt(player.maxHealth)
            : "-";
        GUI.Label(new Rect(card.x, card.y + card.height * 0.53f, card.width, 30f),
            CassetteLocalization.T("end.stats", timeText, hpText), statStyle);

        GUI.Label(new Rect(card.x, card.yMax - 78f, card.width, 32f),
            CassetteLocalization.T("end.restart"), promptStyle);

        GUI.color = prevColor;
    }
}
