using UnityEngine;

/// <summary>
/// Utility builder that attaches seamless telescoping needle spikes to GuidePin_Left / GuidePin_Right
/// and builds cassette-themed magnetic head / solenoid dropper assemblies along the top ceiling.
/// </summary>
public static class CassetteParkourObstacleBuilder
{
    static void RemoveColliderSafe(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Object.Destroy(col);
        else Object.DestroyImmediate(col);
    }

    public static void BuildOrUpdateParkourObstacles(Transform cassetteRoot, CassettePlayer player)
    {
        if (cassetteRoot == null) return;
        Transform mech = cassetteRoot.Find("Mechanism");
        Transform shell = cassetteRoot.Find("Shell");
        if (mech == null) return;

        // 1. Attach seamless telescoping needle spikes (升降刺) to GuidePin_Left and GuidePin_Right
        Transform pinL = mech.Find("GuidePin_Left");
        if (pinL != null)
        {
            var spikeL = pinL.GetComponent<CassettePinSpikeObstacle>();
            if (spikeL == null) spikeL = pinL.gameObject.AddComponent<CassettePinSpikeObstacle>();
            spikeL.damage = 50f;
            spikeL.phaseOffset = 0f;
            spikeL.player = player;
            spikeL.EnsureVisualSpikes();
        }

        Transform pinR = mech.Find("GuidePin_Right");
        if (pinR != null)
        {
            var spikeR = pinR.GetComponent<CassettePinSpikeObstacle>();
            if (spikeR == null) spikeR = pinR.gameObject.AddComponent<CassettePinSpikeObstacle>();
            spikeR.damage = 50f;
            spikeR.phaseOffset = 1.35f; // Alternating rhythm with left pin
            spikeR.player = player;
            spikeR.EnsureVisualSpikes();
        }

        // 2. Ensure TapeHeadPad obstacle deals 50 damage per hit
        var headObs = mech.GetComponentInChildren<CassetteHeadObstacle>();
        if (headObs != null)
        {
            headObs.contactDamage = 50f;
            headObs.lethalCrushUnderneath = false;
            headObs.player = player;
        }

        // 3. Build cassette-themed overhead solenoid head droppers along the upper ceiling
        Transform existingGroup = mech.Find("OverheadDroppers");
        if (existingGroup != null)
        {
            var validDroppers = existingGroup.GetComponentsInChildren<CassetteOverheadDropper>(true);
            bool allValid = validDroppers != null && validDroppers.Length == 4;
            if (allValid)
            {
                for (int i = 0; i < validDroppers.Length; i++)
                {
                    if (validDroppers[i].builtVersion != CassetteOverheadDropper.DropperVisualVersion)
                    {
                        allValid = false;
                        break;
                    }
                    validDroppers[i].player = player;
                    validDroppers[i].AutoBindPartsIfNeeded();
                }
            }

            if (!allValid)
            {
            // Detach before destroying so mech.Find("OverheadDroppers") never collides in Play Mode
            existingGroup.SetParent(null);
            existingGroup.name = "_DestroyedOverheadDroppers";
            if (Application.isPlaying) Object.Destroy(existingGroup.gameObject);
            else Object.DestroyImmediate(existingGroup.gameObject);
        }

        float ceilingBottomY = 7.65f;
        float floorTopY = -7.40f;
        if (shell != null)
        {
            Transform wallTop = shell.Find("Wall_Top");
            if (wallTop != null)
                ceilingBottomY = wallTop.localPosition.y - wallTop.localScale.y * 0.5f;
            Transform wallBottom = shell.Find("Wall_Bottom");
            if (wallBottom != null)
                floorTopY = wallBottom.localPosition.y + wallBottom.localScale.y * 0.5f;
        }

        var dropperGroup = new GameObject("OverheadDroppers");
        dropperGroup.transform.SetParent(mech, false);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var darkMat = new Material(shader) { color = new Color(0.22f, 0.23f, 0.26f) };
        if (darkMat.HasProperty("_BaseColor")) darkMat.SetColor("_BaseColor", darkMat.color);
        var metalMat = new Material(shader) { color = new Color(0.64f, 0.67f, 0.72f) };
        if (metalMat.HasProperty("_BaseColor")) metalMat.SetColor("_BaseColor", metalMat.color);
        var copperMat = new Material(shader) { color = new Color(0.80f, 0.45f, 0.20f) };
        if (copperMat.HasProperty("_BaseColor")) copperMat.SetColor("_BaseColor", copperMat.color);

        float[] dropperXPositions = new float[] { -5.2f, -2.1f, 2.1f, 5.2f };
        float[] phaseOffsets = new float[] { 0.0f, 0.48f, 0.96f, 1.44f };

        for (int i = 0; i < dropperXPositions.Length; i++)
        {
            var turret = new GameObject("OverheadDropper_" + (i + 1));
            turret.transform.SetParent(dropperGroup.transform, false);
            turret.transform.localPosition = new Vector3(dropperXPositions[i], ceilingBottomY, 0f);

            // Walkman-style upper bracket housing
            var mount = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mount.name = "CeilingBracket";
            RemoveColliderSafe(mount);
            mount.transform.SetParent(turret.transform, false);
            mount.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            mount.transform.localScale = new Vector3(1.25f, 0.44f, 1.30f);
            mount.GetComponent<Renderer>().sharedMaterial = darkMat;

            var shieldPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shieldPlate.name = "MetalShieldPlate";
            RemoveColliderSafe(shieldPlate);
            shieldPlate.transform.SetParent(turret.transform, false);
            shieldPlate.transform.localPosition = new Vector3(0f, -0.24f, -0.58f);
            shieldPlate.transform.localScale = new Vector3(0.98f, 0.32f, 0.16f);
            shieldPlate.GetComponent<Renderer>().sharedMaterial = metalMat;

            // Telescoping dual-rail guide shaft
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "TelescopingArm";
            RemoveColliderSafe(arm);
            arm.transform.SetParent(turret.transform, false);
            arm.transform.localPosition = new Vector3(0f, -0.80f, 0f);
            arm.transform.localScale = new Vector3(0.36f, 1.60f, 0.46f);
            arm.GetComponent<Renderer>().sharedMaterial = metalMat;

            // Cassette magnetic erase/record head assembly at the bottom tip of the arm
            var nozzle = new GameObject("NozzleTip");
            nozzle.transform.SetParent(turret.transform, false);
            nozzle.transform.localPosition = new Vector3(0f, -1.60f, 0f);

            // Outer metallic head shield case
            var headCase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headCase.name = "HeadShieldCase";
            RemoveColliderSafe(headCase);
            headCase.transform.SetParent(nozzle.transform, false);
            headCase.transform.localPosition = Vector3.zero;
            headCase.transform.localScale = new Vector3(0.84f, 0.42f, 0.82f);
            headCase.GetComponent<Renderer>().sharedMaterial = metalMat;

            // Dark ferrite core & tape gap at the bottom of the magnetic head
            var ferrite = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ferrite.name = "HeadFerriteCore";
            RemoveColliderSafe(ferrite);
            ferrite.transform.SetParent(nozzle.transform, false);
            ferrite.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            ferrite.transform.localScale = new Vector3(0.56f, 0.16f, 0.86f);
            ferrite.GetComponent<Renderer>().sharedMaterial = darkMat;

            // Glowing electromagnetic copper coil band wrapped around the head
            var coilBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coilBand.name = "CopperCoilBand";
            RemoveColliderSafe(coilBand);
            coilBand.transform.SetParent(nozzle.transform, false);
            coilBand.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            coilBand.transform.localScale = new Vector3(0.88f, 0.18f, 0.86f);
            var coilRenderer = coilBand.GetComponent<Renderer>();
            coilRenderer.sharedMaterial = new Material(copperMat);

            var dropper = turret.AddComponent<CassetteOverheadDropper>();
            dropper.telescopingArm = arm.transform;
            dropper.nozzleTip = nozzle.transform;
            dropper.warningRenderer = coilRenderer;
            dropper.player = player;
            dropper.projectileDamage = 50f;
            dropper.retractedArmLength = 1.60f;
            dropper.extendDistance = 5.20f;
            dropper.floorDestroyY = floorTopY + 0.15f;
            dropper.phaseOffset = phaseOffsets[i];
            dropper.builtVersion = CassetteOverheadDropper.DropperVisualVersion;
        }
            } // end if (!allValid)

        // 4. Wall-mounted crossbow turrets on both sides of the bottom parkour runway
        BuildCrossbowTurrets(mech, player);
    }

