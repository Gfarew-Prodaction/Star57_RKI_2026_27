using UnityEditor;

namespace Zvezda57.Editor
{
    public sealed class OfficeModelImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Textures/Office/"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
            if (assetPath.Contains("_Normal_"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }

            if (assetPath.Contains("_Metallic_") || assetPath.Contains("_Roughness_") || assetPath.Contains("_Mixed_AO_"))
            {
                importer.sRGBTexture = false;
            }
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Models/Office/"))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
        }
    }
}
