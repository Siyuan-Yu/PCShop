using UnityEditor;
using UnityEngine;

// Portraits are small UI textures; source PNGs remain untouched for later reuse.
public sealed class NpcPortraitImportRules : AssetPostprocessor
{
    public const string AnimeRoot = "Assets/Resources/PortraitsAnime/";
    public const string RelationshipRoot = "Assets/Resources/RelationshipPortraits/";
    private static bool IsPortrait(string path) => path.StartsWith(AnimeRoot, System.StringComparison.Ordinal)
        || path.StartsWith(RelationshipRoot, System.StringComparison.Ordinal);
    private void OnPreprocessTexture()
    {
        if (!IsPortrait(assetPath)) return;
        Configure((TextureImporter)assetImporter);
    }
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (System.Array.Exists(imported, IsPortrait) || System.Array.Exists(deleted, IsPortrait)
            || System.Array.Exists(moved, IsPortrait) || System.Array.Exists(movedFrom, IsPortrait))
            PCShopSell.Game.UI.GameUi.InvalidatePortraitCache();
    }
    public static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        // Anime portraits are also cut out and shown large in event CGs; 512 blurs the hair edges into halos.
        importer.maxTextureSize = importer.assetPath.StartsWith(AnimeRoot, System.StringComparison.Ordinal) ? 1024 : 512;
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
    [MenuItem("PCShop/Art/重新导入正式人物头像")]
    public static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AnimeRoot.TrimEnd('/'), RelationshipRoot.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer); importer.SaveAndReimport();
        }
    }
}