    // ==========================================================================================
    // Crossbow turrets (both sides of the bottom runway)
    // ==========================================================================================

    static Material MakeTurretMaterial(Shader shader, Color color, float smoothness)
    {
        var m = new Material(shader) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Lerp(0.1f, 0.8f, smoothness));
        return m;
    }

    static GameObject TurretPart(Transform parent, PrimitiveType type, string partName,
        Vector3 localPos, Vector3 scale, Quaternion localRot, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = partName;
        RemoveColliderSafe(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.transform.localRotation = localRot;
        var r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        return go;
    }

    static void BuildCrossbowTurrets(Transform mech, CassettePlayer player)
    {
        Transform existingGroup = mech.Find("CrossbowTurrets");
        if (existingGroup != null)
        {
            var existingTurrets = existingGroup.GetComponentsInChildren<CassetteCrossbowTurret>(true);
            bool allValid = existingTurrets != null && existingTurrets.Length == 4;
            if (allValid)
            {
                for (int i = 0; i < existingTurrets.Length; i++)
                {
                    if (existingTurrets[i].builtVersion != CassetteCrossbowTurret.TurretVisualVersion ||
                        existingTurrets[i].aimLine == null)
                    {
                        allValid = false;
                        break;
                    }
                    existingTurrets[i].player = player;
                    existingTurrets[i].AutoBindPartsIfNeeded();
                }
            }
            if (allValid) return;

            existingGroup.SetParent(null);
            existingGroup.name = "_DestroyedCrossbowTurrets";
            if (Application.isPlaying) Object.Destroy(existingGroup.gameObject);
            else Object.DestroyImmediate(existingGroup.gameObject);
        }

        var group = new GameObject("CrossbowTurrets");
        group.transform.SetParent(mech, false);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var woodMat = MakeTurretMaterial(shader, new Color(0.36f, 0.22f, 0.11f), 0.20f);
        var darkWoodMat = MakeTurretMaterial(shader, new Color(0.24f, 0.14f, 0.07f), 0.15f);
        var metalMat = MakeTurretMaterial(shader, new Color(0.62f, 0.65f, 0.71f), 0.80f);
        var darkMetalMat = MakeTurretMaterial(shader, new Color(0.20f, 0.21f, 0.24f), 0.55f);
        var stringMat = MakeTurretMaterial(shader, new Color(0.88f, 0.87f, 0.78f), 0.30f);
        var glowMat = MakeTurretMaterial(shader, new Color(0.55f, 0.20f, 0.07f), 0.55f);

        // Warning line material (linear aim telegraph drawn from the muzzle).
        Material aimLineMat = null;
        var lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            aimLineMat = new Material(lineShader) { color = new Color(1f, 0.35f, 0.12f, 0.85f) };
            if (aimLineMat.HasProperty("_BaseColor")) aimLineMat.SetColor("_BaseColor", new Color(1f, 0.35f, 0.12f, 0.85f));
        }

        // sideSign -1 = left wall, +1 = right wall.  Two turrets per side at different heights.
        float[] sideSigns = new float[] { -1f, 1f };
        float[] heights = new float[] { -5.55f, -2.85f };
        int index = 0;
        for (int s = 0; s < sideSigns.Length; s++)
        {
            float side = sideSigns[s];
            for (int h = 0; h < heights.Length; h++)
            {
                var turret = new GameObject($"CrossbowTurret_{(side < 0f ? "L" : "R")}_{h + 1}");
                turret.transform.SetParent(group.transform, false);
                // Mounted on the side wall, local +X points towards the centre of the runway.
                turret.transform.localPosition = new Vector3(side * 17.25f, heights[h], 0f);
                turret.transform.localRotation = side < 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

                // ---- Fixed wall mount (does not rotate) ----
                TurretPart(turret.transform, PrimitiveType.Cube, "WallPlate",
                    new Vector3(-1.02f, 0f, 0f), new Vector3(0.18f, 0.92f, 0.92f), Quaternion.identity, darkMetalMat);
                TurretPart(turret.transform, PrimitiveType.Cube, "WallBracketArm",
                    new Vector3(-0.52f, 0f, 0f), new Vector3(1.00f, 0.18f, 0.18f), Quaternion.identity, metalMat);
                TurretPart(turret.transform, PrimitiveType.Cube, "SwivelBase",
                    new Vector3(-0.12f, 0f, 0f), new Vector3(0.42f, 0.52f, 0.52f), Quaternion.identity, darkMetalMat);

                // ---- Rotating head ----
                var head = new GameObject("HeadPivot");
                head.transform.SetParent(turret.transform, false);
                head.transform.localPosition = Vector3.zero;

                // Wooden stock / body of the crossbow
                TurretPart(head.transform, PrimitiveType.Cube, "StockBody",
                    new Vector3(0.22f, 0f, 0f), new Vector3(1.00f, 0.24f, 0.26f), Quaternion.identity, woodMat);
                TurretPart(head.transform, PrimitiveType.Cube, "StockButt",
                    new Vector3(-0.23f, -0.04f, 0f), new Vector3(0.30f, 0.34f, 0.30f), Quaternion.identity, darkWoodMat);
                TurretPart(head.transform, PrimitiveType.Cube, "BoltRail",
                    new Vector3(0.40f, 0.145f, 0f), new Vector3(1.10f, 0.05f, 0.10f), Quaternion.identity, metalMat);

                // Bow limbs (two angled metal blades at the front) + tips
                TurretPart(head.transform, PrimitiveType.Cube, "LimbUpper",
                    new Vector3(0.76f, 0.25f, 0f), new Vector3(0.66f, 0.075f, 0.085f), Quaternion.Euler(0f, 0f, -24f), metalMat);
                TurretPart(head.transform, PrimitiveType.Cube, "LimbLower",
                    new Vector3(0.76f, -0.25f, 0f), new Vector3(0.66f, 0.075f, 0.085f), Quaternion.Euler(0f, 0f, 24f), metalMat);
                TurretPart(head.transform, PrimitiveType.Cube, "LimbTipUpper",
                    new Vector3(1.04f, 0.39f, 0f), new Vector3(0.10f, 0.13f, 0.10f), Quaternion.identity, darkMetalMat);
                TurretPart(head.transform, PrimitiveType.Cube, "LimbTipLower",
                    new Vector3(1.04f, -0.39f, 0f), new Vector3(0.10f, 0.13f, 0.10f), Quaternion.identity, darkMetalMat);

                // Bow string (animated backwards while charging)
                var bowString = TurretPart(head.transform, PrimitiveType.Cube, "BowString",
                    new Vector3(0.46f, 0f, 0f), new Vector3(0.035f, 0.88f, 0.035f), Quaternion.identity, stringMat);

                // Trigger housing + hand crank detail
                TurretPart(head.transform, PrimitiveType.Cube, "TriggerHousing",
                    new Vector3(-0.02f, -0.17f, 0f), new Vector3(0.26f, 0.18f, 0.20f), Quaternion.identity, darkMetalMat);
                TurretPart(head.transform, PrimitiveType.Cylinder, "CrankWheel",
                    new Vector3(-0.20f, -0.20f, 0.18f), new Vector3(0.20f, 0.035f, 0.20f), Quaternion.Euler(90f, 0f, 0f), metalMat);

                // Loaded bolt (hidden for ~0.55s after each shot while "reloading")
                var loaded = new GameObject("LoadedBolt");
                loaded.transform.SetParent(head.transform, false);
                loaded.transform.localPosition = Vector3.zero;
                TurretPart(loaded.transform, PrimitiveType.Cube, "LoadedBoltShaft",
                    new Vector3(0.55f, 0.19f, 0f), new Vector3(0.92f, 0.035f, 0.035f), Quaternion.identity, darkWoodMat);
                TurretPart(loaded.transform, PrimitiveType.Cube, "LoadedBoltTip",
                    new Vector3(1.02f, 0.19f, 0f), new Vector3(0.14f, 0.07f, 0.07f), Quaternion.identity, metalMat);

                // Warning glow at the tip (pulses during the charge-up)
                var glow = TurretPart(head.transform, PrimitiveType.Sphere, "WarningGlow",
                    new Vector3(1.06f, 0f, 0f), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.identity, glowMat);

                // Muzzle / bolt spawn point
                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(head.transform, false);
                muzzle.transform.localPosition = new Vector3(1.12f, 0.03f, 0f);

                // Thin warning line: follows the player during the first part of the charge, then
                // stays locked on the last aim point so the shot can be dodged by stepping off it.
                var aimLineGo = new GameObject("AimLine");
                aimLineGo.transform.SetParent(turret.transform, false);
                var aimLine = aimLineGo.AddComponent<LineRenderer>();
                aimLine.positionCount = 2;
                aimLine.useWorldSpace = true;
                aimLine.startWidth = 0.035f;
                aimLine.endWidth = 0.02f;
                aimLine.numCapVertices = 0;
                aimLine.textureMode = LineTextureMode.Stretch;
                aimLine.alignment = LineAlignment.View;
                aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                aimLine.receiveShadows = false;
                aimLine.enabled = false;
                if (aimLineMat != null) aimLine.sharedMaterial = aimLineMat;
                aimLine.startColor = new Color(1f, 0.6f, 0.18f, 0.45f);
                aimLine.endColor = new Color(1f, 0.25f, 0.10f, 0.75f);

                var turretComp = turret.AddComponent<CassetteCrossbowTurret>();
                turretComp.player = player;
                turretComp.turretHead = head.transform;
                turretComp.muzzle = muzzle.transform;
                turretComp.warningRenderer = glow.GetComponent<Renderer>();
                turretComp.bowString = bowString.transform;
                turretComp.loadedBolt = loaded.transform;
                turretComp.aimLine = aimLine;
                turretComp.builtVersion = CassetteCrossbowTurret.TurretVisualVersion;
                turretComp.firstShotPhase = index * 0.65f;
                index++;
            }
        }
    }
}
