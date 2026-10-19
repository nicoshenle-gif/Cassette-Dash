using UnityEngine;

/// <summary>
/// Interactive ladder connecting the top of the right reel to the cassette shell roof.
/// Player presses F near the ladder to mount, W/S to climb up/down, and Space/F to dismount.
/// Directly leads through the opening in the cassette roof onto the railway tracks!
/// </summary>
[SelectionBase]
public class CassetteLadder : MonoBehaviour
{
    [Header("Ladder Geometry")]
    public float ladderX = 9.52f;
    public float bottomY = 5.25f;
    public float topY = 8.65f;
    public float roofDeckY = 8.16f;
    public float climbSpeed = 2.8f;
    public float ladderTopY => topY;
    public float ladderBotY => bottomY;

    [Header("Mount Trigger Extents")]
    [Tooltip("Lowest world Y the mount trigger reaches.\n" +
             "The right reel pack shrinks down to hubY(2.805) + coreRadius(1.19) = 3.99 when the tape is " +
             "almost empty. The trigger must reach BELOW that surface, otherwise the player standing on a " +
             "nearly-empty reel cannot press F and is forced to jump first.")]
    public float triggerBottomY = 3.85f;
    [Tooltip("Extra height added above topY so the trigger still covers the player while topping out.")]
    public float triggerTopPad = 0.20f;
    [Tooltip("X width of the mount trigger box (centered on ladderX). 1.6 covers the whole right reel top " +
             "while staying clear of the roof exit point.")]
    public float triggerWidthX = 1.60f;
    [Tooltip("Z depth of the mount trigger. Player depth is clamped to +/-1.02 and the shell interior is " +
             "+/-1.19, so 2.6 covers every depth the player can reach.")]
    public float triggerDepthZ = 2.60f;

    [Header("Roof Exit (Topping Out)")]
    [Tooltip("World Y of the walkable surface the player is placed onto when reaching the top of the ladder.\n" +
             "The Roof_Railway BoxCollider deck top is 0.13 above the roof plate: roofDeckY(8.16) + 0.13 = 8.29.\n" +
             "Landing on the roof plate itself (8.16) leaves the capsule blocked by that 0.13 lip.")]
    public float roofExitSurfaceY = 8.29f;
    [Tooltip("World X of the topping-out spot. Must be on the rail deck (rail collider starts at x = 10.30) " +
             "and right of the mount trigger (trigger max X = ladderX + triggerWidthX/2 = 10.32) so " +
             "OnTriggerExit clears currentNearbyLadder instead of re-latching it.")]
    public float roofExitX = 10.55f;
    [Tooltip("Gap between the character's soles and the roof exit surface.")]
    public float roofExitClearance = 0.05f;

    [Header("Visual Model (Optional Custom FBX)")]
    [Tooltip("Drag custom ladder FBX here if provided; otherwise a procedural metallic ladder is generated.")]
    public GameObject customLadderModel;
    [Tooltip("Width and depth multipliers for custom ladder FBX.")]
    public float ladderWidthMultiplier = 1.25f;
    public float ladderDepthMultiplier = 1.0f;

    [Header("State")]
    public bool isPlayerNear = false;
    public CassettePlayer player;

    void Awake()
    {
        EnsureColliderAndVisuals();
    }

    public void AutoSetup(Transform parent, float x, float botY, float tY, float rDeckY)
    {
        transform.SetParent(parent, false);
        ladderX = x;
        bottomY = botY;
        topY = tY;
        roofDeckY = rDeckY;
        transform.localPosition = new Vector3(ladderX, bottomY, 0f);
        EnsureColliderAndVisuals();
    }

