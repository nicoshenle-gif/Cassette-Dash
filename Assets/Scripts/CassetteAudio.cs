using System.Collections.Generic;
using UnityEngine;

/// <summary>All sound effects used by the game (clips live in Assets/Resources/Audio/SFX).</summary>
public enum CassetteSfx
{
    /// <summary>脚步 · 磁带薄膜 (SFX_01)</summary>
    FootstepTape,
    /// <summary>脚步 · 塑料壳 (SFX_04)</summary>
    FootstepShell,
    /// <summary>起跳 (SFX_07)</summary>
    Jump,
    /// <summary>重摔落地 (SFX_08)</summary>
    HardLand,
    /// <summary>受击 (SFX_09)</summary>
    Hurt,
    /// <summary>击倒死亡 (SFX_12)</summary>
    Death,
    /// <summary>复活 (SFX_13)</summary>
    Respawn,
    /// <summary>出带加速 · 循环 (SFX_14)</summary>
    TapeFeedLoop,
    /// <summary>磁头下砸 (SFX_15)</summary>
    HeadSlam
}

/// <summary>
/// Tiny runtime audio service: loads the SFX clips from Resources once, plays 2D one-shots and
/// positional one-shots, and drives the looping tape-feed sound with a short fade.
/// Everything is created on demand, so no scene / prefab wiring is required.
/// </summary>
public static class CassetteAudio
{
    public const string ResourceFolder = "Audio/SFX/";

    /// <summary>Master switch (kept for a future options menu).</summary>
    public static bool Enabled = true;

    /// <summary>
    /// Global SFX volume applied on top of every per-sound volume below.
    /// Was 0.6 after the first balance pass reported the effects as too loud – lower it further
    /// (e.g. 0.35f) or raise it towards 1f to taste.
    /// </summary>
    public static float MasterVolume = 0.6f;

    // Original file names (in Downloads) -> the asset name used inside Resources:
    //   SFX_01_脚步-磁带薄膜-1.wav  -> sfx_01_footstep_tape
    //   SFX_04_脚步-塑料壳-1.wav    -> sfx_04_footstep_shell
    //   SFX_07_起跳.wav             -> sfx_07_jump
    //   SFX_08_重摔落地.wav         -> sfx_08_hard_land
    //   SFX_09_受击-1.wav           -> sfx_09_hurt
    //   SFX_12_击倒死亡.wav         -> sfx_12_death
    //   SFX_13_复活.wav             -> sfx_13_respawn
    //   SFX_14_出带加速-循环.wav     -> sfx_14_tape_feed_loop
    //   SFX_15_磁头下砸.wav         -> sfx_15_head_slam
    //   (SFX_16_尖刺预警 / SFX_17_尖刺弹出 / SFX_18_投放器警戒 were removed again on request)
    static readonly Dictionary<CassetteSfx, string> ClipAssetNames = new Dictionary<CassetteSfx, string>
    {
        { CassetteSfx.FootstepTape,  "sfx_01_footstep_tape" },
        { CassetteSfx.FootstepShell, "sfx_04_footstep_shell" },
        { CassetteSfx.Jump,          "sfx_07_jump" },
        { CassetteSfx.HardLand,      "sfx_08_hard_land" },
        { CassetteSfx.Hurt,          "sfx_09_hurt" },
        { CassetteSfx.Death,         "sfx_12_death" },
        { CassetteSfx.Respawn,       "sfx_13_respawn" },
        { CassetteSfx.TapeFeedLoop,  "sfx_14_tape_feed_loop" },
        { CassetteSfx.HeadSlam,      "sfx_15_head_slam" }
    };

    static readonly Dictionary<CassetteSfx, float> BaseVolumes = new Dictionary<CassetteSfx, float>
    {
        { CassetteSfx.FootstepTape,  0.20f },
        { CassetteSfx.FootstepShell, 0.22f },
        { CassetteSfx.Jump,          0.32f },
        { CassetteSfx.HardLand,      0.40f },
        { CassetteSfx.Hurt,          0.34f },
        { CassetteSfx.Death,         0.42f },
        { CassetteSfx.Respawn,       0.32f },
        { CassetteSfx.TapeFeedLoop,  0.22f },
        { CassetteSfx.HeadSlam,      0.40f }
    };

