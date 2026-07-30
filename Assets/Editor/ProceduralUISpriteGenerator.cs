// Editor-only. Procedurally generates premium, 9-sliceable UI sprites into Assets/UI/Generated/.
using System.IO;
using UnityEngine;
using UnityEditor;

public static class ProceduralUISpriteGenerator
{
    const string FolderRel   = "UI/Generated";
    const string FolderAsset = "Assets/UI/Generated";

    const int   PanelSize     = 96;
    const float PanelRadius   = 22f;
    const int   PanelBorder    = 30;
    const float PanelRimWidth = 2f;
    const float PanelFill     = 0.90f;
    const float PanelRim      = 1.00f;
    const float ButtonTopMul  = 1.00f;
    const float ButtonBotMul  = 0.86f;

    const int   ShadowSize     = 128;
    const int   ShadowBorder   = 48;
    const float ShadowInset    = 24f;
    const float ShadowRadius   = 16f;
    const float ShadowFalloff  = 24f;
    const float ShadowMaxAlpha = 0.45f;

    const int   VignetteSize  = 512;
    const float VignetteInner = 0.45f;
    const float VignetteMaxA  = 0.60f;

    [MenuItem("Tools/UI/Generate Premium UI Sprites")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, FolderRel));
        AssetDatabase.Refresh();

        var panelB = new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder);

        WriteSprite("ui_panel.png",  PanelSize, PanelSize,
            BuildPanel(PanelSize, PanelRadius, PanelRimWidth, PanelFill, PanelRim, false), panelB);

        WriteSprite("ui_button.png", PanelSize, PanelSize,
            BuildPanel(PanelSize, PanelRadius, PanelRimWidth, PanelFill, PanelRim, true), panelB);

        WriteSprite("ui_shadow.png", ShadowSize, ShadowSize,
            BuildShadow(ShadowSize, ShadowInset, ShadowRadius, ShadowFalloff, ShadowMaxAlpha),
            new Vector4(ShadowBorder, ShadowBorder, ShadowBorder, ShadowBorder));

        WriteSprite("ui_vignette.png", VignetteSize, VignetteSize,
            BuildVignette(VignetteSize, VignetteInner, VignetteMaxA), Vector4.zero);

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
        Debug.Log("[ProceduralUISpriteGenerator] Generated 4 sprites in " + FolderAsset + "/");
    }

    static float SdRoundBox(float px, float py, float halfX, float halfY, float radius)
    {
        float qx = Mathf.Abs(px) - halfX + radius;
        float qy = Mathf.Abs(py) - halfY + radius;
        float ox = Mathf.Max(qx, 0f);
        float oy = Mathf.Max(qy, 0f);
        float outside = Mathf.Sqrt(ox * ox + oy * oy);
        float inside  = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - radius;
    }

    static float Smooth01(float edge0, float edge1, float x)
    {
        if (Mathf.Approximately(edge0, edge1)) return x < edge0 ? 0f : 1f;
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static float EdgeCoverage(float sdfPixels) { return Mathf.Clamp01(0.5f - sdfPixels); }

    static Color[] BuildPanel(int size, float radius, float rimWidth, float fill, float rim, bool verticalHighlight)
    {
        var px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float grad = verticalHighlight ? Mathf.Lerp(ButtonBotMul, ButtonTopMul, (float)y / (size - 1)) : 1f;
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float sdf = SdRoundBox(dx, dy, half, half, radius);
                float a = EdgeCoverage(sdf);
                float edgeDist = -sdf;
                float fillT = Smooth01(rimWidth, rimWidth + 1f, edgeDist);
                float c = Mathf.Lerp(rim, fill, fillT) * grad;
                px[y * size + x] = new Color(c, c, c, a);
            }
        }
        return px;
    }

    static Color[] BuildShadow(int size, float inset, float radius, float falloff, float maxA)
    {
        var px = new Color[size * size];
        float half = size * 0.5f;
        float halfCore = half - inset;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - half;
            float dy = y + 0.5f - half;
            float sdf = SdRoundBox(dx, dy, halfCore, halfCore, radius);
            float a = sdf <= 0f ? maxA : maxA * (1f - Smooth01(0f, falloff, sdf));
            px[y * size + x] = new Color(0f, 0f, 0f, a);
        }
        return px;
    }

    static Color[] BuildVignette(int size, float innerR, float maxA)
    {
        var px = new Color[size * size];
        float half = size * 0.5f;
        float maxD = Mathf.Sqrt(2f) * half;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - half;
            float dy = y + 0.5f - half;
            float nd = Mathf.Sqrt(dx * dx + dy * dy) / maxD;
            float a = maxA * Smooth01(innerR, 1f, nd);
            px[y * size + x] = new Color(0f, 0f, 0f, a);
        }
        return px;
    }

    static void WriteSprite(string fileName, int w, int h, Color[] pixels, Vector4 border)
    {
        string absPath   = Path.Combine(Application.dataPath, FolderRel, fileName);
        string assetPath = FolderAsset + "/" + fileName;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply(false);
        byte[] png = ImageConversion.EncodeToPNG(tex);
        UnityEngine.Object.DestroyImmediate(tex);
        File.WriteAllBytes(absPath, png);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) { Debug.LogError("[ProceduralUISpriteGenerator] No TextureImporter at " + assetPath); return; }

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.spriteBorder        = border;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode          = FilterMode.Bilinear;
        importer.mipmapEnabled       = false;
        importer.wrapMode            = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture         = true;
        importer.npotScale           = TextureImporterNPOTScale.None;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize      = 2048;
        importer.SaveAndReimport();

        Debug.Log("[ProceduralUISpriteGenerator] Wrote " + assetPath + " (" + w + "x" + h + ", border " + border + ")");
    }
}