    void EnsureColliderAndVisuals()
    {
        var col = GetComponent<BoxCollider>();
        if (!col) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;

        // The mount trigger has to span the whole usable right-reel surface, not just the ladder rails.
        // World Y range: [triggerBottomY .. topY + triggerTopPad].
        // This object sits at world (ladderX, bottomY, 0), so convert world Y to local Y.
        float trigBottom = TriggerBottomWorldY;
        float trigTop = TriggerTopWorldY;
        float trigHeight = Mathf.Max(0.5f, trigTop - trigBottom);
        col.center = new Vector3(0f, (trigBottom + trigTop) * 0.5f - bottomY, 0f);
        col.size = new Vector3(
            Mathf.Max(0.2f, triggerWidthX),
            trigHeight,
            Mathf.Max(0.2f, triggerDepthZ));

#if UNITY_EDITOR
        if (customLadderModel == null)
        {
            customLadderModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Ladder.fbx");
        }
#endif

        if (transform.childCount == 0)
        {
            BuildLadderVisuals();
        }
    }

    public void BuildLadderVisuals()
    {
        // Clear existing children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        float targetHeight = topY - bottomY;

        if (customLadderModel != null)
        {
            float rawHeight = 1.101f;
            var mf = customLadderModel.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.size.y > 0.0001f)
            {
                rawHeight = mf.sharedMesh.bounds.size.y;
            }

            int sectionCount = Mathf.Max(1, Mathf.RoundToInt(targetHeight / rawHeight));
            float sectionH = targetHeight / sectionCount;
            float scaleY = sectionH / rawHeight;
            float scaleX = scaleY * ladderWidthMultiplier;
            float scaleZ = scaleY * ladderDepthMultiplier;

            Material ladderMat = null;
#if UNITY_EDITOR
            ladderMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Ladder_Material.mat");
#endif
            var urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            for (int i = 0; i < sectionCount; i++)
            {
                var inst = Instantiate(customLadderModel, transform);
                inst.name = $"Ladder_Seg_{i}";
                inst.transform.localPosition = new Vector3(0f, i * sectionH, 0f);
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    if (ladderMat != null)
                    {
                        r.sharedMaterial = ladderMat;
                    }
                    else if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("Error"))
                    {
                        var m = new Material(urpShader) { color = new Color(0.95f, 0.78f, 0.18f) };
                        m.SetFloat("_Smoothness", 0.7f);
                        m.SetFloat("_Metallic", 0.6f);
                        r.sharedMaterial = m;
                    }
                }
            }
            return;
        }

        BuildProceduralLadder();
    }

    public void BuildProceduralLadder()
    {
        // Clear existing children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        float height = topY - bottomY;
        float railSpacing = 0.52f;
        float railRadius = 0.038f;

        // Side rails (left & right)
        CreateRail("Rail_Left", new Vector3(-railSpacing * 0.5f, height * 0.5f, 0f), height + 0.35f, railRadius);
        CreateRail("Rail_Right", new Vector3(railSpacing * 0.5f, height * 0.5f, 0f), height + 0.35f, railRadius);

        // Horizontal rungs
        int rungCount = Mathf.FloorToInt(height / 0.32f);
        for (int i = 1; i <= rungCount; i++)
        {
            float rungY = i * 0.32f;
            CreateRung($"Rung_{i}", new Vector3(0f, rungY, 0f), railSpacing, railRadius * 0.85f);
        }

        // Top grab handles extending onto the roof
        CreateRail("Handle_Left", new Vector3(-railSpacing * 0.5f, height + 0.22f, -0.12f), 0.45f, railRadius);
        CreateRail("Handle_Right", new Vector3(railSpacing * 0.5f, height + 0.22f, -0.12f), 0.45f, railRadius);
    }

    void CreateRail(string name, Vector3 localPos, float height, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        var c = go.GetComponent<Collider>();
        if (c)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
        ApplyMat(go, new Color(0.92f, 0.76f, 0.20f)); // Bright industrial ladder yellow
    }

    void CreateRung(string name, Vector3 localPos, float length, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        var c = go.GetComponent<Collider>();
        if (c)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
        ApplyMat(go, new Color(0.95f, 0.95f, 0.98f)); // Polished silver rungs
    }

    void ApplyMat(GameObject go, Color col)
    {
        var r = go.GetComponent<Renderer>();
        if (!r) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { color = col };
        m.SetFloat("_Smoothness", 0.65f);
        r.sharedMaterial = m;
    }

    // ------------------------------------------------------------------------------------------
    // Climb helpers used by CassettePlayer
    // ------------------------------------------------------------------------------------------

    /// <summary>Lowest world Y covered by the mount trigger.</summary>
    public float TriggerBottomWorldY => Mathf.Min(triggerBottomY, bottomY - 0.35f);

    /// <summary>Highest world Y covered by the mount trigger.</summary>
    public float TriggerTopWorldY => topY + Mathf.Max(0f, triggerTopPad);

    /// <summary>
    /// World position (capsule centre) the player is placed at when reaching the top of the ladder.
    /// The Roof_Railway BoxCollider forms a 0.13 lip above the roof plate, so the player must be put
    /// ON TOP of the rail deck; landing on the roof plate itself wedges the capsule against that lip,
    /// which is why pressing D next to the train did nothing. The exit point is also kept to the right
    /// of the mount trigger so walking off the ladder does not leave currentNearbyLadder latched.
    /// </summary>
    public Vector3 GetRoofExitPosition(float capsuleHalfHeight)
    {
        float y = roofExitSurfaceY + Mathf.Max(0f, capsuleHalfHeight) + Mathf.Max(0f, roofExitClearance);
        return new Vector3(roofExitX, y, 0f);
    }

    /// <summary>
    /// World position (capsule centre) the player snaps to when mounting the ladder.
    /// The right reel pack can be as small as coreRadius, which leaves the player's feet ~1.3 units
    /// below bottomY. Lifting the capsule to the bottom of the climb range keeps the climb loop from
    /// immediately detecting "reached the bottom" and auto-dismounting on the first physics step.
    /// </summary>
    public Vector3 GetBottomMountPosition(Vector3 currentPosition, float capsuleHalfHeight)
    {
        float minY = bottomY + Mathf.Max(0f, capsuleHalfHeight) + 0.05f;
        return new Vector3(ladderX, Mathf.Max(currentPosition.y, minY), 0f);
    }

    /// <summary>
    /// Same volume as the mount trigger, evaluated analytically.
    /// Trigger callbacks are only dispatched during the physics step, so this is used as a fallback by
    /// CassettePlayer.HandleInteract for the frame right after a teleport (or whenever the trigger has
    /// not fired yet). It derives from the same fields as EnsureColliderAndVisuals so it cannot drift.
    /// </summary>
    public bool IsCapsuleInMountRange(Vector3 capsuleCenter, float capsuleHalfHeight)
    {
        Vector3 c = transform.position; // (ladderX, bottomY, 0) after AutoSetup
        float halfW = Mathf.Max(0.2f, triggerWidthX) * 0.5f;
        float halfD = Mathf.Max(0.2f, triggerDepthZ) * 0.5f;
        if (Mathf.Abs(capsuleCenter.x - c.x) > halfW + 0.10f) return false;
        if (Mathf.Abs(capsuleCenter.z - c.z) > halfD + 0.10f) return false;
        float halfH = Mathf.Max(0f, capsuleHalfHeight);
        float capsuleTop = capsuleCenter.y + halfH;
        float capsuleBottom = capsuleCenter.y - halfH;
        return capsuleTop >= TriggerBottomWorldY && capsuleBottom <= TriggerTopWorldY;
    }

    void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponent<CassettePlayer>() ?? other.GetComponentInParent<CassettePlayer>();
        if (p)
        {
            player = p;
            isPlayerNear = true;
            p.currentNearbyLadder = this;
        }
    }

    void OnTriggerExit(Collider other)
    {
        var p = other.GetComponent<CassettePlayer>() ?? other.GetComponentInParent<CassettePlayer>();
        if (p && p == player)
        {
            isPlayerNear = false;
            if (p.currentNearbyLadder == this)
                p.currentNearbyLadder = null;
        }
    }
}