    const int OneShotVoices = 10;

    static readonly Dictionary<CassetteSfx, AudioClip> Clips = new Dictionary<CassetteSfx, AudioClip>();
    static readonly HashSet<CassetteSfx> WarnedMissing = new HashSet<CassetteSfx>();

    static GameObject _root;
    static AudioSource[] _voices;
    static int _voiceCursor;
    static CassetteAudioHost _host;
    static AudioSource _loop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureReady();
    }

    static void EnsureReady()
    {
        if (_root != null) return;

        _root = new GameObject("Cassette_Audio");
        if (Application.isPlaying) Object.DontDestroyOnLoad(_root);

        _voices = new AudioSource[OneShotVoices];
        for (int i = 0; i < _voices.Length; i++)
        {
            var src = _root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = 1f;
            _voices[i] = src;
        }

        _loop = _root.AddComponent<AudioSource>();
        _loop.playOnAwake = false;
        _loop.loop = true;
        _loop.spatialBlend = 0f;
        _loop.volume = 0f;

        _host = _root.AddComponent<CassetteAudioHost>();
        _host.loopSource = _loop;
    }

    /// <summary>Loads (and caches) the AudioClip for a sound effect; null when the asset is missing.</summary>
    public static AudioClip GetClip(CassetteSfx sfx)
    {
        AudioClip clip;
        if (Clips.TryGetValue(sfx, out clip)) return clip;

        string assetName;
        if (!ClipAssetNames.TryGetValue(sfx, out assetName)) assetName = sfx.ToString();

        clip = Resources.Load<AudioClip>(ResourceFolder + assetName);
        Clips[sfx] = clip;

        if (clip == null && WarnedMissing.Add(sfx))
            Debug.LogWarning("[CassetteAudio] missing SFX clip: Resources/" + ResourceFolder + assetName);

        return clip;
    }

    /// <summary>Clip duration in seconds (0 when missing). Handy to time warning sounds with telegraphs.</summary>
    public static float ClipLength(CassetteSfx sfx)
    {
        var clip = GetClip(sfx);
        return clip != null ? clip.length : 0f;
    }

    public static float BaseVolume(CassetteSfx sfx)
    {
        float v;
        return BaseVolumes.TryGetValue(sfx, out v) ? v : 1f;
    }

    /// <summary>Effective playback volume (per-sound × master).</summary>
    public static float EffectiveVolume(CassetteSfx sfx)
    {
        return Mathf.Clamp01(BaseVolume(sfx) * MasterVolume);
    }

    /// <summary>Sets the master SFX volume (0..1) – handy for an options slider later.</summary>
    public static void SetMasterVolume(float volume01)
    {
        MasterVolume = Mathf.Clamp01(volume01);
        if (_host != null) _host.SetLoopTarget(EffectiveVolume(CassetteSfx.TapeFeedLoop));
    }

    /// <summary>Non-positional one-shot (player / UI / feedback sounds).</summary>
    public static void Play(CassetteSfx sfx, float volumeScale = 1f, float pitch = 1f)
    {
        if (!Enabled) return;
        var clip = GetClip(sfx);
        if (clip == null) return;

        EnsureReady();
        var src = NextVoice();
        src.spatialBlend = 0f;
        src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        src.PlayOneShot(clip, Mathf.Clamp01(BaseVolume(sfx) * MasterVolume * volumeScale));
    }

    /// <summary>Positional one-shot (obstacles, turrets) – softly faded by distance.</summary>
    public static void PlayAt(CassetteSfx sfx, Vector3 worldPosition, float volumeScale = 1f, float pitch = 1f, float maxDistance = 45f)
    {
        if (!Enabled) return;
        var clip = GetClip(sfx);
        if (clip == null) return;

        EnsureReady();
        var src = NextVoice();
        src.transform.position = worldPosition;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = maxDistance;
        src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        src.PlayOneShot(clip, Mathf.Clamp01(BaseVolume(sfx) * MasterVolume * volumeScale));
    }

    static AudioSource NextVoice()
    {
        EnsureReady();
        var src = _voices[_voiceCursor];
        _voiceCursor = (_voiceCursor + 1) % _voices.Length;
        return src;
    }

    // ------------------------------------------------------------------------------------------
    // Looping sound (tape feed / 出带加速)
    // ------------------------------------------------------------------------------------------

    public static bool LoopActive => _loop != null && _loop.isPlaying;

    public static void StartLoop(CassetteSfx sfx, float volumeScale = 1f, float pitch = 1f)
    {
        if (!Enabled) return;
        var clip = GetClip(sfx);
        if (clip == null) return;

        EnsureReady();
        float target = Mathf.Clamp01(BaseVolume(sfx) * MasterVolume * volumeScale);

        if (_loop.clip != clip)
        {
            _loop.clip = clip;
            _loop.volume = 0f;
        }
        _loop.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        if (!_loop.isPlaying) _loop.Play();
        if (_host != null) _host.SetLoopTarget(target);
        else _loop.volume = target;
    }

    public static void SetLoopVolume(float volumeScale)
    {
        if (_loop == null) return;
        float target = Mathf.Clamp01(BaseVolume(CassetteSfx.TapeFeedLoop) * MasterVolume * volumeScale);
        if (_host != null) _host.SetLoopTarget(target);
        else _loop.volume = target;
    }

    public static void StopLoop()
    {
        if (_loop == null) return;
        if (_host != null) _host.FadeOutLoop();
        else _loop.Stop();
    }

    // ------------------------------------------------------------------------------------------
    // Diagnostics (also used by the CLI verification)
    // ------------------------------------------------------------------------------------------

    /// <summary>Multi-line report used by tools/tests: one line per sound effect.</summary>
    public static string DescribeAll()
    {
        var sb = new System.Text.StringBuilder();
        int ok = 0;
        foreach (var pair in ClipAssetNames)
        {
            var clip = GetClip(pair.Key);
            if (clip != null) ok++;
            sb.Append(pair.Key)
              .Append(" <- ").Append(pair.Value)
              .Append(clip == null
                  ? "  MISSING"
                  : "  ok len=" + clip.length.ToString("F2") + "s ch=" + clip.channels + " freq=" + clip.frequency)
              .Append("  vol=").Append(EffectiveVolume(pair.Key).ToString("F2"))
              .Append('\n');
        }
        sb.Append("loaded ").Append(ok).Append('/').Append(ClipAssetNames.Count)
          .Append("  master=").Append(MasterVolume.ToString("F2"));
        return sb.ToString();
    }

    /// <summary>Stops everything (used on scene reload / restart).</summary>
    public static void StopAll()
    {
        if (_voices != null)
            for (int i = 0; i < _voices.Length; i++) _voices[i].Stop();
        if (_loop != null) _loop.Stop();
        if (_host != null) _host.Reset();
    }
}

/// <summary>Fade helper for the looping tape-feed source (no coroutines needed).</summary>
public class CassetteAudioHost : MonoBehaviour
{
    public AudioSource loopSource;
    float _target;
    bool _fadingOut;

    public void SetLoopTarget(float target)
    {
        _target = Mathf.Clamp01(target);
        _fadingOut = false;
    }

    public void FadeOutLoop()
    {
        _fadingOut = true;
    }

    public void Reset()
    {
        _target = 0f;
        _fadingOut = false;
        if (loopSource != null) loopSource.volume = 0f;
    }

    void Update()
    {
        if (loopSource == null) return;
        float dt = Time.unscaledDeltaTime;
        if (_fadingOut)
        {
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, 0f, dt * 2.5f);
            if (loopSource.volume <= 0.002f)
            {
                loopSource.Stop();
                _fadingOut = false;
            }
        }
        else if (loopSource.volume < _target)
        {
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, _target, dt * 4f);
        }
    }
}