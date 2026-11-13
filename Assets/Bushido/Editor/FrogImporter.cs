using UnityEditor;

namespace Bushido.Editor
{
    public sealed class FrogImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath.Contains("/Models/Weapons/katana/") && assetPath.EndsWith(".fbx"))
            {
                ((ModelImporter)assetImporter).isReadable = true;
                return;
            }
            if (!assetPath.EndsWith("/Models/Yokai_Rana.fbx")) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
