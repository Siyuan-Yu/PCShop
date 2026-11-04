using UnityEditor;
using UnityEngine;

// Portraits are small UI textures; source PNGs remain untouched for later reuse.
public sealed class NpcPortraitImportRules : AssetPostprocessor
{
    public const string Root = "Assets/Resources/PortraitsRetro/";
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
        Configure((TextureImporter)assetImporter);
    }
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
            if (path.StartsWith(Root, System.StringComparison.Ordinal))
            { PCShopSell.Game.UI.GameUi.InvalidatePortraitCache(); break; }
    }
    public static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.maxTextureSize = 512;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.compressionQuality = 60;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.sRGBTexture = true;
    }
    [MenuItem("PCShop/Art/Reimport retro NPC portraits")]
    public static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer); importer.SaveAndReimport();
        }
    }
}
