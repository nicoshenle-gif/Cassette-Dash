using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Procedurally builds an empty-shell cassette tape physics skeleton from primitives,
/// including a solid Back Cover (+Z), transparent Front Cover (-Z), narrow tape strip,
/// rear-mounted head-frame ribs (leaving a front Z bypass lane so the player can walk
/// around the slanted ribs to reach the bottom tape run), and the custom FBX character.
/// </summary>
public static class CassetteLevelGenerator
{
    const string RootName = "Cassette_Level";
    const string PlayerFbxPath = "Assets/Models/Player_Idle.fbx";
    const string PlayerRunFbxPath = "Assets/Models/Player_Run.fbx";
    const string PlayerFallFbxPath = "Assets/Models/Player_Fall.fbx";
    const string PlayerLandFbxPath = "Assets/Models/Player_Land.fbx";
    const string PlayerHitFbxPath = "Assets/Models/Player_Hit.fbx";
    const string PlayerCrouchFbxPath = "Assets/Models/Player_Crouch.fbx";
    const string PlayerCrouchSourceFbxPath = @"d:\微信聊天数据\xwechat_files\wxid_jiquydr51spg22_a28a\msg\file\2026-10\蹲转.fbx";
    const string PlayerHitSourceFbxPath = @"d:\微信聊天数据\xwechat_files\wxid_jiquydr51spg22_a28a\msg\file\2026-10\受击.fbx";
    const string PlayerControllerPath = "Assets/Models/Player_Animator.controller";

    // ---- Global scale ----
    const float MmToUnit = 0.17f;           // 220.4 mm elongated shell -> ~37.47 units wide for bottom parkour

    // ---- Shell (mm) ----
    const float ShellWidthMm = 220.4f;      // Elongated horizontally (+120 mm) for bottom parkour runway
    const float ShellHeightMm = 96.0f;      // Taller shell (~16.32 units high) for vertical headroom & parkour
    const float WallMm = 3f;
    const float DepthMm = 14f;              // Z interior depth (14 mm -> 2.38 units for comfortable front/back lanes)
    const float CoverThicknessMm = 1.2f;    // Back & front cover thickness

    // ---- Reel hubs (mm) ----
    const float HubSpacingMm = 112.0f;      // Moved inward (hubX = +-56.0 mm = +-9.52 units: left gear right, right gear left)
    const float HubCenterYMm = 16.5f;       // Raised higher up in the taller cassette shell (+2.81 units)
    const float HubCoreRadiusMm = 7f;       // solid core = collider
    const float HubGearRadiusMm = 9.5f;     // decorative gear disk
    const int GearTeeth = 18;
    const float ToothLengthMm = 1.4f;
    const float ToothWidthMm = 1.1f;
    const float SpindleHoleRadiusMm = 4.2f;

    // ---- Tape line: height of the tape above the inner floor (mm) ----
    const float TapeLineMm = 1.4f;

    // ---- Tape itself (mm) - narrower than shell depth so player can move in/out along Z ----
    const float TapeWidthMm = 6.2f;             // ~44% of shell depth (centered at Z = 0)
    const float TapeVisualThicknessMm = 0.28f;  // thickness of the thin strip in XY
    const float TapePackFullRadiusMm = 14.5f;   // outer radius when all tape is on one reel

    // ---- Corner guide rollers (mm) ----
    const float RollerXMm = 100.8f;             // +-17.14 units -> 34.27-unit bottom parkour runway!
    const float RollerRadiusMm = 4f;

    // ---- Guide pins between rollers and head (mm) ----
    const float GuidePinXMm = 48f;
    const float GuidePinRadiusMm = 1f;

    // ---- Pressure pad (felt) + leaf spring (mm) ----
    const float PadWidthMm = 7f;
    const float PadHeightMm = 2.5f;
    const float PadGapMm = 1.6f;

    // ---- Lower head-area frame (low-profile relief on the Back Cover, narrowed away from rollers/tape sag) ----
    const float FrameTopYMm = -31.5f;
    const float FrameTopHalfWidthMm = 54f;
    const float FrameBottomHalfWidthMm = 58f;
    const float FrameSideBottomYMm = -41.0f;
    const float RibThicknessMm = 1.1f;
    const float RearStructureDepthMm = 2.2f; // thin relief on Back Cover (+Z), completely behind the tape & player lane

    // ---- Screws (mm) ----
    const float ScrewBossRadiusMm = 2.4f;
    const float CornerScrewXMm = 107.5f;
    const float CornerScrewYMm = 42.5f;
    const float CenterScrewYMm = -35.0f;

    static readonly Color ShellColor = new Color(0.78f, 0.80f, 0.82f);
    static readonly Color BackCoverColor = new Color(0.72f, 0.74f, 0.77f);
    static readonly Color FrontGlassColor = new Color(0.88f, 0.94f, 1.0f, 0.14f);
    static readonly Color WhiteColor = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color MetalColor = new Color(0.6f, 0.62f, 0.66f);
    static readonly Color DarkColor = new Color(0.25f, 0.25f, 0.28f);
    static readonly Color FeltColor = new Color(0.55f, 0.42f, 0.30f);
    static readonly Color PlayerColor = new Color(0.20f, 0.78f, 0.95f);

