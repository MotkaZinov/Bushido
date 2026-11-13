using UnityEditor;
public sealed class BushidoMapIconImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Bushido/Resources/MapInk/"))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;
        texture.maxTextureSize=256;
        texture.alphaIsTransparency=true;
        texture.mipmapEnabled=false;
        texture.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        texture.filterMode=UnityEngine.FilterMode.Bilinear;
        texture.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
