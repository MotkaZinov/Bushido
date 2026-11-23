using UnityEditor;

namespace Bushido.Editor
{
    public sealed class FrogImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            // The weapon meshes are rebuilt from their UVs at runtime, so they must stay readable in builds.
            if (assetPath.Contains("/Models/Weapons/") && assetPath.EndsWith(".fbx"))
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