    static float U(float mm) => mm * MmToUnit;
    static Vector3 P(float xMm, float yMm, float z = 0f) => new Vector3(U(xMm), U(yMm), z);

    static float Depth => U(DepthMm);
    static float TapeDepth => U(TapeWidthMm);
    static float RearDepth => U(RearStructureDepthMm);
    // Z center of rear-mounted relief ribs (flush against Back Cover at +Depth/2)
    static float RearZ => (Depth - RearDepth) * 0.5f;

    [MenuItem("Tools/Generate Cassette Level")]
    public static void Generate()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject.transform.IsChildOf(existing.transform))
                Selection.activeGameObject = null;
            Undo.DestroyObjectImmediate(existing);
        }

        // Clean up any stray roof train, railway, or ladder objects in the scene
        var strayTrain = GameObject.Find("Roof_Train_System");
        if (strayTrain != null) Undo.DestroyObjectImmediate(strayTrain);
        var strayRail = GameObject.Find("Roof_Railway");
        if (strayRail != null) Undo.DestroyObjectImmediate(strayRail);
        var strayLadder = GameObject.Find("Cassette_Ladder");
        if (strayLadder != null) Undo.DestroyObjectImmediate(strayLadder);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Generate Cassette Level");
        root.transform.position = Vector3.zero;

        float halfW = ShellWidthMm * 0.5f;
        float halfH = ShellHeightMm * 0.5f;
        float floorMm = -halfH + WallMm;
        float tapeYMm = floorMm + TapeLineMm;

        var shell = Group(root, "Shell");
        var mech = Group(root, "Mechanism");
        var anchors = Group(root, "TapeAnchors");

        // ---- 1. Shell perimeter walls (with ladder opening above right hub) ----
        float rightHubX = U(HubSpacingMm * 0.5f);
        float hatchHalfW = 0.45f;
        float leftWallW = (rightHubX - hatchHalfW) - (-U(halfW));
        float leftCenterX = (-U(halfW) + (rightHubX - hatchHalfW)) * 0.5f;
        CreateBox(shell, "Wall_Top_Left", new Vector3(leftCenterX, U(halfH - WallMm * 0.5f), 0f), new Vector3(leftWallW, U(WallMm), Depth), ShellColor);

        float rightWallW = U(halfW) - (rightHubX + hatchHalfW);
        float rightCenterX = ((rightHubX + hatchHalfW) + U(halfW)) * 0.5f;
        CreateBox(shell, "Wall_Top_Right", new Vector3(rightCenterX, U(halfH - WallMm * 0.5f), 0f), new Vector3(rightWallW, U(WallMm), Depth), ShellColor);

        var wallBottom = CreateBox(shell, "Wall_Bottom", P(0, -halfH + WallMm * 0.5f), new Vector3(U(ShellWidthMm), U(WallMm), Depth), ShellColor);
        float sideH = ShellHeightMm - WallMm * 2f;
        CreateBox(shell, "Wall_Left", P(-halfW + WallMm * 0.5f, 0), new Vector3(U(WallMm), U(sideH), Depth), ShellColor);
        CreateBox(shell, "Wall_Right", P(halfW - WallMm * 0.5f, 0), new Vector3(U(WallMm), U(sideH), Depth), ShellColor);

        // ---- 2. Solid Back Cover (+Z) & Transparent Front Cover (-Z) ----
        float coverThick = U(CoverThicknessMm);
        CreateBox(shell, "Wall_BackCover",
            new Vector3(0f, 0f, Depth * 0.5f + coverThick * 0.5f),
            new Vector3(U(ShellWidthMm), U(ShellHeightMm), coverThick), BackCoverColor);

        var frontCover = CreateBox(shell, "Wall_FrontCover_Glass",
            new Vector3(0f, 0f, -Depth * 0.5f - coverThick * 0.5f),
            new Vector3(U(ShellWidthMm), U(ShellHeightMm), coverThick), FrontGlassColor);
        SetTransparentMaterial(frontCover, FrontGlassColor);

        // ---- 3. Low-profile rear-mounted head frame (pure visual relief on Back Cover, never blocks tape or player) ----
        CreateSegment(shell, "HeadFrame_Top",
            new Vector2(-FrameTopHalfWidthMm, FrameTopYMm), new Vector2(FrameTopHalfWidthMm, FrameTopYMm),
            RibThicknessMm, RearDepth, RearZ, ShellColor, false);
        CreateSegment(shell, "HeadFrame_SideLeft",
            new Vector2(-FrameTopHalfWidthMm, FrameTopYMm), new Vector2(-FrameBottomHalfWidthMm, FrameSideBottomYMm),
            RibThicknessMm, RearDepth, RearZ, ShellColor, false);
        CreateSegment(shell, "HeadFrame_SideRight",
            new Vector2(FrameTopHalfWidthMm, FrameTopYMm), new Vector2(FrameBottomHalfWidthMm, FrameSideBottomYMm),
            RibThicknessMm, RearDepth, RearZ, ShellColor, false);

        // ---- 4. Screws: 4 corners (full depth) + 1 center (rear-mounted) ----
        CreateScrew(shell, "ScrewPost_TL", -CornerScrewXMm, CornerScrewYMm, Depth, 0f);
        CreateScrew(shell, "ScrewPost_TR", CornerScrewXMm, CornerScrewYMm, Depth, 0f);
        CreateScrew(shell, "ScrewPost_BL", -CornerScrewXMm, -CornerScrewYMm, Depth, 0f);
        CreateScrew(shell, "ScrewPost_BR", CornerScrewXMm, -CornerScrewYMm, Depth, 0f);
        CreateScrew(shell, "ScrewPost_Center", 0f, CenterScrewYMm, RearDepth, RearZ);

        // ---- 5. Reel hubs (centered at Z = 0, matching narrow tape width) ----
        float hubX = HubSpacingMm * 0.5f;
        var leftHub = CreateHub(mech, "LeftReelHub", -hubX, HubCenterYMm);
        var rightHub = CreateHub(mech, "RightReelHub", hubX, HubCenterYMm);

        // ---- 6. Corner guide rollers (narrow width matching tape so front bypass lane stays wide open) ----
        float rollerYMm = tapeYMm + RollerRadiusMm;
        var leftRoller = CreateRoller(mech, "GuideRoller_Left", -RollerXMm, rollerYMm);
        var rightRoller = CreateRoller(mech, "GuideRoller_Right", RollerXMm, rollerYMm);

        // ---- 7. Guide pins & Pressure pad (matching tape width, plus invisible bottom tape collider) ----
        float pinYMm = tapeYMm + GuidePinRadiusMm;
        CreateCylinder(mech, "GuidePin_Left", P(-GuidePinXMm, pinYMm), U(GuidePinRadiusMm), TapeDepth, MetalColor);
        CreateCylinder(mech, "GuidePin_Right", P(GuidePinXMm, pinYMm), U(GuidePinRadiusMm), TapeDepth, MetalColor);

        float padBottomMm = tapeYMm + PadGapMm;
        float padCenterYMm = padBottomMm + PadHeightMm * 0.5f;
        var headPadGo = CreateBox(mech, "TapeHeadPad", P(0, padCenterYMm), new Vector3(U(PadWidthMm), U(PadHeightMm), TapeDepth), DarkColor);
        var feltGo = CreateVisualBox(mech, "TapeHeadPad_Felt", P(0, padBottomMm + 0.3f),
            new Vector3(U(PadWidthMm * 0.8f), U(0.6f), TapeDepth * 1.02f), FeltColor);

        float springTopYMm = CenterScrewYMm - ScrewBossRadiusMm;
        float padTopMm = padBottomMm + PadHeightMm;
        var springL = CreateSegment(mech, "PadSpring_L", new Vector2(0f, springTopYMm), new Vector2(-PadWidthMm * 0.4f, padTopMm), 0.5f, TapeDepth * 0.5f, 0f, MetalColor, false);
        var springR = CreateSegment(mech, "PadSpring_R", new Vector2(0f, springTopYMm), new Vector2(PadWidthMm * 0.4f, padTopMm), 0.5f, TapeDepth * 0.5f, 0f, MetalColor, false);

        var headObstacle = headPadGo.AddComponent<CassetteHeadObstacle>();
        headObstacle.tapeSurfaceY = U(tapeYMm);
        headObstacle.feltTransform = feltGo.transform;
        headObstacle.springLeft = springL.transform;
        headObstacle.springRight = springR.transform;

        // Thin collider along the bottom horizontal tape run so the player can run directly on the bottom tape strip
        var bottomTapeCol = new GameObject("BottomTapeRun_Collider");
        bottomTapeCol.transform.SetParent(mech.transform, false);
        bottomTapeCol.transform.localPosition = P(0f, tapeYMm);
        var bc = bottomTapeCol.AddComponent<BoxCollider>();
        bc.size = new Vector3(U(RollerXMm * 2f), U(TapeVisualThicknessMm * 2f), TapeDepth);

        // ---- 8. Tape anchors ----
        CreateAnchor(anchors, "00_LeftHub", -hubX, HubCenterYMm);
        CreateAnchor(anchors, "01_LeftRoller_Outer", -RollerXMm - RollerRadiusMm, rollerYMm);
        CreateAnchor(anchors, "02_LeftRoller_Bottom", -RollerXMm, tapeYMm);
        CreateAnchor(anchors, "03_LeftPin_Bottom", -GuidePinXMm, tapeYMm);
        CreateAnchor(anchors, "04_Head", 0f, tapeYMm);
        CreateAnchor(anchors, "05_RightPin_Bottom", GuidePinXMm, tapeYMm);
        CreateAnchor(anchors, "06_RightRoller_Bottom", RollerXMm, tapeYMm);
        CreateAnchor(anchors, "07_RightRoller_Outer", RollerXMm + RollerRadiusMm, rollerYMm);
        CreateAnchor(anchors, "08_RightHub", hubX, HubCenterYMm);

        // ---- 9. Animated narrow tape ----
        var tapeGo = new GameObject("Tape");
        tapeGo.transform.SetParent(root.transform, false);
        var leftPack = CreateCylinder(tapeGo, "TapePack_Left", P(-hubX, HubCenterYMm),
            U(TapePackFullRadiusMm), TapeDepth, DarkColor);
        var rightPack = CreateCylinder(tapeGo, "TapePack_Right", P(hubX, HubCenterYMm),
            U(HubCoreRadiusMm), TapeDepth, DarkColor);

        // ---- 10. Player with custom FBX model & Idle/Run animations ----
        float capHalfHeight = 0.30f;
        Vector3 playerSpawn = new Vector3(U(-hubX), U(HubCenterYMm + TapePackFullRadiusMm) + capHalfHeight, 0f);
        var player = CreatePlayer(root, playerSpawn);

        var tape = tapeGo.AddComponent<CassetteTape>();
        tape.leftHub = leftHub.transform;
        tape.rightHub = rightHub.transform;
        tape.leftRoller = leftRoller.transform;
        tape.rightRoller = rightRoller.transform;
        tape.leftPack = leftPack.transform;
        tape.rightPack = rightPack.transform;
        tape.player = player;
        tape.shellFloor = wallBottom.transform;
        tape.coreRadius = U(HubCoreRadiusMm);
        tape.fullRadius = U(TapePackFullRadiusMm);
        tape.rollerRadius = U(RollerRadiusMm);
        tape.tapeWidth = TapeDepth;
        tape.tapeThickness = U(TapeVisualThicknessMm);
        tape.fractionOnLeft = 1f;
        tape.direction = 1;
        tape.animateInEditMode = false;
        tape.Rebuild();
        player.tape = tape;

        // ---- 11. Build Parkour Obstacles (Rising Pin Spikes & Overhead Extending Droppers) ----
        CassetteParkourObstacleBuilder.BuildOrUpdateParkourObstacles(root.transform, player);

        // ---- 12. Ensure Right-Gear Ladder & Cassette Roof Railway Train ----
        player.EnsureLadderAndRoofTrain(root.transform);

        // ---- Frame Main Camera & Attach Smooth Follow ----
        var cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<CassetteCameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CassetteCameraFollow>();
            follow.target = player.transform;
            follow.distance = 6.8f;
            follow.planarOffset = new Vector2(0f, -0.45f);
            follow.cassetteHalfSize = new Vector2(U(ShellWidthMm * 0.5f), U(ShellHeightMm * 0.5f));

            cam.transform.position = new Vector3(
                player.transform.position.x + follow.planarOffset.x,
                player.transform.position.y + follow.planarOffset.y,
                -follow.distance);
            cam.transform.rotation = Quaternion.identity;
        }

        Selection.activeGameObject = player.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("[CassetteLevelGenerator] Generated '" + RootName + "' with Parkour Obstacles, Smooth 96-Sided Reels, and Fitted Player.");
    }

    // =====================================================================================
    // Player Builder (uses Assets/Models/Player_Idle.fbx + Player_Run.fbx)
    // =====================================================================================

    static CassettePlayer CreatePlayer(GameObject root, Vector3 spawnPos)
    {
        var go = new GameObject("Player");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = spawnPos;

        float capRadius = 0.14f;
        float capHeight = 0.62f;
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = capRadius;
        col.height = capHeight;
        col.center = Vector3.zero;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 1.2f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxDepenetrationVelocity = 2.5f;

        var player = go.AddComponent<CassettePlayer>();
        float halfPlayableZ = Depth * 0.5f - capRadius - 0.03f;
        player.minZ = -halfPlayableZ;
        player.maxZ = halfPlayableZ;

        var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbxPath);
        if (fbxPrefab != null)
        {
            var modelPivot = new GameObject("ModelRoot");
            modelPivot.transform.SetParent(go.transform, false);
            modelPivot.transform.localPosition = Vector3.zero;
            modelPivot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var modelInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, modelPivot.transform);
            modelInst.transform.localScale = Vector3.one;
            modelInst.transform.localPosition = Vector3.zero;
            modelInst.transform.localRotation = Quaternion.identity;

            foreach (var smr in modelInst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            // Sample the Idle animation clip at t = 0 so knees/feet are in their actual gameplay Idle pose
            AnimationClip idleClip = FindClipInAsset(PlayerFbxPath);
            if (idleClip != null)
                idleClip.SampleAnimation(modelInst, 0f);

            // Measure actual posed world bounds and fit height + sole position to match the CapsuleCollider
            if (TryGetPosedWorldBounds(modelInst, out Bounds b))
            {
                float rawHeight = Mathf.Max(0.01f, b.size.y);
                float fitScale = capHeight / rawHeight;
                modelInst.transform.localScale = Vector3.one * fitScale;
                Physics.SyncTransforms();

                if (TryGetPosedWorldBounds(modelInst, out b))
                {
                    float capsuleBottomWorldY = go.transform.position.y - capHeight * 0.5f;
                    float offsetY = capsuleBottomWorldY - b.min.y;
                    float offsetX = go.transform.position.x - b.center.x;
                    float offsetZ = go.transform.position.z - b.center.z;
                    modelInst.transform.position += new Vector3(offsetX, offsetY, offsetZ);
                }
            }

            AssignExtractedTextures(modelInst);

            var anim = modelInst.GetComponent<Animator>();
            if (anim == null) anim = modelInst.AddComponent<Animator>();
            anim.runtimeAnimatorController = BuildOrUpdatePlayerController();
            anim.applyRootMotion = false;

            player.modelRoot = modelPivot.transform;
            player.animator = anim;
        }
        else
        {
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(fallback.GetComponent<Collider>());
            fallback.transform.SetParent(go.transform, false);
            fallback.transform.localScale = new Vector3(0.26f, 0.30f, 0.26f);
            SetColor(fallback, PlayerColor);
            player.modelRoot = fallback.transform;
        }

        return player;
    }

    static bool TryGetPosedWorldBounds(GameObject modelInst, out Bounds worldBounds)
    {
        worldBounds = default;
        bool hasAny = false;

        Vector3 rootScale = modelInst.transform.lossyScale;
        var smrs = modelInst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs != null && smrs.Length > 0)
        {
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
                    if (!hasAny) { worldBounds = new Bounds(wp, Vector3.zero); hasAny = true; }
                    else worldBounds.Encapsulate(wp);
                }
            }
            Object.DestroyImmediate(baked);
        }

        if (!hasAny)
        {
            var renderers = modelInst.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasAny) { worldBounds = renderers[i].bounds; hasAny = true; }
                else worldBounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasAny;
    }

    static void AssignExtractedTextures(GameObject modelInst)
    {
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Textures/texture_pbr_20250901.png");
        if (baseTex == null) return;
        var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Textures/texture_pbr_20250901_normal.png");
        var metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Textures/texture_pbr_20250901_metallic.png");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { name = "Player_PBR_Runtime" };
        mat.mainTexture = baseTex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (normalTex != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (metallicTex != null && mat.HasProperty("_MetallicGlossMap"))
        {
            mat.SetTexture("_MetallicGlossMap", metallicTex);
        }

        foreach (var r in modelInst.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = mat;
    }

    static AnimationClip FindClipInAsset(string assetPath)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (obj is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        }
        return null;
    }

    [InitializeOnLoadMethod]
    static void EnsureFallAnimationControllerOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!System.IO.File.Exists(PlayerFallFbxPath)) return;

            if (!System.IO.File.Exists(PlayerHitFbxPath) && System.IO.File.Exists(PlayerHitSourceFbxPath))
            {
                System.IO.File.Copy(PlayerHitSourceFbxPath, PlayerHitFbxPath, true);
                AssetDatabase.ImportAsset(PlayerHitFbxPath, ImportAssetOptions.ForceSynchronousImport);
            }

            if (!System.IO.File.Exists(PlayerCrouchFbxPath) && System.IO.File.Exists(PlayerCrouchSourceFbxPath))
            {
                System.IO.File.Copy(PlayerCrouchSourceFbxPath, PlayerCrouchFbxPath, true);
                AssetDatabase.ImportAsset(PlayerCrouchFbxPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            bool hasDeadParam = false;
            bool hasLandParam = false;
            bool hasHitParam = false;
            bool hasCrouchParam = false;
            if (ctrl != null)
            {
                foreach (var p in ctrl.parameters)
                {
                    if (p.name == "IsDead") hasDeadParam = true;
                    if (p.name == "Land") hasLandParam = true;
                    if (p.name == "Hit") hasHitParam = true;
                    if (p.name == "IsCrouching") hasCrouchParam = true;
                }
            }

            AnimationClip currentHitClip = FindClipInAsset(PlayerHitFbxPath);
            bool hitClipNeedsTrim = currentHitClip == null || currentHitClip.length > 0.6f;

            if (!hasDeadParam || !hasLandParam || !hasHitParam || !hasCrouchParam || hitClipNeedsTrim)
            {
                var updated = BuildOrUpdatePlayerController();
                var player = Object.FindFirstObjectByType<CassettePlayer>();
                if (player != null && player.animator != null && updated != null)
                {
                    player.animator.runtimeAnimatorController = updated;
                    EditorUtility.SetDirty(player.animator);
                }
            }

            var wallBottom = GameObject.Find("Wall_Bottom");
            var wallLeft = GameObject.Find("Wall_Left");
            if ((wallBottom != null && wallBottom.transform.localScale.x < 30f) ||
                (wallLeft != null && wallLeft.transform.localScale.y < 13f))
            {
                Generate();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                return;
            }

            if (Object.FindFirstObjectByType<CassetteHeadObstacle>() == null)
            {
                var padGo = GameObject.Find("TapeHeadPad");
                if (padGo != null)
                {
                    var obs = padGo.AddComponent<CassetteHeadObstacle>();
                    obs.AutoBindReferences();
                    EditorUtility.SetDirty(padGo);
                }
            }

            var rootGo = GameObject.Find(RootName);
            if (rootGo != null)
            {
                foreach (var tr in rootGo.GetComponentsInChildren<Transform>(true))
                {
                    if (tr != null)
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
                }
                var p = Object.FindFirstObjectByType<CassettePlayer>();
                CassetteParkourObstacleBuilder.BuildOrUpdateParkourObstacles(rootGo.transform, p);
            }
        };
    }

    static void ConfigureClipLooping(string assetPath, bool loopTime, int maxLastFrame = -1)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) return;

        bool changed = false;
        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
        }

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime != loopTime)
                {
                    clips[i].loopTime = loopTime;
                    changed = true;
                }
                if (maxLastFrame > 0 && Mathf.RoundToInt(clips[i].lastFrame) != maxLastFrame)
                {
                    clips[i].firstFrame = 0f;
                    clips[i].lastFrame = maxLastFrame;
                    changed = true;
                }
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }
    }

    static AnimatorState _crouchStateForLinks;

    public static RuntimeAnimatorController BuildOrUpdatePlayerController()
    {
        if (!System.IO.File.Exists(PlayerHitFbxPath) && System.IO.File.Exists(PlayerHitSourceFbxPath))
            System.IO.File.Copy(PlayerHitSourceFbxPath, PlayerHitFbxPath, true);
        if (!System.IO.File.Exists(PlayerCrouchFbxPath) && System.IO.File.Exists(PlayerCrouchSourceFbxPath))
            System.IO.File.Copy(PlayerCrouchSourceFbxPath, PlayerCrouchFbxPath, true);

        if (System.IO.File.Exists(PlayerFallFbxPath))
            AssetDatabase.ImportAsset(PlayerFallFbxPath, ImportAssetOptions.ForceSynchronousImport);
        if (System.IO.File.Exists(PlayerLandFbxPath))
            AssetDatabase.ImportAsset(PlayerLandFbxPath, ImportAssetOptions.ForceSynchronousImport);
        if (System.IO.File.Exists(PlayerHitFbxPath))
            AssetDatabase.ImportAsset(PlayerHitFbxPath, ImportAssetOptions.ForceSynchronousImport);
        if (System.IO.File.Exists(PlayerCrouchFbxPath))
            AssetDatabase.ImportAsset(PlayerCrouchFbxPath, ImportAssetOptions.ForceSynchronousImport);

        ConfigureClipLooping(PlayerFbxPath, true);
        ConfigureClipLooping(PlayerRunFbxPath, true);
        ConfigureClipLooping(PlayerFallFbxPath, false);     // Play knockdown once and hold final pose on ground
        ConfigureClipLooping(PlayerLandFbxPath, false);     // Play landing animation once per landing
        ConfigureClipLooping(PlayerHitFbxPath, false, 30);  // Trim to frames 0..30 (0.5s short standing flinch; cuts off late collapse)
        ConfigureClipLooping(PlayerCrouchFbxPath, true);    // Crouch pose loops while Ctrl is held

        AnimationClip idleClip = FindClipInAsset(PlayerFbxPath);
        AnimationClip runClip = FindClipInAsset(PlayerRunFbxPath);
        AnimationClip fallClip = FindClipInAsset(PlayerFallFbxPath);
        AnimationClip landClip = FindClipInAsset(PlayerLandFbxPath);
        AnimationClip hitClip = FindClipInAsset(PlayerHitFbxPath);
        AnimationClip crouchClip = FindClipInAsset(PlayerCrouchFbxPath);
        if (idleClip == null) return null;

        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath) != null)
            AssetDatabase.DeleteAsset(PlayerControllerPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
        ctrl.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Land", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var idleState = sm.AddState("Idle");
        idleState.motion = idleClip;
        sm.defaultState = idleState;

        AnimatorState runState = null;
        AnimatorState _crouchStateForLinks = null;
        if (runClip != null)
        {
            runState = sm.AddState("Run");
            runState.motion = runClip;

            var toRun = idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.12f;
            toRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            toRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");

            var toIdle = runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        }

        if (crouchClip != null)
        {
            var crouchState = sm.AddState("Crouch");
            crouchState.motion = crouchClip;

            var idleToCrouch = idleState.AddTransition(crouchState);
            idleToCrouch.hasExitTime = false;
            idleToCrouch.duration = 0.10f;
            idleToCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
            idleToCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

            if (runState != null)
            {
                var runToCrouch = runState.AddTransition(crouchState);
                runToCrouch.hasExitTime = false;
                runToCrouch.duration = 0.10f;
                runToCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
                runToCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

                var crouchToRun = crouchState.AddTransition(runState);
                crouchToRun.hasExitTime = false;
                crouchToRun.duration = 0.12f;
                crouchToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
                crouchToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            }

            var crouchToIdle = crouchState.AddTransition(idleState);
            crouchToIdle.hasExitTime = false;
            crouchToIdle.duration = 0.12f;
            crouchToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            crouchToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            // Hit / Fall interrupts are wired below once those states exist
            _crouchStateForLinks = crouchState;
        }
        else
        {
            _crouchStateForLinks = null;
        }

        AnimatorState landState = null;
        if (landClip != null)
        {
            landState = sm.AddState("Land");
            landState.motion = landClip;
            landState.speed = 2.0f; // Speed up landing animation by 2x as requested!

            var idleToLand = idleState.AddTransition(landState);
            idleToLand.hasExitTime = false;
            idleToLand.duration = 0.05f;
            idleToLand.AddCondition(AnimatorConditionMode.If, 0f, "Land");
            idleToLand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

            if (runState != null)
            {
                var runToLand = runState.AddTransition(landState);
                runToLand.hasExitTime = false;
                runToLand.duration = 0.05f;
                runToLand.AddCondition(AnimatorConditionMode.If, 0f, "Land");
                runToLand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

                var landToRun = landState.AddTransition(runState);
                landToRun.hasExitTime = true;
                landToRun.exitTime = 0.65f;
                landToRun.duration = 0.12f;
                landToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
                landToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            }

            var landToIdle = landState.AddTransition(idleState);
            landToIdle.hasExitTime = true;
            landToIdle.exitTime = 0.88f;
            landToIdle.duration = 0.12f;
            landToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            landToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        }

        AnimatorState hitState = null;
        if (hitClip != null)
        {
            hitState = sm.AddState("Hit");
            hitState.motion = hitClip;
            hitState.speed = 1.8f; // 0.5s clip / 1.8 = ~0.28s very short flinch

            var idleToHit = idleState.AddTransition(hitState);
            idleToHit.hasExitTime = false;
            idleToHit.duration = 0.03f;
            idleToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            idleToHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

            if (runState != null)
            {
                var runToHit = runState.AddTransition(hitState);
                runToHit.hasExitTime = false;
                runToHit.duration = 0.03f;
                runToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                runToHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

                var hitToRun = hitState.AddTransition(runState);
                hitToRun.hasExitTime = true;
                hitToRun.exitTime = 0.60f;
                hitToRun.duration = 0.10f;
                hitToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
                hitToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
                hitToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            }

            if (landState != null)
            {
                var landToHit = landState.AddTransition(hitState);
                landToHit.hasExitTime = false;
                landToHit.duration = 0.03f;
                landToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                landToHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            }

            if (_crouchStateForLinks != null)
            {
                var crouchToHit = _crouchStateForLinks.AddTransition(hitState);
                crouchToHit.hasExitTime = false;
                crouchToHit.duration = 0.03f;
                crouchToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                crouchToHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");

                var hitToCrouch = hitState.AddTransition(_crouchStateForLinks);
                hitToCrouch.hasExitTime = true;
                hitToCrouch.exitTime = 0.60f;
                hitToCrouch.duration = 0.10f;
                hitToCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
                hitToCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            }

            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.65f;
            hitToIdle.duration = 0.12f;
            hitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            hitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            hitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        }

        if (fallClip != null)
        {
            var fallState = sm.AddState("Fall");
            fallState.motion = fallClip;

            var idleToFall = idleState.AddTransition(fallState);
            idleToFall.hasExitTime = false;
            idleToFall.duration = 0.06f;
            idleToFall.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");

            if (runState != null)
            {
                var runToFall = runState.AddTransition(fallState);
                runToFall.hasExitTime = false;
                runToFall.duration = 0.06f;
                runToFall.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
            }

            if (landState != null)
            {
                var landToFall = landState.AddTransition(fallState);
                landToFall.hasExitTime = false;
                landToFall.duration = 0.05f;
                landToFall.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
            }

            if (hitState != null)
            {
                var hitToFall = hitState.AddTransition(fallState);
                hitToFall.hasExitTime = false;
                hitToFall.duration = 0.05f;
                hitToFall.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
            }

            if (_crouchStateForLinks != null)
            {
                var crouchToFall = _crouchStateForLinks.AddTransition(fallState);
                crouchToFall.hasExitTime = false;
                crouchToFall.duration = 0.05f;
                crouchToFall.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
            }

            var fallToIdle = fallState.AddTransition(idleState);
            fallToIdle.hasExitTime = false;
            fallToIdle.duration = 0.15f;
            fallToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // =====================================================================================
    // Builders
    // =====================================================================================

    static GameObject CreateHub(GameObject parent, string name, float xMm, float yMm)
    {
        var hub = new GameObject(name);
        hub.transform.SetParent(parent.transform, false);
        hub.transform.localPosition = P(xMm, yMm);

        float frontZ = -TapeDepth * 0.5f;

        CreateCylinder(hub, "Core", Vector3.zero, U(HubCoreRadiusMm), TapeDepth, WhiteColor);

        // Place GearDisk slightly in front of the tape pack's front face (-TapeDepth*0.5) so they never Z-fight
        float ringDepth = 0.14f;
        float ringZ = frontZ - ringDepth * 0.5f - 0.01f;
        CreateVisualCylinder(hub, "GearDisk", new Vector3(0f, 0f, ringZ), U(HubGearRadiusMm), ringDepth, WhiteColor);

        for (int i = 0; i < GearTeeth; i++)
        {
            float a = i * Mathf.PI * 2f / GearTeeth;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var tooth = CreateVisualBox(hub, "Tooth_" + i,
                dir * U(HubGearRadiusMm + ToothLengthMm * 0.5f - 0.2f) + new Vector3(0f, 0f, ringZ),
                new Vector3(U(ToothLengthMm), U(ToothWidthMm), ringDepth), WhiteColor);
            tooth.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
        }

        float holeR = U(SpindleHoleRadiusMm);
        float holeFrontZ = ringZ - ringDepth * 0.5f;
        CreateVisualCylinder(hub, "SpindleHole", new Vector3(0f, 0f, holeFrontZ - 0.01f), holeR, 0.04f, DarkColor);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI * 2f / 6f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var key = CreateVisualBox(hub, "DriveKey_" + i,
                dir * (holeR - U(0.6f)) + new Vector3(0f, 0f, holeFrontZ - 0.03f),
                new Vector3(U(1.2f), U(0.9f), 0.06f), WhiteColor);
            key.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
        }
        return hub;
    }

    static GameObject CreateRoller(GameObject parent, string name, float xMm, float yMm)
    {
        var roller = CreateCylinder(parent, name, P(xMm, yMm), U(RollerRadiusMm), TapeDepth, WhiteColor);
        CreateVisualCylinder(parent, name + "_Axle", P(xMm, yMm, -TapeDepth * 0.5f - 0.05f), U(1.2f), 0.1f, MetalColor);
        return roller;
    }

    static void CreateScrew(GameObject parent, string name, float xMm, float yMm, float depth, float zCenter)
    {
        CreateCylinder(parent, name, P(xMm, yMm, zCenter), U(ScrewBossRadiusMm), depth, ShellColor);
        float frontFace = zCenter - depth * 0.5f;
        CreateVisualCylinder(parent, name + "_Head", P(xMm, yMm, frontFace - 0.05f), U(1.8f), 0.1f, MetalColor);
        CreateVisualBox(parent, name + "_Slot", P(xMm, yMm, frontFace - 0.11f),
            new Vector3(U(2.6f), U(0.4f), 0.02f), DarkColor).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    static void CreateAnchor(GameObject parent, string name, float xMm, float yMm)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = P(xMm, yMm);
    }

    static GameObject CreateSegment(GameObject parent, string name, Vector2 aMm, Vector2 bMm,
        float thicknessMm, float zDepth, float zCenter, Color color, bool withCollider)
    {
        Vector2 a = aMm * MmToUnit, b = bMm * MmToUnit;
        Vector2 d = b - a;
        Vector2 mid = (a + b) * 0.5f;
        var go = CreateBox(parent, name, new Vector3(mid.x, mid.y, zCenter),
            new Vector3(d.magnitude + U(thicknessMm), U(thicknessMm), zDepth), color);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        if (!withCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Group(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    // =====================================================================================
    // Primitive helpers
    // =====================================================================================

    static GameObject CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        SetColor(go, color);
        return go;
    }

    static GameObject CreateVisualBox(GameObject parent, string name, Vector3 localPos, Vector3 size, Color color)
    {
        var go = CreateBox(parent, name, localPos, size, color);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject CreateCylinder(GameObject parent, string name, Vector3 localPos, float radius, float depth, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(radius * 2f, depth * 0.5f, radius * 2f);

        Mesh cylMesh = GetOrCreateSmoothCylinderMesh();
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = cylMesh;
        go.AddComponent<MeshRenderer>();

        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = cylMesh;
        mc.convex = true;

        SetColor(go, color);
        return go;
    }

    static GameObject CreateVisualCylinder(GameObject parent, string name, Vector3 localPos, float radius, float depth, Color color)
    {
        var go = CreateCylinder(parent, name, localPos, radius, depth, color);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    const string SmoothCylinderAssetPath = "Assets/Models/SmoothCylinder96.asset";

    static Mesh GetOrCreateSmoothCylinderMesh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(SmoothCylinderAssetPath);
        if (existing != null) return existing;

        const int segs = 96;
        const float r = 0.5f;
        const float halfH = 1.0f; // total height = 2.0 along local Y, matching Unity's Cylinder primitive

        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        // Side ring vertices (smooth normals)
        for (int i = 0; i <= segs; i++)
        {
            float a = (i % segs) * Mathf.PI * 2f / segs;
            float cx = Mathf.Cos(a);
            float cz = Mathf.Sin(a);
            Vector3 n = new Vector3(cx, 0f, cz);
            verts.Add(new Vector3(cx * r, -halfH, cz * r)); norms.Add(n);
            verts.Add(new Vector3(cx * r, +halfH, cz * r)); norms.Add(n);
        }
        for (int i = 0; i < segs; i++)
        {
            int b = i * 2;
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 3);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
        }

        // Top cap (+Y)
        int topCenter = verts.Count;
        verts.Add(new Vector3(0f, +halfH, 0f)); norms.Add(Vector3.up);
        for (int i = 0; i <= segs; i++)
        {
            float a = (i % segs) * Mathf.PI * 2f / segs;
            verts.Add(new Vector3(Mathf.Cos(a) * r, +halfH, Mathf.Sin(a) * r));
            norms.Add(Vector3.up);
        }
        for (int i = 0; i < segs; i++)
        {
            tris.Add(topCenter);
            tris.Add(topCenter + 1 + i + 1);
            tris.Add(topCenter + 1 + i);
        }

        // Bottom cap (-Y)
        int botCenter = verts.Count;
        verts.Add(new Vector3(0f, -halfH, 0f)); norms.Add(Vector3.down);
        for (int i = 0; i <= segs; i++)
        {
            float a = (i % segs) * Mathf.PI * 2f / segs;
            verts.Add(new Vector3(Mathf.Cos(a) * r, -halfH, Mathf.Sin(a) * r));
            norms.Add(Vector3.down);
        }
        for (int i = 0; i < segs; i++)
        {
            tris.Add(botCenter);
            tris.Add(botCenter + 1 + i);
            tris.Add(botCenter + 1 + i + 1);
        }

        var mesh = new Mesh { name = "SmoothCylinder96" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, SmoothCylinderAssetPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    static void SetColor(GameObject go, Color color)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        r.sharedMaterial = mat;
    }

    static void SetTransparentMaterial(GameObject go, Color color)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent in URP Lit
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
