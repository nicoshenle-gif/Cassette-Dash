using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor helper for the game's sound effects:
///   • Tools ▸ Cassette Dash ▸ Verify SFX Clips  – reports every clip found under Assets/Resources/Audio/SFX
///   • CassetteSfxTools.VerifyFromCLI()           – same check for headless/batch verification
/// </summary>
public static class CassetteSfxTools
{
    [MenuItem("Tools/Cassette Dash/Verify SFX Clips")]
    public static void VerifyMenu()
    {
        string report = CassetteAudio.DescribeAll();
        Debug.Log("[CassetteSfx] verification:\n" + report);
    }

    public static void VerifyFromCLI()
    {
        string report = CassetteAudio.DescribeAll();
        Debug.Log("[CassetteSfx] verification:\n" + report);

        bool allFound = true;
        foreach (var line in report.Split('\n'))
        {
            if (line.Contains("MISSING"))
            {
                allFound = false;
                Debug.LogError("[CassetteSfx] " + line.Trim());
            }
        }

        Debug.Log(allFound ? "[CassetteSfx] all clips loaded OK" : "[CassetteSfx] some clips are missing");

        if (Application.isBatchMode)
            EditorApplication.Exit(allFound ? 0 : 1);
    }
}