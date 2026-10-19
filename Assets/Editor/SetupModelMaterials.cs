using UnityEngine;
using UnityEditor;
using System.IO;

public static class SetupModelMaterials
{
    [MenuItem("Tools/Setup Model Materials and Rebuild")]
    public static void SetupAndRebuild()
    {
        SetupMaterials();
        CassetteLevelGenerator.Generate();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("=== [SetupModelMaterials] Materials Created, Level Regenerated and Saved! ===");
    }

    public static void SetupMaterials()
    {
        EnsureModelScale("Assets/Models/Ladder.fbx", 100f);
        EnsureModelScale("Assets/Models/Railway.fbx", 100f);
        EnsureModelScale("Assets/Models/Train.fbx", 100f);

        var urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        CreateMat("Assets/Models/Ladder_Material.mat",
            "Assets/Models/Ladder_Textures/ladder_albedo.png",
            "Assets/Models/Ladder_Textures/ladder_normal.png",
            "Assets/Models/Ladder_Textures/ladder_metallic.png",
            urpShader);

        CreateMat("Assets/Models/Railway_Material.mat",
            "Assets/Models/Railway_Textures/railway_albedo.png",
            "Assets/Models/Railway_Textures/railway_normal.png",
            "Assets/Models/Railway_Textures/railway_metallic.png",
            urpShader);

        CreateMat("Assets/Models/Train_Material.mat",
            "Assets/Models/Train_Textures/train_albedo.png",
            "Assets/Models/Train_Textures/train_normal.png",
            "Assets/Models/Train_Textures/train_metallic.png",
            urpShader);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void EnsureModelScale(string path, float targetScale)
    {
        if (!File.Exists(path)) return;
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null && Mathf.Abs(importer.globalScale - targetScale) > 0.001f)
        {
            importer.globalScale = targetScale;
            importer.SaveAndReimport();
        }
    }

    static void CreateMat(string matPath, string albedoPath, string normalPath, string metallicPath, Shader shader)
    {
        // Configure normal map texture importer
        if (File.Exists(normalPath))
        {
            var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }

        var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        if (albedoTex != null)
        {
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetTexture("_MainTex", albedoTex);
            mat.color = Color.white;
            mat.SetColor("_BaseColor", Color.white);
        }

        var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.EnableKeyword("_NORMALMAP");
        }

        var metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
        if (metallicTex != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallicTex);
        }

        mat.SetFloat("_Smoothness", 0.65f);
        EditorUtility.SetDirty(mat);
    }
}
